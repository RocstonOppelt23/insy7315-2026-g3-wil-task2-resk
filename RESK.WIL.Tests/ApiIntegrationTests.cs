using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RESK.WIL.Controllers.API;
using RESK.WIL.Data;
using RESK.WIL.Security;
using Xunit;

namespace RESK.WIL.Tests
{
    // Starts the real app in memory (in-memory database instead of SQL Server).
    public sealed class ReskAppFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = "resk-app-" + Guid.NewGuid();


        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Database:MigrateOnStartup", "false");
            builder.UseSetting("RateLimiting:ApiPerMinute", "20");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions>();

                DbContextOptions<ApplicationDbContext> options =
                    new DbContextOptionsBuilder<ApplicationDbContext>()
                        .UseInMemoryDatabase(_databaseName)
                        .Options;

                services.AddSingleton(options);
                services.AddSingleton<DbContextOptions>(options);
            });
        }


        // Plain http client that doesn't follow redirects.
        public HttpClient Client() =>
            CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }


    [Collection(DatabaseCollection.Name)]
    public class ApiIntegrationTests
    {
        [Fact]
        public async Task Health_endpoint_reports_healthy_database()
        {
            using var app = new ReskAppFactory();

            HttpResponseMessage response = await app.Client().GetAsync("/health");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("\"status\":\"Healthy\"", body);
            Assert.Contains("\"name\":\"database\"", body);
        }


        [Fact]
        public async Task Swagger_document_lists_every_api_endpoint_and_no_mvc_pages()
        {
            using var app = new ReskAppFactory();

            HttpResponseMessage response = await app.Client().GetAsync("/swagger/v1/swagger.json");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            List<string> paths = doc.RootElement.GetProperty("paths")
                .EnumerateObject()
                .Select(p => p.Name)
                .ToList();

            Assert.Contains("/api/proposals", paths);
            Assert.Contains("/api/proposals/{id}/submit", paths);
            Assert.Contains("/api/review/queue", paths);
            Assert.Contains("/api/review/{id}/approve", paths);
            Assert.Contains("/api/users/create", paths);
            Assert.Contains("/api/system/update", paths);
            Assert.All(paths, p => Assert.StartsWith("/api/", p));
        }


        [Fact]
        public async Task Swagger_ui_page_is_served()
        {
            using var app = new ReskAppFactory();

            HttpResponseMessage response = await app.Client().GetAsync("/swagger/index.html");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }


        [Theory]
        [InlineData("/api/proposals", 401)]
        [InlineData("/api/review/queue", 401)]
        [InlineData("/api/users", 401)]
        [InlineData("/api/system", 404)]
        public async Task Api_errors_use_problem_details(string url, int expectedStatus)
        {
            using var app = new ReskAppFactory();

            HttpResponseMessage response = await app.Client().GetAsync(url);

            Assert.Equal(expectedStatus, (int)response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

            using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(expectedStatus, doc.RootElement.GetProperty("status").GetInt32());
            Assert.True(doc.RootElement.TryGetProperty("title", out _));
        }


        [Fact]
        public async Task Signed_out_page_requests_still_redirect_to_login()
        {
            using var app = new ReskAppFactory();

            HttpResponseMessage response = await app.Client().GetAsync("/Producer");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Login", response.Headers.Location?.ToString());
        }


        [Fact]
        public async Task Api_is_rate_limited_with_problem_details_and_retry_after()
        {
            using var app = new ReskAppFactory();
            HttpClient client = app.Client();

            HttpResponseMessage? limited = null;

            for (int i = 0; i < 70 && limited == null; i++)
            {
                HttpResponseMessage response = await client.GetAsync("/api/system");

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    limited = response;
                }
            }

            Assert.NotNull(limited);
            Assert.True(limited!.Headers.Contains("Retry-After"));
            Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
        }


        [Fact]
        public void Every_api_controller_uses_the_shared_base_and_rate_limit()
        {
            Type[] controllers = typeof(ApiControllerBase).Assembly.GetTypes()
                .Where(t => typeof(ControllerBase).IsAssignableFrom(t) &&
                            !t.IsAbstract &&
                            t.GetCustomAttribute<RouteAttribute>()?.Template.StartsWith("api/") == true)
                .ToArray();

            Assert.Equal(4, controllers.Length);

            foreach (Type controller in controllers)
            {
                Assert.True(typeof(ApiControllerBase).IsAssignableFrom(controller), controller.Name);

                EnableRateLimitingAttribute? limit =
                    controller.GetCustomAttribute<EnableRateLimitingAttribute>(inherit: true);

                Assert.Equal(ReskRateLimiting.Api, limit?.PolicyName);
            }
        }


        [Theory]
        [InlineData("Login")]
        [InlineData("Register")]
        [InlineData("ForgotPassword")]
        public void Sign_in_forms_are_rate_limited(string action)
        {
            MethodInfo post = typeof(RESK.WIL.Controllers.AccountController)
                .GetMethods()
                .Single(m => m.Name == action &&
                             m.GetCustomAttribute<HttpPostAttribute>() != null);

            Assert.Equal(
                ReskRateLimiting.SignIn,
                post.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
        }


        [Fact]
        public void Users_api_can_be_created_by_dependency_injection()
        {
            using var app = new ReskAppFactory();
            using IServiceScope scope = app.Services.CreateScope();

            var controller = ActivatorUtilities.CreateInstance<ApiUserController>(scope.ServiceProvider);

            Assert.NotNull(controller);
        }
    }
}
