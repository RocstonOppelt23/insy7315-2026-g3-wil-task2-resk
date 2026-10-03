using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RESK.WIL.Data;
using RESK.WIL.Models;

namespace RESK.WIL.Security
{
    // Identity users have a string/GUID id, but the domain Users table uses an int id.
    // This adds an "AppUserId" claim at login so controllers can find the domain user.
    // Inherits the ROLE-AWARE base factory so Producer/Reviewer/Admin role claims are kept.
    public class AppUserClaimsPrincipalFactory
        : UserClaimsPrincipalFactory<IdentityUser, IdentityRole>
    {
        public const string AppUserIdClaimType = "AppUserId";
        private readonly ApplicationDbContext _db;

        public AppUserClaimsPrincipalFactory(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> optionsAccessor,
            ApplicationDbContext db)
            : base(userManager, roleManager, optionsAccessor)
        {
            _db = db;
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(IdentityUser identityUser)
        {
            var identity = await base.GenerateClaimsAsync(identityUser);

            var email = identityUser.Email ?? identityUser.UserName;
            if (string.IsNullOrWhiteSpace(email))
                return identity;

            var domainUser = await _db.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.Email == email);

            if (domainUser is null)
            {
                domainUser = new User
                {
                    Email = email,
                    Name = identityUser.UserName ?? email,
                    LastName = string.Empty,
                    AccountStatus = UserAccountStatus.Pending
                };
                _db.Users.Add(domainUser);
                await _db.SaveChangesAsync();
            }

            identity.AddClaim(new Claim(AppUserIdClaimType, domainUser.Id.ToString()));
            return identity;
        }
    }
}
