using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SpotifyAPI.Web;
using SpotifyWebApp.Models;
using System.Net.Http.Headers;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HomeController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [Authorize]
    public async Task<IActionResult> Index()
    {
        var accessToken = await EnsureValidAccessTokenAsync();
        if (string.IsNullOrEmpty(accessToken))
        {
            return RedirectToAction("Logout");
        }
        else
        {
            return View();
        }
    }

    public async Task<IActionResult> Profile()
    {
        var accessToken = await HttpContext.GetTokenAsync("access_token");

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var profileEndpoint = "https://api.spotify.com/v1/me";

        var profileTask = client.GetAsync(profileEndpoint);

        await Task.WhenAll(profileTask);

        var profileResponse = await profileTask;

        if (profileResponse.IsSuccessStatusCode)
        {
            var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            var contentProfile = await profileResponse.Content.ReadAsStringAsync();
            var spotifyUser = JsonConvert.DeserializeObject<SpotifyUser>(contentProfile, settings);

            ViewBag.Email = spotifyUser.Email; // Store the email in ViewBag for use in the view
            ViewBag.DisplayName = spotifyUser.DisplayName;
            ViewBag.SpotifyUserProfileImage = spotifyUser.Images.LastOrDefault()?.Url;
            ViewBag.ID = spotifyUser.ID;
            ViewBag.Country = spotifyUser?.Country;
            ViewBag.AccountType = spotifyUser.AccountType;
            ViewBag.Followers = spotifyUser.Followers.Total;

        }
        return View();
    }

    public async Task<IActionResult> Logout()
    {
        // Clear the session
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return View(); 
    }

    public Task<IActionResult> Artists()
    {
        return Task.FromResult<IActionResult>(View());
    }

    public Task<IActionResult> Tracks()
    {
        return Task.FromResult<IActionResult>(View());
    }

    public Task<IActionResult> Genres()
    {
        return Task.FromResult<IActionResult>(View());
    }


    private async Task<string> EnsureValidAccessTokenAsync()
    {
        var accessToken = await HttpContext.GetTokenAsync("access_token");
        var refreshToken = await HttpContext.GetTokenAsync("refresh_token");
        var expiresAt = await HttpContext.GetTokenAsync("expires_at");

        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(expiresAt))
        {
            throw new Exception("Tokens are missing in the session.");
        }

        if (DateTime.TryParse(expiresAt, out var expiryTime) && DateTime.UtcNow >= expiryTime)
        {
            // Token has expired, refresh it

            using (var client = _httpClientFactory.CreateClient())
            {
                var parameters = new Dictionary<string, string>
                {
                    { "grant_type", "refresh_token" },
                    { "refresh_token", refreshToken },
                    { "client_id", "9d8836eff00a4ac49132fd687fa862a7" },
                    { "client_secret", "0da6a9e4992a417ca8e9f81d77708cbe" }
                };

                var content = new FormUrlEncodedContent(parameters);
                var response = await client.PostAsync("https://accounts.spotify.com/api/token", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseString);
                    // Log the new tokens


                    // Use the existing refresh token if it's not included in the response
                    var newAccessToken = tokenResponse.AccessToken;
                    var newRefreshToken = tokenResponse.RefreshToken ?? refreshToken;
                    var newExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn).ToString();


                    // Save the new tokens
                    var authInfo = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    authInfo.Properties.UpdateTokenValue("access_token", newAccessToken);
                    authInfo.Properties.UpdateTokenValue("refresh_token", newRefreshToken);
                    //authInfo.Properties.UpdateTokenValue("expires_at", DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn).ToString());
                    authInfo.Properties.UpdateTokenValue("expires_at", newExpiresAt);

                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, authInfo.Principal, authInfo.Properties);

                    return tokenResponse.AccessToken;
                }
                else
                {
                    throw new Exception("Could not refresh access token.");
                }
            }
        }

        return accessToken;
    }

    [HttpGet]
    public async Task<JsonResult> GetArtistTrackData(string timeRange, string type)
    {
        //timeRange : short_term medium_term long_term
        //type : artists tracks
        var data = await GetDataBasedOnTimeFrameAsync(timeRange, type);
        return Json(data);
    }

    private async Task<object> GetDataBasedOnTimeFrameAsync(string timeRange, string type)
    {
        var accessToken = await EnsureValidAccessTokenAsync();
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var endPoint = $"https://api.spotify.com/v1/me/top/{type}?time_range={timeRange}&limit=50";
        var endPointResponse = await client.GetAsync(endPoint);

        var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        if (!endPointResponse.IsSuccessStatusCode)
        {
            return new { message = "Failed to retrieve data from Spotify API." };
        }

        var content = await endPointResponse.Content.ReadAsStringAsync();
        return type switch
        {
            "artists" => await ProcessArtistsResponseAsync(content, settings),
            "tracks" => await ProcessTracksResponseAsync(content, settings),
            _ => new { message = "Invalid time frame or type." }
        };
    }

    private Task<object> ProcessArtistsResponseAsync(string content, JsonSerializerSettings settings)
    {
        var spotifyArtist = JsonConvert.DeserializeObject<SpotifyTopArtists>(content, settings);
        if (spotifyArtist == null)
        {
            return Task.FromResult<object>(new { message = "No Artists Found" });
        }

        foreach (var artist in spotifyArtist.Items)
        {
            if (artist.Image?.Count > 0)
            {
                artist.FirstImageUrl = artist.Image.Last()?.Url;
            }
        }

        return Task.FromResult<object>(new { message = "Data retrieved successfully", artists = spotifyArtist.Items });
    }

    private Task<object> ProcessTracksResponseAsync(string content, JsonSerializerSettings settings)
    {
        var spotifyTrack = JsonConvert.DeserializeObject<SpotifyTopTracks>(content, settings);
        if (spotifyTrack == null)
        {
            return Task.FromResult<object>(new { message = "No Tracks Found" });
        }

        foreach (var track in spotifyTrack.Items)
        {
            var songFeatures = string.Join(", ", track.Album.Artists.Select((artist, index) =>
                index == track.Album.Artists.Count - 1 ? $"{artist.Name}." : artist.Name));

            if (track.Album?.Image.Count > 0)
            {
                track.Album.FirstImageUrl = track.Album.Image.FirstOrDefault()?.Url;
                track.Album.stringArtists = songFeatures;
            }
        }

        return Task.FromResult<object>(new { message = "Data retrieved successfully", tracks = spotifyTrack.Items });
    }

}
