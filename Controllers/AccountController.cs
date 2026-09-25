using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace RESK.WIL.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;

        public AccountController(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }


        // ==========================================
        // LOGIN - GET
        // ==========================================

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity != null &&
                User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Proposal");
            }

            return View();
        }


        // ==========================================
        // LOGIN - POST
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string email,
            string password,
            bool rememberMe)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Please enter your email address.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Please enter your password.";
                return View();
            }

            // Find user by email
            var user =
                await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                ViewBag.Error =
                    "Incorrect email address or password.";

                return View();
            }

            // Check password and sign in
            var result =
                await _signInManager.PasswordSignInAsync(
                    user,
                    password,
                    rememberMe,
                    lockoutOnFailure: true);

            if (result.Succeeded)
            {
                return RedirectToAction(
                    "Index",
                    "Proposal");
            }

            if (result.IsLockedOut)
            {
                ViewBag.Error =
                    "Your account has been temporarily locked because of too many failed login attempts.";

                return View();
            }

            if (result.IsNotAllowed)
            {
                ViewBag.Error =
                    "Your account is not currently allowed to sign in.";

                return View();
            }

            ViewBag.Error =
                "Incorrect email address or password.";

            return View();
        }


        // ==========================================
        // REGISTER - GET
        // ==========================================

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity != null &&
                User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Proposal");
            }

            return View();
        }


        // ==========================================
        // REGISTER - POST
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            string email,
            string password,
            string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error =
                    "Please enter your email address.";

                return View();
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error =
                    "Please enter a password.";

                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error =
                    "Passwords do not match.";

                return View();
            }

            // Check if account already exists
            var existingUser =
                await _userManager.FindByEmailAsync(email);

            if (existingUser != null)
            {
                ViewBag.Error =
                    "An account with this email address already exists.";

                return View();
            }

            // Create real Identity user
            var user = new IdentityUser
            {
                UserName = email,
                Email = email
            };

            var result =
                await _userManager.CreateAsync(
                    user,
                    password);

            if (result.Succeeded)
            {
                // New public accounts become Producers
                var roleResult =
                    await _userManager.AddToRoleAsync(
                        user,
                        "Producer");

                if (!roleResult.Succeeded)
                {
                    // Remove the account if role assignment fails
                    await _userManager.DeleteAsync(user);

                    ViewBag.Error =
                        "The account could not be assigned a Producer role.";

                    return View();
                }

                TempData["Success"] =
                    "Account created successfully. You can now sign in.";

                return RedirectToAction(
                    "Login",
                    "Account");
            }

            ViewBag.Errors =
                result.Errors
                    .Select(error => error.Description)
                    .ToList();

            return View();
        }


        // ==========================================
        // LOGOUT
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            return RedirectToAction(
                "Login",
                "Account");
        }


        // ==========================================
        // ACCESS DENIED
        // ==========================================

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}