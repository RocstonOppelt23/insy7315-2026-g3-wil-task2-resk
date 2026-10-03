using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.Controllers.API
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Policy = "ManageUsers")]
    public class ApiUserController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly AccessControlService _access;

        public ApiUserController(
            ApplicationDbContext db,
            IPasswordHasher<User> passwordHasher,
            AccessControlService access)
        {
            _db = db;
            _passwordHasher = passwordHasher;
            _access = access;
        }

        // GET /api/users
        [HttpGet]
        public async Task<ActionResult<List<UserResponse>>> GetUsers(
            CancellationToken cancellationToken)
        {
            var currentUser = await _access.GetCurrentUserAsync(
                User,
                cancellationToken);

            if (currentUser is null)
                return Unauthorized();

            if (!await _access.CanUseUsersAsync(currentUser, cancellationToken))
                return Forbid();

            // Apply the custom role scope before projecting users.
            // Without this, a user with UsersScope.Own or SelectedRoles could
            // still list every account by calling the API directly.
            // When ApiTesting:BypassServiceLogic is true, the service returns
            // the original query so Swagger/Postman can test the endpoint shape.
            var scopedUsers = _access.ApplyUserScope(
                _db.Users.AsNoTracking().Include(u => u.Role),
                currentUser);

            var users = await scopedUsers
                .Select(u => new UserResponse(
                    u.Id, u.Name, u.LastName, u.Email ?? string.Empty,
                    u.AccountStatus, u.RoleId, u.Role != null ? u.Role.Title : null,
                    u.CreatedAtUtc, u.UpdatedAtUtc))
                .ToListAsync(cancellationToken);

            return Ok(users);
        }

        // GET /api/users/7
        [HttpGet("{id:int}")]
        public async Task<ActionResult<UserResponse>> GetUserById(
            int id,
            CancellationToken cancellationToken)
        {
            var currentUser = await _access.GetCurrentUserAsync(
                User,
                cancellationToken);

            if (currentUser is null)
                return Unauthorized();

            if (!await _access.CanUseUsersAsync(currentUser, cancellationToken))
                return Forbid();

            // Detail lookup uses the same server-side scope as the list endpoint.
            var user = await _access.ApplyUserScope(
                    _db.Users.AsNoTracking().Include(u => u.Role),
                    currentUser)
                .Where(u => u.Id == id)
                .Select(u => new UserResponse(
                    u.Id, u.Name, u.LastName, u.Email ?? string.Empty,
                    u.AccountStatus, u.RoleId, u.Role != null ? u.Role.Title : null,
                    u.CreatedAtUtc, u.UpdatedAtUtc))
                .SingleOrDefaultAsync(cancellationToken);

            return user is null ? NotFound() : Ok(user);
        }

        // POST /api/users/create
        [HttpPost("create")]
        public async Task<ActionResult<UserResponse>> CreateUser(
            [FromBody] CreateUserRequest request,
            CancellationToken cancellationToken)
        {
            var currentUser = await _access.GetCurrentUserAsync(
                User,
                cancellationToken);

            if (currentUser is null)
                return Unauthorized();

            if (!await _access.CanManageUsersAsync(currentUser, cancellationToken))
                return Forbid();

            string email = request.Email.Trim();

            if (await _db.Users.AnyAsync(
                    u => u.Email == email, cancellationToken))
            {
                return Conflict("An account with this email already exists.");
            }

            var now = DateTime.UtcNow;
            int? roleId = NormalizeRoleId(request.RoleId);
            string? roleError = await ValidateRole(roleId, cancellationToken);

            if (roleError is not null)
                return BadRequest(roleError);

            var user = new User
            {
                Name = request.Name.Trim(),
                LastName = request.LastName.Trim(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Phone = request.Phone.Trim(),
                PhysicalAddress = request.PhysicalAddress.Trim(),
                RoleId = roleId,
                AccountStatus = UserAccountStatus.Pending,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                PasswordLastChanged = now
            };

            user.PasswordHash =
                _passwordHasher.HashPassword(user, request.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(
                nameof(GetUserById),
                new { id = user.Id },
                ToResponse(user));
        }

        // PUT /api/users/7/update
        [HttpPut("{id:int}/update")]
        public async Task<ActionResult<UserResponse>> UpdateUser(
            int id,
            [FromBody] UpdateUserRequest request,
            CancellationToken cancellationToken)
        {
            var currentUser = await _access.GetCurrentUserAsync(
                User,
                cancellationToken);

            if (currentUser is null)
                return Unauthorized();

            if (!await _access.CanManageUsersAsync(currentUser, cancellationToken))
                return Forbid();

            var user = await _db.Users.FindAsync(
                new object[] { id }, cancellationToken);

            if (user is null)
                return NotFound();

            string email = request.Email.Trim();
            int? roleId = NormalizeRoleId(request.RoleId);
            string? roleError = await ValidateRole(roleId, cancellationToken);

            if (roleError is not null)
                return BadRequest(roleError);

            if (await _db.Users.AnyAsync(
                    u => u.Email == email && u.Id != id,
                    cancellationToken))
            {
                return Conflict("An account with this email already exists.");
            }

            user.Name = request.Name.Trim();
            user.LastName = request.LastName.Trim();
            user.Email = email;
            user.NormalizedEmail = email.ToUpperInvariant();
            user.UserName = email;
            user.NormalizedUserName = email.ToUpperInvariant();
            user.Phone = request.Phone.Trim();
            user.PhysicalAddress = request.PhysicalAddress.Trim();
            user.RoleId = roleId;
            user.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(ToResponse(user));
        }

        // PATCH /api/users/7/change/status
        [HttpPatch("{id:int}/change/status")]
        public async Task<ActionResult<UserResponse>> ChangeStatus(
            int id,
            [FromBody] ChangeUserStatusRequest request,
            CancellationToken cancellationToken)
        {
            var currentUser = await _access.GetCurrentUserAsync(
                User,
                cancellationToken);

            if (currentUser is null)
                return Unauthorized();

            if (!await _access.CanManageUsersAsync(currentUser, cancellationToken))
                return Forbid();

            if (!request.AccountStatus.HasValue ||
                !Enum.IsDefined(request.AccountStatus.Value))
            {
                return BadRequest("Select a valid account status.");
            }

            var user = await _db.Users.FindAsync(
                new object[] { id }, cancellationToken);

            if (user is null)
                return NotFound();

            user.AccountStatus = request.AccountStatus.Value;
            user.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(ToResponse(user));
        }

        private static UserResponse ToResponse(User user) =>
            new(
                user.Id,
                user.Name,
                user.LastName,
                user.Email ?? string.Empty,
                user.AccountStatus,
                user.RoleId,
                user.Role?.Title,
                user.CreatedAtUtc,
                user.UpdatedAtUtc);

        private int? NormalizeRoleId(int? roleId)
        {
            // Swagger's generated example uses 0 for nullable integers.
            // In local API testing mode, treat 0 as no role so sample requests
            // can be tested without manually editing every body first.
            if (_access.BypassServiceLogic && roleId == 0)
                return null;

            return roleId;
        }

        private async Task<string?> ValidateRole(
            int? roleId,
            CancellationToken cancellationToken)
        {
            if (!roleId.HasValue)
                return null;

            bool exists = await _db.AccessRoles.AnyAsync(
                r => r.Id == roleId.Value && r.IsActive,
                cancellationToken);

            return exists ? null : "Select a valid active role.";
        }
    }

    public record UserResponse(
        int Id,
        string Name,
        string LastName,
        string Email,
        UserAccountStatus AccountStatus,
        int? RoleId,
        string? RoleTitle,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);

    public class CreateUserRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string PhysicalAddress { get; set; } = string.Empty;

        public int? RoleId { get; set; }

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class UpdateUserRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string PhysicalAddress { get; set; } = string.Empty;

        public int? RoleId { get; set; }
    }

    public class ChangeUserStatusRequest
    {
        [Required]
        public UserAccountStatus? AccountStatus { get; set; }
    }
    //----------Constructor----------//
}
