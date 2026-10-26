using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RESK.WIL.Data;
using RESK.WIL.Models;
using RESK.WIL.Security;
using RESK.WIL.Services;

var builder = WebApplication.CreateBuilder(args);


// =====================================================
// DATABASE
// =====================================================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found.");

// The factory also registers ApplicationDbContext as a scoped
// service (used by Identity and the controllers). The static RESK
// stores use the factory directly - see Data/ReskDb.cs.
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();


// =====================================================
// ASP.NET CORE IDENTITY
// =====================================================

builder.Services
    .AddDefaultIdentity<IdentityUser>(options =>
    {
        // Email confirmation disabled for development/testing
        options.SignIn.RequireConfirmedAccount = false;


        // -------------------------------------------------
        // PASSWORD SECURITY
        // -------------------------------------------------

        options.Password.RequiredLength = 8;

        options.Password.RequireDigit = true;

        options.Password.RequireUppercase = true;

        options.Password.RequireLowercase = true;

        options.Password.RequireNonAlphanumeric = true;


        // -------------------------------------------------
        // LOCKOUT PROTECTION
        // -------------------------------------------------

        options.Lockout.MaxFailedAccessAttempts = 5;

        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);


        // -------------------------------------------------
        // UNIQUE EMAIL
        // -------------------------------------------------

        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddScoped<IUserClaimsPrincipalFactory<IdentityUser>, AppUserClaimsPrincipalFactory>();


// =====================================================
// COOKIE SECURITY
// =====================================================

builder.Services.ConfigureApplicationCookie(options =>
{
    // Prevent JavaScript from accessing authentication
    // cookies.

    options.Cookie.HttpOnly = true;


    // Only transmit authentication cookies over HTTPS.

    options.Cookie.SecurePolicy =
        CookieSecurePolicy.Always;


    // Authentication session expires after 30 minutes.

    options.ExpireTimeSpan =
        TimeSpan.FromMinutes(30);


    // Activity refreshes the authentication timeout.

    options.SlidingExpiration = true;


    // Custom login page.

    options.LoginPath =
        "/Account/Login";


    // Custom access denied page.

    options.AccessDeniedPath =
        "/Account/AccessDenied";


    // API calls get 401 / 403 instead of being redirected to
    // an HTML page (the body is filled in as problem details).

    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        }
        else
        {
            context.Response.Redirect(context.RedirectUri);
        }

        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
        }
        else
        {
            context.Response.Redirect(context.RedirectUri);
        }

        return Task.CompletedTask;
    };
});


// =====================================================
// API: ERRORS, RATE LIMITS, SWAGGER DOCS
// =====================================================

// One error format for the whole API: RFC 7807 problem details
// (see Controllers/API/ApiControllerBase.cs).
builder.Services.AddProblemDetails();

// 60 API requests / minute and 10 sign-in attempts / minute per IP.
builder.Services.AddReskRateLimiting(builder.Configuration);

// The users API hashes passwords for the domain User table.
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// OpenAPI document at /swagger/v1/swagger.json, UI at /swagger.
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "R.E.S.K Proposal Workflow API",
        Version = "v1",
        Description =
            "JSON API for Cape Town TV programme proposals. " +
            "Sign in through the website first - the API uses the same sign-in cookie. " +
            "Errors use RFC 7807 problem details. " +
            "Requests are rate limited per IP address (HTTP 429 with Retry-After)."
    });

    // Only the /api controllers, not the MVC pages.
    options.DocInclusionPredicate((_, api) =>
        api.RelativePath?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true);

    // Use the /// comments on the controllers as descriptions.
    string xmlFile = Path.Combine(
        AppContext.BaseDirectory,
        typeof(Program).Assembly.GetName().Name + ".xml");

    if (File.Exists(xmlFile))
    {
        options.IncludeXmlComments(xmlFile);
    }
});


// =====================================================
// HEALTH CHECKS + REQUEST LOGGING
// =====================================================

// GET /health reports whether the app can reach the database.
// The CI/CD pipeline calls it after every deployment.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>(
        name: "database",
        tags: new[] { "ready" });

// One log line per request (method, path, status code, time taken).
// On Azure these appear in App Service "Log stream".
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields =
        HttpLoggingFields.RequestMethod |
        HttpLoggingFields.RequestPath |
        HttpLoggingFields.ResponseStatusCode |
        HttpLoggingFields.Duration;

    options.CombineLogs = true;
});


// =====================================================
// MVC
// =====================================================

builder.Services.AddControllersWithViews();


// =====================================================
// FILE UPLOAD LIMITS
// =====================================================

/*
 * The Attachments step accepts up to three 20 MB files
 * (about 63 MB in total). Allow multipart forms up to
 * 70 MB. The Attachments POST action also sets its own
 * [RequestSizeLimit] to the same value.
 */

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit =
        70_000_000;
});


// =====================================================
// SESSION STORAGE
// =====================================================

/*
 * Proposal creation currently uses ASP.NET Core Session
 * to temporarily store the producer's progress between
 * the multi-step proposal screens.
 *
 * Examples:
 *
 * ProposalGuidelinesAccepted
 * ProducerDetails
 * ProgrammeDetails
 */

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    // Keep proposal session information for 30 minutes
    // after the user's last request.

    options.IdleTimeout =
        TimeSpan.FromMinutes(30);


    // Prevent JavaScript from reading the session cookie.

    options.Cookie.HttpOnly = true;


    // Allow the session cookie to be created because
    // the proposal workflow requires it.

    options.Cookie.IsEssential = true;


    // Give the RESK session cookie its own name.

    options.Cookie.Name =
        ".RESK.WIL.Session";


    // Match the security policy used by the
    // authentication cookie.

    options.Cookie.SecurePolicy =
        CookieSecurePolicy.Always;


    options.Cookie.SameSite =
        SameSiteMode.Lax;
});


// =====================================================
// BUILD APPLICATION
// =====================================================
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ManageProposals", policy =>
        policy.RequireRole("Producer", "Admin"));
    options.AddPolicy("ManageUsers", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ManageSystemSettings", policy => policy.RequireRole("Admin"));
});

builder.Services.AddSingleton<IBlobStorageService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var blobConnection = config.GetConnectionString("BlobStorage")
        ?? throw new InvalidOperationException("Connection string 'BlobStorage' not found.");
    var containerName = config["BlobStorage:ContainerName"] ?? "proposal-attachments";
    return new BlobStorageService(blobConnection, containerName);
});

var app = builder.Build();


// =====================================================
// DATABASE SETUP
// =====================================================

// Let the static RESK stores open database connections.
ReskDb.Configure(
    app.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>());

// Apply migrations and import any old App_Data JSON files.
await ReskDatabaseSetup.RunAsync(app);


// =====================================================
// ERROR HANDLING
// =====================================================

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    // Pages show the friendly error page; the API returns a
    // problem-details JSON body instead of HTML.

    app.UseWhen(
        context => context.Request.Path.StartsWithSegments("/api"),
        api => api.UseExceptionHandler());

    app.UseWhen(
        context => !context.Request.Path.StartsWithSegments("/api"),
        web => web.UseExceptionHandler("/Home/Error"));

    app.UseHsts();
}

// Empty API error responses (401, 403, 404 ...) get a
// problem-details body too.
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api"),
    api => api.UseStatusCodePages());


// =====================================================
// HTTPS
// =====================================================

app.UseHttpsRedirection();


// =====================================================
// STATIC FILES
// =====================================================

app.UseStaticFiles();


// =====================================================
// LOGGING + HEALTH ENDPOINT
// =====================================================

app.UseHttpLogging();

// Registered before session/auth so the health check is never
// redirected to a sign-in page. Returns 200 when healthy and
// 503 when the database can't be reached.
app.UseHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            totalMs = Math.Round(report.TotalDuration.TotalMilliseconds),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                ms = Math.Round(entry.Value.Duration.TotalMilliseconds)
            })
        }));
    }
});


// =====================================================
// ROUTING
// =====================================================

app.UseRouting();


// =====================================================
// RATE LIMITING + API DOCS
// =====================================================

app.UseRateLimiter();

if (app.Configuration.GetValue("Swagger:Enabled", true))
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "R.E.S.K API v1");
        options.DocumentTitle = "R.E.S.K API";
    });
}


// =====================================================
// SESSION
// =====================================================

/*
 * IMPORTANT:
 *
 * AddSession() above registers the Session services.
 *
 * UseSession() here adds Session to the HTTP request
 * pipeline.
 *
 * Without this line:
 *
 * HttpContext.Session
 *
 * throws:
 *
 * "Session has not been configured for this
 * application or request."
 */

app.UseSession();


// =====================================================
// AUTHENTICATION
// =====================================================

app.UseAuthentication();


// =====================================================
// AUTHORIZATION
// =====================================================

// Signs out suspended / banned users straight away.
app.UseMiddleware<RESK.WIL.Services.ReskAuditMiddleware>();
app.UseMiddleware<RESK.WIL.Services.ReskSettingsMiddleware>();
app.UseMiddleware<RESK.WIL.Services.ReskRestrictionMiddleware>();

app.UseAuthorization();


// =====================================================
// SECURITY HEADERS
// =====================================================

app.Use(async (context, next) =>
{
    context.Response.Headers.Append(
        "Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "object-src 'none';");

    await next();
});


// =====================================================
// ROUTES
// =====================================================

// Application starts on the Splash screen.

app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Splash}/{action=Index}/{id?}");


// =====================================================
// IDENTITY RAZOR PAGES
// =====================================================

// Keep ASP.NET Core Identity Razor Pages available.

app.MapRazorPages();


// =====================================================
// CREATE SYSTEM ROLES + DEVELOPMENT ADMIN
// =====================================================

using (var scope = app.Services.CreateScope())
{
    var roleManager =
        scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

    var userManager =
        scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();


    // =================================================
    // CREATE SYSTEM ROLES
    // =================================================

    string[] roles =
    {
        "Producer",
        "Reviewer",
        "Admin"
    };


    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var roleResult =
                await roleManager.CreateAsync(
                    new IdentityRole(role));


            if (!roleResult.Succeeded)
            {
                var errors =
                    string.Join(
                        ", ",
                        roleResult.Errors.Select(
                            error =>
                                error.Description));


                throw new Exception(
                    $"Failed to create role '{role}': {errors}");
            }
        }
    }


    // =================================================
    // DEVELOPMENT ADMIN ACCOUNT
    // =================================================

    /*
     * Admin credentials are read from configuration
     * (User Secrets in development), NOT from source code.
     *
     * Set them from the project folder with:
     *
     * dotnet user-secrets init
     * dotnet user-secrets set "SeedAdmin:Email" "admin@resk.co.za"
     * dotnet user-secrets set "SeedAdmin:Password" "<new strong password>"
     *
     * If the Admin account already exists with a different
     * password, the password is updated to the configured one.
     *
     * If the settings are missing, Admin setup is skipped.
     */

    string? adminEmail =
        app.Configuration["SeedAdmin:Email"];

    string? adminPassword =
        app.Configuration["SeedAdmin:Password"];


    if (string.IsNullOrWhiteSpace(adminEmail) ||
        string.IsNullOrWhiteSpace(adminPassword))
    {
        app.Logger.LogWarning(
            "SeedAdmin:Email or SeedAdmin:Password is not configured. " +
            "Skipping development Admin account setup. " +
            "Set them with 'dotnet user-secrets'.");
    }
    else
    {
        // =============================================
        // FIND EXISTING ADMIN
        // =============================================

        var adminUser =
            await userManager.FindByEmailAsync(
                adminEmail);


        // =============================================
        // CREATE ADMIN IF IT DOES NOT EXIST
        // =============================================

        if (adminUser == null)
        {
            adminUser =
                new IdentityUser
                {
                    UserName = adminEmail,

                    Email = adminEmail,

                    EmailConfirmed = true
                };


            var createAdminResult =
                await userManager.CreateAsync(
                    adminUser,
                    adminPassword);


            if (!createAdminResult.Succeeded)
            {
                var errors =
                    string.Join(
                        ", ",
                        createAdminResult.Errors.Select(
                            error =>
                                error.Description));


                throw new Exception(
                    $"Failed to create Admin account: {errors}");
            }
        }
        else if (!await userManager.CheckPasswordAsync(
                     adminUser,
                     adminPassword))
        {
            // =========================================
            // UPDATE ADMIN PASSWORD
            // =========================================

            // The old password was committed to source code,
            // so replace it with the configured one.

            var resetToken =
                await userManager.GeneratePasswordResetTokenAsync(
                    adminUser);

            var resetResult =
                await userManager.ResetPasswordAsync(
                    adminUser,
                    resetToken,
                    adminPassword);


            if (!resetResult.Succeeded)
            {
                var errors =
                    string.Join(
                        ", ",
                        resetResult.Errors.Select(
                            error =>
                                error.Description));


                throw new Exception(
                    $"Failed to update Admin password: {errors}");
            }
        }


        // =============================================
        // ENSURE ADMIN ROLE
        // =============================================

        if (!await userManager.IsInRoleAsync(
                adminUser,
                "Admin"))
        {
            var addAdminRoleResult =
                await userManager.AddToRoleAsync(
                    adminUser,
                    "Admin");


            if (!addAdminRoleResult.Succeeded)
            {
                var errors =
                    string.Join(
                        ", ",
                        addAdminRoleResult.Errors.Select(
                            error =>
                                error.Description));


                throw new Exception(
                    $"Failed to assign Admin role: {errors}");
            }
        }


        // =============================================
        // REMOVE PRODUCER ROLE FROM ADMIN
        // =============================================

        /*
         * If this email was previously registered as a
         * Producer, remove that role.
         *
         * This keeps the Admin account separate from
         * Producer accounts.
         */

        if (await userManager.IsInRoleAsync(
                adminUser,
                "Producer"))
        {
            var removeProducerResult =
                await userManager.RemoveFromRoleAsync(
                    adminUser,
                    "Producer");


            if (!removeProducerResult.Succeeded)
            {
                var errors =
                    string.Join(
                        ", ",
                        removeProducerResult.Errors.Select(
                            error =>
                                error.Description));


                throw new Exception(
                    $"Failed to remove Producer role from Admin: {errors}");
            }
        }
    }
}


// =====================================================
// RUN APPLICATION
// =====================================================

app.Run();

// Lets the integration tests start the app (WebApplicationFactory<Program>).
public partial class Program
{
}
