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

        //var response = await client.GetAsync("https://api.spotify.com/v1/me");

        var profileEndpoint = "https://api.spotify.com/v1/me";
        var artistEndpoint = "https://api.spotify.com/v1/me/top/artists?limit=5";

        var profileTask = client.GetAsync(profileEndpoint);
        var artistTask = client.GetAsync(artistEndpoint);

        await Task.WhenAll(profileTask, artistTask);

        var profileResponse = await profileTask;
        var artistResponse = await artistTask;

        if (profileResponse.IsSuccessStatusCode && artistResponse.IsSuccessStatusCode)
        {
            var settings = new JsonSerializerSettings{ NullValueHandling = NullValueHandling.Ignore};
            var contentProfile = await profileResponse.Content.ReadAsStringAsync();
            var contentArtist =  await artistResponse.Content.ReadAsStringAsync();
            var spotifyUser = JsonConvert.DeserializeObject<SpotifyUser>(contentProfile, settings);
            var spotifyArtist = JsonConvert.DeserializeObject<SpotifyTopArtists>(contentArtist, settings);

            if (spotifyArtist != null)
            {
                foreach (var artist in spotifyArtist.Items)
                {
                    if (artist.Image != null && artist.Image.Count > 0)
                    {
                        // Get the last image from the list and assign it back to the Image property
                        artist.Image = new List<SpotifyImage> { artist.Image.Last() };
                        artist.ImageTest = artist.Image.FirstOrDefault()?.Url;
                    }
                }
            }
            else
            {
                // Handle the case when spotifyArtists is null
            }


            ViewBag.Email = spotifyUser.Email; // Store the email in ViewBag for use in the view
            ViewBag.DisplayName = spotifyUser.DisplayName;
            ViewBag.SpotifyUserProfileImage = spotifyUser.Images.FirstOrDefault()?.Url;
            ViewBag.ID = spotifyUser.ID;
            ViewBag.Country = spotifyUser?.Country;
            ViewBag.AccountType = spotifyUser.AccountType;
            ViewBag.Followers = spotifyUser.Followers.Total;

            ViewBag.Artists = spotifyArtist.Items;
            



        }

        return View("Index");
    }
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return RedirectToAction("Index"); // Redirect to home or another page after logout
    }
}
