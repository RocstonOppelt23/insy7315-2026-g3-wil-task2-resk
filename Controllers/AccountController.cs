using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

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

        // =========================================================
        // ROLE-BASED REDIRECT
        // =========================================================

        private async Task<IActionResult> RedirectUserByRole(
            IdentityUser user)
        {
            // ADMIN
            if (await _userManager.IsInRoleAsync(user, "Admin") ||
                await _userManager.IsInRoleAsync(user, "Administrator"))
            {
                return RedirectToAction(
                    "Index",
                    "Splash",
                    new
                    {
                        destination = "admin"
                    });
            }

            // PRODUCER
            if (await _userManager.IsInRoleAsync(user, "Producer"))
            {
                return RedirectToAction(
                    "Index",
                    "Splash",
                    new
                    {
                        destination = "producer"
                    });
            }

            // REVIEWER
            if (await _userManager.IsInRoleAsync(user, "Reviewer"))
            {
                return RedirectToAction(
                    "Index",
                    "Splash",
                    new
                    {
                        destination = "reviewer"
                    });
            }

            // INVALID / UNKNOWN ROLE
            await _signInManager.SignOutAsync();

            TempData["LoginError"] =
                "Your account does not have a valid system role.";

            return RedirectToAction(
                "Index",
                "Splash",
                new
                {
                    destination = "login"
                });
        }


        // =========================================================
        // LOGIN GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> Login()
        {
            /*
             * When the normal application splash redirects
             * to Login, clear any old authentication session.
             */

            if (User.Identity?.IsAuthenticated == true)
            {
                await _signInManager.SignOutAsync();
            }

            return View();
        }


        // =========================================================
        // LOGIN POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string email,
            string password,
            bool rememberMe)
        {
            ViewBag.Email = email;
            ViewBag.RememberMe = rememberMe;

            // -----------------------------------------------------
            // EMAIL VALIDATION
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(
                    "email",
                    "Please enter your email address.");
            }
            else
            {
                email = email.Trim();

                var emailValidator =
                    new EmailAddressAttribute();

                if (!emailValidator.IsValid(email))
                {
                    ModelState.AddModelError(
                        "email",
                        "Please enter a valid email address.");
                }
            }

            // -----------------------------------------------------
            // PASSWORD VALIDATION
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(
                    "password",
                    "Please enter your password.");
            }

            if (!ModelState.IsValid)
            {
                return View();
            }

            // -----------------------------------------------------
            // FIND IDENTITY USER
            // -----------------------------------------------------

            var user =
                await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Incorrect email address or password.");

                return View();
            }

            // -----------------------------------------------------
            // SIGN USER IN
            // -----------------------------------------------------

            var result =
                await _signInManager.PasswordSignInAsync(
                    user,
                    password,
                    rememberMe,
                    lockoutOnFailure: true);

            // -----------------------------------------------------
            // SUCCESS
            // -----------------------------------------------------

            if (result.Succeeded)
            {
                /*
                 * Admin:
                 *
                 * Login
                 *   ↓
                 * Splash
                 *   ↓
                 * Admin Dashboard
                 *
                 *
                 * Producer:
                 *
                 * Login
                 *   ↓
                 * Splash
                 *   ↓
                 * Producer Dashboard
                 */

                return await RedirectUserByRole(user);
            }

            // -----------------------------------------------------
            // LOCKED OUT
            // -----------------------------------------------------

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Your account has been temporarily locked because of too many failed login attempts. Please try again later.");

                return View();
            }

            // -----------------------------------------------------
            // NOT ALLOWED
            // -----------------------------------------------------

            if (result.IsNotAllowed)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Your account is not currently allowed to sign in.");

                return View();
            }

            // -----------------------------------------------------
            // TWO FACTOR
            // -----------------------------------------------------

            if (result.RequiresTwoFactor)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Two-factor authentication is required for this account.");

                return View();
            }

            // -----------------------------------------------------
            // INCORRECT LOGIN
            // -----------------------------------------------------

            ModelState.AddModelError(
                string.Empty,
                "Incorrect email address or password.");

            return View();
        }


        // =========================================================
        // REGISTER GET
        // =========================================================

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }


        // =========================================================
        // REGISTER POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            string email,
            string password,
            string confirmPassword)
        {
            ViewBag.Email = email;

            // -----------------------------------------------------
            // EMAIL VALIDATION
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(
                    "email",
                    "Please enter your email address.");
            }
            else
            {
                email = email.Trim();

                var emailValidator =
                    new EmailAddressAttribute();

                if (!emailValidator.IsValid(email))
                {
                    ModelState.AddModelError(
                        "email",
                        "Please enter a valid email address.");
                }
            }

            // -----------------------------------------------------
            // PASSWORD VALIDATION
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(
                    "password",
                    "Please enter a password.");
            }
            else
            {
                if (password.Length < 8)
                {
                    ModelState.AddModelError(
                        "password",
                        "Password must contain at least 8 characters.");
                }

                if (!password.Any(char.IsUpper))
                {
                    ModelState.AddModelError(
                        "password",
                        "Password must contain at least one uppercase letter.");
                }

                if (!password.Any(char.IsLower))
                {
                    ModelState.AddModelError(
                        "password",
                        "Password must contain at least one lowercase letter.");
                }

                if (!password.Any(char.IsDigit))
                {
                    ModelState.AddModelError(
                        "password",
                        "Password must contain at least one number.");
                }

                if (!password.Any(
                    character =>
                        !char.IsLetterOrDigit(character)))
                {
                    ModelState.AddModelError(
                        "password",
                        "Password must contain at least one special character.");
                }
            }

            // -----------------------------------------------------
            // CONFIRM PASSWORD
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(confirmPassword))
            {
                ModelState.AddModelError(
                    "confirmPassword",
                    "Please confirm your password.");
            }
            else if (
                !string.IsNullOrWhiteSpace(password) &&
                password != confirmPassword)
            {
                ModelState.AddModelError(
                    "confirmPassword",
                    "Passwords do not match.");
            }

            if (!ModelState.IsValid)
            {
                return View();
            }

            // -----------------------------------------------------
            // CHECK FOR EXISTING ACCOUNT
            // -----------------------------------------------------

            var existingUser =
                await _userManager.FindByEmailAsync(email);

            if (existingUser != null)
            {
                ModelState.AddModelError(
                    "email",
                    "An account with this email address already exists.");

                return View();
            }

            // -----------------------------------------------------
            // CREATE IDENTITY ACCOUNT
            // -----------------------------------------------------

            var user =
                new IdentityUser
                {
                    UserName = email,
                    Email = email
                };

            var createResult =
                await _userManager.CreateAsync(
                    user,
                    password);

            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                return View();
            }

            // =====================================================
            // EVERY PUBLIC SIGN-UP IS A PRODUCER
            // =====================================================

            var roleResult =
                await _userManager.AddToRoleAsync(
                    user,
                    "Producer");

            if (!roleResult.Succeeded)
            {
                /*
                 * If Producer role assignment fails,
                 * delete the incomplete Identity account.
                 */

                await _userManager.DeleteAsync(user);

                foreach (var error in roleResult.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                ModelState.AddModelError(
                    string.Empty,
                    "The account could not be assigned the Producer role.");

                return View();
            }

            // =====================================================
            // AUTOMATICALLY LOGIN NEW PRODUCER
            // =====================================================

            /*
             * IMPORTANT:
             *
             * We do NOT redirect back to Login here.
             *
             * The newly registered Producer is immediately
             * authenticated.
             */

            await _signInManager.SignInAsync(
                user,
                isPersistent: false);

            // =====================================================
            // SIGN UP -> SPLASH -> PRODUCER DASHBOARD
            // =====================================================

            return RedirectToAction(
                "Index",
                "Splash",
                new
                {
                    destination = "producer"
                });
        }


        // =========================================================
        // FORGOT PASSWORD GET
        // =========================================================

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }


        // =========================================================
        // FORGOT PASSWORD POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(
            string email)
        {
            ViewBag.Email = email;

            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(
                    "email",
                    "Please enter your email address.");

                return View();
            }

            email = email.Trim();

            var emailValidator =
                new EmailAddressAttribute();

            if (!emailValidator.IsValid(email))
            {
                ModelState.AddModelError(
                    "email",
                    "Please enter a valid email address.");

                return View();
            }

            var user =
                await _userManager.FindByEmailAsync(email);

            if (user != null)
            {
                _ =
                    await _userManager
                        .GeneratePasswordResetTokenAsync(user);
            }

            TempData["ForgotPasswordMessage"] =
                "If an account exists for that email address, password reset instructions will be sent to it.";

            return RedirectToAction(
                nameof(ForgotPasswordConfirmation));
        }


        // =========================================================
        // FORGOT PASSWORD CONFIRMATION
        // =========================================================

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }


        // =========================================================
        // LOGOUT
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            /*
             * Logout
             *   ↓
             * Splash
             *   ↓
             * Login
             */

            return RedirectToAction(
                "Index",
                "Splash",
                new
                {
                    destination = "login"
                });
        }


        // =========================================================
        // ACCESS DENIED
        // =========================================================

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}