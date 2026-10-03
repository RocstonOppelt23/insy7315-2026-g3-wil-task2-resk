using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;
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
});


// =====================================================
// MVC
// =====================================================

builder.Services.AddControllersWithViews();


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