using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace RESK.WIL.Controllers
{
    /*
     * POST /Producer/Logout
     *
     * Used by the "Log out" button in the profile menu on every
     * producer page. Signs the producer out and returns to the
     * login page.
     */
    [Authorize]
    [Route("Producer")]
    public class ProducerAccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;

        public ProducerAccountController(SignInManager<IdentityUser> signInManager)
        {
            _signInManager = signInManager;
        }

        [HttpPost("Logout")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            // Forget any unfinished proposal kept in Session.
            HttpContext.Session.Clear();

            // Back to the login page.
            return Redirect("/Account/Login");
        }
    }
}