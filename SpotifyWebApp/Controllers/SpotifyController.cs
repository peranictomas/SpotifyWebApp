using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace SpotifyWebApp.Controllers
{
    public class SpotifyController : Controller
    {
        public IActionResult SignIn()
        {
            return Challenge(new AuthenticationProperties
            {
                RedirectUri = "/",
            }, "OpenIdConnect-Spotify");
        }

        public async Task<IActionResult> Callback()
        {
            var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Use authResult to get user information, e.g., authResult.Principal
            // ...

            return RedirectToAction("Index", "Home");
        }
    }
}
