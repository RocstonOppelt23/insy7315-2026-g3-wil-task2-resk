using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;
using RESK.WIL.Models;

namespace RESK.WIL.Controllers.API
{
    /// <summary>User records (Admin only).</summary>
    [Route("api/users")]
    [Authorize(Policy = "ManageUsers")]
    public class ApiUserController : ApiControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IPasswordHasher<User> _passwordHasher;

        public ApiUserController(
            ApplicationDbContext db,
            IPasswordHasher<User> passwordHasher)
        {
            _db = db;
            _passwordHasher = passwordHasher;
        }

        /// <summary>Lists all users.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<UserResponse>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<UserResponse>>> GetUsers(
            CancellationToken cancellationToken)
        {
            var users = await _db.Users
                .AsNoTracking()
                .Select(u => new UserResponse(
                    u.Id, u.Name, u.LastName, u.Email,
                    u.AccountStatus, u.CreatedAtUtc, u.UpdatedAtUtc))
                .ToListAsync(cancellationToken);

            return Ok(users);
        }

        /// <summary>Gets one user.</summary>
        /// <param name="id">The user id.</param>
        /// <response code="404">No user has that id.</response>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserResponse>> GetUserById(
            int id,
            CancellationToken cancellationToken)
        {
            var user = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new UserResponse(
                    u.Id, u.Name, u.LastName, u.Email,
                    u.AccountStatus, u.CreatedAtUtc, u.UpdatedAtUtc))
                .SingleOrDefaultAsync(cancellationToken);

            return user is null ? NotFoundProblem($"User {id} was not found.") : Ok(user);
        }

        /// <summary>Creates a user (status starts as Pending).</summary>
        /// <response code="201">The new user.</response>
        /// <response code="400">A field is missing or invalid.</response>
        /// <response code="409">An account with this email already exists.</response>
        [HttpPost("create")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<UserResponse>> CreateUser(
            [FromBody] CreateUserRequest request,
            CancellationToken cancellationToken)
        {
            string email = request.Email.Trim();

            if (await _db.Users.AnyAsync(
                    u => u.Email == email, cancellationToken))
            {
                return ConflictProblem("An account with this email already exists.");
            }

            var now = DateTime.UtcNow;
            var user = new User
            {
                Name = request.Name.Trim(),
                LastName = request.LastName.Trim(),
                Email = email,
                Phone = request.Phone.Trim(),
                PhysicalAddress = request.PhysicalAddress.Trim(),
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

        /// <summary>Updates a user's details.</summary>
        /// <param name="id">The user id.</param>
        /// <response code="400">A field is missing or invalid.</response>
        /// <response code="404">No user has that id.</response>
        /// <response code="409">Another account already uses this email.</response>
        [HttpPut("{id:int}/update")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<UserResponse>> UpdateUser(
            int id,
            [FromBody] UpdateUserRequest request,
            CancellationToken cancellationToken)
        {
            var user = await _db.Users.FindAsync(
                new object[] { id }, cancellationToken);

            if (user is null)
                return NotFoundProblem($"User {id} was not found.");

            string email = request.Email.Trim();

            if (await _db.Users.AnyAsync(
                    u => u.Email == email && u.Id != id,
                    cancellationToken))
            {
                return ConflictProblem("An account with this email already exists.");
            }

            user.Name = request.Name.Trim();
            user.LastName = request.LastName.Trim();
            user.Email = email;
            user.Phone = request.Phone.Trim();
            user.PhysicalAddress = request.PhysicalAddress.Trim();
            user.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(ToResponse(user));
        }

        /// <summary>Changes a user's account status.</summary>
        /// <param name="id">The user id.</param>
        /// <response code="400">The status is missing or not a valid value.</response>
        /// <response code="404">No user has that id.</response>
        [HttpPatch("{id:int}/change/status")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserResponse>> ChangeStatus(
            int id,
            [FromBody] ChangeUserStatusRequest request,
            CancellationToken cancellationToken)
        {
            if (!request.AccountStatus.HasValue ||
                !Enum.IsDefined(request.AccountStatus.Value))
            {
                return BadRequestProblem("Select a valid account status.");
            }

            var user = await _db.Users.FindAsync(
                new object[] { id }, cancellationToken);

            if (user is null)
                return NotFoundProblem($"User {id} was not found.");

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
                user.Email,
                user.AccountStatus,
                user.CreatedAtUtc,
                user.UpdatedAtUtc);
    }

    public record UserResponse(
        int Id,
        string Name,
        string LastName,
        string Email,
        UserAccountStatus AccountStatus,
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

        [Required, MinLength(8)]
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
    }

    public class ChangeUserStatusRequest
    {
        [Required]
        public UserAccountStatus? AccountStatus { get; set; }
    }
}
