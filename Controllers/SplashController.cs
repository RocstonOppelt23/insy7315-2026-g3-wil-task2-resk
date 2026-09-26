using Microsoft.AspNetCore.Mvc;

namespace RESK.WIL.Controllers
{
    public class SplashController : Controller
    {
        [HttpGet]
        public IActionResult Index(
            string? destination = null)
        {
            string destinationUrl;

            string requestedDestination =
                destination?
                    .Trim()
                    .ToLowerInvariant()
                ?? "login";


            // =====================================================
            // DETERMINE WHERE SPLASH SHOULD REDIRECT
            // =====================================================

            switch (requestedDestination)
            {
                // =================================================
                // ADMIN
                //
                // Login -> Splash -> Admin Dashboard
                // =================================================

                case "admin":

                    destinationUrl =
                        Url.Action(
                            "Index",
                            "Admin")
                        ?? "/Admin";

                    break;


                // =================================================
                // PRODUCER
                //
                // Login -> Splash -> Producer Dashboard
                //
                // OR
                //
                // Sign Up -> Splash -> Producer Dashboard
                // =================================================

                case "producer":

                    destinationUrl =
                        Url.Action(
                            "Index",
                            "Producer")
                        ?? "/Producer";

                    break;


                // =================================================
                // REVIEWER
                // =================================================

                case "reviewer":

                    destinationUrl =
                        Url.Action(
                            "Index",
                            "Home")
                        ?? "/Home";

                    break;


                // =================================================
                // LOGIN
                //
                // Logout -> Splash -> Login
                // =================================================

                case "login":

                    destinationUrl =
                        Url.Action(
                            "Login",
                            "Account")
                        ?? "/Account/Login";

                    break;


                // =================================================
                // DEFAULT
                //
                // Initial application startup:
                //
                // Splash -> Login
                // =================================================

                default:

                    destinationUrl =
                        Url.Action(
                            "Login",
                            "Account")
                        ?? "/Account/Login";

                    break;
            }


            // =====================================================
            // SEND DESTINATION TO SPLASH VIEW
            // =====================================================

            ViewBag.DestinationUrl =
                destinationUrl;

            return View();
        }
    }
}