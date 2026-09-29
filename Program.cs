using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;

var builder = WebApplication.CreateBuilder(args);


// =====================================================
// DATABASE
// =====================================================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
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
});


// =====================================================
// MVC
// =====================================================

builder.Services.AddControllersWithViews();


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

var app = builder.Build();


// =====================================================
// ERROR HANDLING
// =====================================================

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();
}


// =====================================================
// HTTPS
// =====================================================

app.UseHttpsRedirection();


// =====================================================
// STATIC FILES
// =====================================================

app.UseStaticFiles();


// =====================================================
// ROUTING
// =====================================================

app.UseRouting();


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
     * This Admin account is created separately from
     * normal public registration.
     *
     * Public registration creates Producer accounts.
     *
     * This account receives ONLY the Admin role.
     *
     * IMPORTANT:
     * Move these credentials to User Secrets or another
     * secure configuration source before production.
     */

    const string adminEmail =
        "admin@resk.co.za";

    const string adminPassword =
        "R3SK!Admin#94_Vault$K7p";


    // =================================================
    // FIND EXISTING ADMIN
    // =================================================

    var adminUser =
        await userManager.FindByEmailAsync(
            adminEmail);


    // =================================================
    // CREATE ADMIN IF IT DOES NOT EXIST
    // =================================================

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


    // =================================================
    // ENSURE ADMIN ROLE
    // =================================================

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


    // =================================================
    // REMOVE PRODUCER ROLE FROM ADMIN
    // =================================================

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


// =====================================================
// RUN APPLICATION
// =====================================================

app.Run();