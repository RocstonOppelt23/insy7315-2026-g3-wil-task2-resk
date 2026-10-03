using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;
using RESK.WIL.Models;
using RESK.WIL.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<User>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();

bool bypassServiceLogic = builder.Configuration.GetValue<bool>(
    "ApiTesting:BypassServiceLogic");

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApiAccess", policy =>
    {
        if (bypassServiceLogic)
            policy.RequireAssertion(_ => true);
        else
            policy.RequireAuthenticatedUser();
    });

    options.AddPolicy("ManageProposals", policy =>
    {
        if (bypassServiceLogic)
            policy.RequireAssertion(_ => true);
        else
            policy.RequireAuthenticatedUser();
    });

    options.AddPolicy("ManageUsers", policy =>
    {
        if (bypassServiceLogic)
            policy.RequireAssertion(_ => true);
        else
            policy.RequireAuthenticatedUser();
    });

    options.AddPolicy("ManageSystemSettings", policy =>
    {
        if (bypassServiceLogic)
            policy.RequireAssertion(_ => true);
        else
            policy.RequireAuthenticatedUser();
    });
});
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<AccessControlService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
