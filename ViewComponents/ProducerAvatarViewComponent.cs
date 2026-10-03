using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using RESK.WIL.Models;
using RESK.WIL.Services;

namespace RESK.WIL.ViewComponents
{
    /*
     * Shows the signed-in producer's photo (or initials) and
     * name in the sidebar of every producer page.
     *
     *   @await Component.InvokeAsync("ProducerAvatar", new { part = "avatar" })
     *   @await Component.InvokeAsync("ProducerAvatar", new { part = "name" })
     */
    [ViewComponent(Name = "ProducerAvatar")]
    public class ProducerAvatarViewComponent : ViewComponent
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public ProducerAvatarViewComponent(
            UserManager<IdentityUser> userManager,
            IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _environment = environment;
        }


        public async Task<IViewComponentResult> InvokeAsync(string part)
        {
            IdentityUser? user =
                UserClaimsPrincipal == null
                    ? null
                    : await _userManager.GetUserAsync(UserClaimsPrincipal);

            ProducerProfile profile =
                user == null
                    ? new ProducerProfile()
                    : ProducerProfileStore.Load(
                        _environment.ContentRootPath,
                        user.Id);

            string name =
                ProducerProfileStore.DisplayName(profile, user);

            HtmlEncoder encoder = HtmlEncoder.Default;


            // Name only
            if (part == "name")
            {
                return new HtmlContentViewComponentResult(
                    new HtmlString(
                        "<span title=\"" + encoder.Encode(name) + "\">" +
                        encoder.Encode(name) +
                        "</span>"));
            }


            // Photo, if the producer uploaded one
            string? photoUrl =
                user == null
                    ? null
                    : ProducerProfileStore.PhotoUrl(
                        _environment.ContentRootPath,
                        user.Id);

            if (photoUrl != null)
            {
                return new HtmlContentViewComponentResult(
                    new HtmlString(
                        "<img src=\"" + encoder.Encode(photoUrl) + "\" alt=\"\" " +
                        "style=\"width:100%;height:100%;display:block;" +
                        "border-radius:50%;object-fit:cover;\" />"));
            }


            // Otherwise the initials
            return new HtmlContentViewComponentResult(
                new HtmlString(
                    encoder.Encode(ProducerProfileStore.Initials(name))));
        }
    }
}