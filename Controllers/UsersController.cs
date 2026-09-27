using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RESK.WIL.Data;
using RESK.WIL.Models;

namespace RESK.WIL.Controllers
{
    [Authorize]
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // USERS LIST
        // /Users
        // /Users/Index
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            string? search,
            string? role,
            string? status)
        {
            IQueryable<User> query = _context.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .AsNoTracking();

            // -----------------------------
            // Search
            // -----------------------------
            if (!string.IsNullOrWhiteSpace(search))
            {
                string searchTerm = search.Trim();

                query = query.Where(u =>
                    u.Name.Contains(searchTerm) ||
                    u.LastName.Contains(searchTerm) ||
                    u.Email.Contains(searchTerm) ||
                    u.Organisation.Contains(searchTerm));
            }

            // -----------------------------
            // Role filter
            // -----------------------------
            if (!string.IsNullOrWhiteSpace(role))
            {
                query = query.Where(u =>
                    u.UserRoles.Any(ur => ur.Role.Title == role));
            }

            // -----------------------------
            // Status filter
            // -----------------------------
            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<UserAccountStatus>(
                    status,
                    true,
                    out var accountStatus))
            {
                query = query.Where(u =>
                    u.AccountStatus == accountStatus);
            }

            // -----------------------------
            // Users displayed in table
            // -----------------------------
            List<User> users = await query
                .OrderByDescending(u => u.CreatedAtUtc)
                .ToListAsync();

            // -----------------------------
            // Dashboard statistics
            // -----------------------------
            ViewBag.TotalUsers =
                await _context.Users.CountAsync();

            ViewBag.PendingUsers =
                await _context.Users.CountAsync(u =>
                    u.AccountStatus == UserAccountStatus.Pending);

            ViewBag.ActiveUsers =
                await _context.Users.CountAsync(u =>
                    u.AccountStatus == UserAccountStatus.Active);

            ViewBag.SuspendedUsers =
                await _context.Users.CountAsync(u =>
                    u.AccountStatus == UserAccountStatus.Disabled);

            // -----------------------------
            // Roles for filter dropdown
            // -----------------------------
            ViewBag.Roles = await _context.Roles
                .AsNoTracking()
                .Where(r => r.IsActive)
                .OrderBy(r => r.Title)
                .Select(r => r.Title)
                .ToListAsync();

            // Preserve filters
            ViewBag.Search = search;
            ViewBag.SelectedRole = role;
            ViewBag.SelectedStatus = status;

            return View(users);
        }
    }
}