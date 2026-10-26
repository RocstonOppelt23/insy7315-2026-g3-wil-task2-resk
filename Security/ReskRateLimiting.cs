using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace RESK.WIL.Security
{
    /*
     * =========================================================
     * RATE LIMITING
     * =========================================================
     *
     * Limits how many requests one IP address can make, so the API
     * can't be flooded and passwords can't be brute-forced.
     *
     *   "api"     every /api endpoint     60 requests per minute
     *   "sign-in" login, register and     10 attempts per minute
     *             forgot-password POSTs
     *
     * Limits can be changed in appsettings.json ("RateLimiting").
     * When a limit is hit the request gets HTTP 429 with a
     * Retry-After header. API callers get the usual problem-details
     * JSON; browser forms get a short plain-text message.
     */
    public static class ReskRateLimiting
    {
        public const string Api = "api";
        public const string SignIn = "sign-in";


        public static IServiceCollection AddReskRateLimiting(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            int apiPerMinute = configuration.GetValue("RateLimiting:ApiPerMinute", 60);
            int signInPerMinute = configuration.GetValue("RateLimiting:SignInPerMinute", 10);

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddPolicy(Api, context =>
                    PerClient(context, Api, apiPerMinute));

                options.AddPolicy(SignIn, context =>
                    PerClient(context, SignIn, signInPerMinute));

                options.OnRejected = WriteRejectionAsync;
            });

            return services;
        }


        // One fixed one-minute window per client IP address.
        private static RateLimitPartition<string> PerClient(
            HttpContext context,
            string policy,
            int permitLimit)
        {
            string client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                policy + ":" + client,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
        }


        private static async ValueTask WriteRejectionAsync(
            OnRejectedContext rejected,
            CancellationToken cancellationToken)
        {
            HttpContext context = rejected.HttpContext;

            int retryAfterSeconds = 60;

            if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
            {
                retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            }

            context.Response.Headers.RetryAfter =
                retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

            if (context.Request.Path.StartsWithSegments("/api"))
            {
                var problems = context.RequestServices.GetRequiredService<IProblemDetailsService>();

                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail = $"Rate limit reached. Try again in {retryAfterSeconds} seconds."
                    }
                });

                return;
            }

            context.Response.ContentType = "text/plain; charset=utf-8";

            await context.Response.WriteAsync(
                $"Too many attempts. Please wait {retryAfterSeconds} seconds and try again.",
                cancellationToken);
        }
    }
}
