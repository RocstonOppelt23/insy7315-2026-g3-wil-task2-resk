using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace RESK.WIL.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;


        // =========================================================
        // CONSTRUCTOR
        // =========================================================

        public AccountController(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }


        // =========================================================
        // LOGIN - GET
        // =========================================================

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity != null &&
                User.Identity.IsAuthenticated)
            {
                return RedirectToAction(
                    "Index",
                    "Proposal");
            }

            return View();
        }


        // =========================================================
        // LOGIN - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string email,
            string password,
            bool rememberMe)
        {
            // Preserve entered email and Remember Me state
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


            // -----------------------------------------------------
            // STOP IF VALIDATION FAILED
            // -----------------------------------------------------

            if (!ModelState.IsValid)
            {
                return View();
            }


            // -----------------------------------------------------
            // FIND USER
            // -----------------------------------------------------

            var user =
                await _userManager.FindByEmailAsync(email);


            // Do not reveal whether the email exists.
            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Incorrect email address or password.");

                return View();
            }


            // -----------------------------------------------------
            // ATTEMPT LOGIN
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
                return RedirectToAction(
                    "Index",
                    "Proposal");
            }


            // -----------------------------------------------------
            // LOCKED ACCOUNT
            // -----------------------------------------------------

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Your account has been temporarily locked because of too many failed login attempts. Please try again later.");

                return View();
            }


            // -----------------------------------------------------
            // SIGN IN NOT ALLOWED
            // -----------------------------------------------------

            if (result.IsNotAllowed)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Your account is not currently allowed to sign in.");

                return View();
            }


            // -----------------------------------------------------
            // TWO FACTOR AUTHENTICATION
            // -----------------------------------------------------

            if (result.RequiresTwoFactor)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Two-factor authentication is required for this account.");

                return View();
            }


            // -----------------------------------------------------
            // INVALID CREDENTIALS
            // -----------------------------------------------------

            ModelState.AddModelError(
                string.Empty,
                "Incorrect email address or password.");

            return View();
        }


        // =========================================================
        // REGISTER - GET
        // =========================================================

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity != null &&
                User.Identity.IsAuthenticated)
            {
                return RedirectToAction(
                    "Index",
                    "Proposal");
            }

            return View();
        }


        // =========================================================
        // REGISTER - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            string email,
            string password,
            string confirmPassword)
        {
            // Preserve entered email
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
            // PASSWORD REQUIRED
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(
                    "password",
                    "Please enter a password.");
            }
            else
            {
                // -------------------------------------------------
                // MINIMUM LENGTH
                // -------------------------------------------------

                if (password.Length < 8)
                {
                    ModelState.AddModelError(
                        "password",
                        "Password must contain at least 8 characters.");
                }


                // -------------------------------------------------
                // UPPERCASE
                // -------------------------------------------------

                if (!password.Any(char.IsUpper))
                {
                    ModelState.AddModelError(
                        "password",
                        "Password must contain at least one uppercase letter.");
                }


                // -------------------------------------------------
                // LOWERCASE
                // -------------------------------------------------

                if (!password.Any(char.IsLower))
                {
                    ModelState.AddModelError(
                        "password",
                        "Password must contain at least one lowercase letter.");
                }


                // -------------------------------------------------
                // NUMBER
                // -------------------------------------------------

                if (!password.Any(char.IsDigit))
                {
                    ModelState.AddModelError(
                        "password",
                        "Password must contain at least one number.");
                }


                // -------------------------------------------------
                // SPECIAL CHARACTER
                // -------------------------------------------------

                if (!password.Any(character =>
                        !char.IsLetterOrDigit(character)))
                {
                    ModelState.AddModelError(
                        "password",
                        "Password must contain at least one special character.");
                }
            }


            // -----------------------------------------------------
            // CONFIRM PASSWORD REQUIRED
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(confirmPassword))
            {
                ModelState.AddModelError(
                    "confirmPassword",
                    "Please confirm your password.");
            }


            // -----------------------------------------------------
            // PASSWORDS MUST MATCH
            // -----------------------------------------------------

            if (!string.IsNullOrWhiteSpace(password) &&
                !string.IsNullOrWhiteSpace(confirmPassword) &&
                password != confirmPassword)
            {
                ModelState.AddModelError(
                    "confirmPassword",
                    "Passwords do not match.");
            }


            // -----------------------------------------------------
            // STOP IF BASIC VALIDATION FAILED
            // -----------------------------------------------------

            if (!ModelState.IsValid)
            {
                return View();
            }


            // -----------------------------------------------------
            // CHECK EXISTING ACCOUNT
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
            // CREATE IDENTITY USER
            // -----------------------------------------------------

            var user =
                new IdentityUser
                {
                    UserName = email,
                    Email = email
                };


            var result =
                await _userManager.CreateAsync(
                    user,
                    password);


            // -----------------------------------------------------
            // ACCOUNT CREATED
            // -----------------------------------------------------

            if (result.Succeeded)
            {
                // Every public registration becomes a Producer.
                var roleResult =
                    await _userManager.AddToRoleAsync(
                        user,
                        "Producer");


                // -------------------------------------------------
                // ROLE ASSIGNMENT FAILED
                // -------------------------------------------------

                if (!roleResult.Succeeded)
                {
                    // Roll back the user account so that we don't
                    // leave an account without the required role.
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


                // -------------------------------------------------
                // SUCCESS
                // -------------------------------------------------

                TempData["SuccessMessage"] =
                    "Account created successfully. You can now sign in.";


                return RedirectToAction(
                    "Login",
                    "Account");
            }


            // -----------------------------------------------------
            // IDENTITY VALIDATION ERRORS
            // -----------------------------------------------------

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    string.Empty,
                    error.Description);
            }


            return View();
        }


        // =========================================================
        // FORGOT PASSWORD - GET
        // =========================================================

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            if (User.Identity != null &&
                User.Identity.IsAuthenticated)
            {
                return RedirectToAction(
                    "Index",
                    "Proposal");
            }


            return View();
        }


        // =========================================================
        // FORGOT PASSWORD - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(
            string email)
        {
            ViewBag.Email = email;


            // -----------------------------------------------------
            // EMAIL REQUIRED
            // -----------------------------------------------------

            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(
                    "email",
                    "Please enter your email address.");

                return View();
            }


            email = email.Trim();


            // -----------------------------------------------------
            // EMAIL FORMAT
            // -----------------------------------------------------

            var emailValidator =
                new EmailAddressAttribute();


            if (!emailValidator.IsValid(email))
            {
                ModelState.AddModelError(
                    "email",
                    "Please enter a valid email address.");

                return View();
            }


            // -----------------------------------------------------
            // FIND ACCOUNT
            // -----------------------------------------------------

            var user =
                await _userManager.FindByEmailAsync(email);


            /*
             * IMPORTANT:
             *
             * We intentionally return the same message whether
             * or not the account exists.
             *
             * This prevents attackers from using this page to
             * discover registered email addresses.
             */


            if (user != null)
            {
                /*
                 * Generate a secure ASP.NET Identity password
                 * reset token.
                 *
                 * Later, when email delivery is configured,
                 * this token will be placed inside the reset
                 * password link sent to the user.
                 */

                var token =
                    await _userManager
                        .GeneratePasswordResetTokenAsync(user);


                /*
                 * DO NOT display the token in the browser.
                 * DO NOT put it into TempData.
                 * DO NOT log it in production.
                 *
                 * The next step will be connecting this token
                 * to the ResetPassword page/email workflow.
                 */
            }


            // -----------------------------------------------------
            // GENERIC SUCCESS MESSAGE
            // -----------------------------------------------------

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


            return RedirectToAction(
                "Login",
                "Account");
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