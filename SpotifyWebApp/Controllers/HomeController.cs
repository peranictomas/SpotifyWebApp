using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SpotifyWebApp.Models;
using System.Net.Http.Headers;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HomeController(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
    }
    public IActionResult Index()
    {
        
        return View();
    }

    [Authorize]
    public async Task<IActionResult> SignIn()
    {
        var accessToken = await HttpContext.GetTokenAsync("access_token");

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync("https://api.spotify.com/v1/me");
        if (response.IsSuccessStatusCode)
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore // Ignore null values during deserialization
            };
            var content = await response.Content.ReadAsStringAsync();
            var spotifyUser = JsonConvert.DeserializeObject<SpotifyUser>(content, settings);
            

            ViewBag.Email = spotifyUser.Email; // Store the email in ViewBag for use in the view
            ViewBag.DisplayName = spotifyUser.DisplayName;
            ViewBag.SpotifyUserProfileImage = spotifyUser.Images.FirstOrDefault()?.Url;
            ViewBag.ID = spotifyUser.ID;
            ViewBag.Country = spotifyUser?.Country;
            ViewBag.AccountType = spotifyUser.AccountType;
            ViewBag.Followers = spotifyUser.Followers.Total;

        }

        return View("Index");
    }
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return RedirectToAction("Index"); // Redirect to home or another page after logout
    }
}
