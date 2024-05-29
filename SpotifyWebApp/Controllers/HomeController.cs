using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SpotifyAPI.Web;
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

    private async Task<string> RefreshAccessTokenAsync(string refreshToken)
    {
        using (var client = new HttpClient())
        {
            var parameters = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken },
                { "client_id", "your_client_id" },
                { "client_secret", "your_client_secret" }
            };

            var content = new FormUrlEncodedContent(parameters);
            var response = await client.PostAsync("https://your-authorization-server.com/oauth/token", content);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseString);
                HttpContext.Session.SetString("AccessToken", tokenResponse.AccessToken);
                HttpContext.Session.SetString("RefreshToken", tokenResponse.RefreshToken);
                HttpContext.Session.SetString("AccessTokenExpiresAt", DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn).ToString());

                return tokenResponse.AccessToken;
            }
            else
            {
                throw new Exception("Could not refresh the access token.");
            }
        }
    }

    private bool IsAccessTokenExpired()
    {
        var expiresAt = HttpContext.Session.GetString("AccessTokenExpiresAt");
        if (string.IsNullOrEmpty(expiresAt))
        {
            return true;
        }

        return DateTime.UtcNow >= DateTime.Parse(expiresAt);
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
            ViewBag.SpotifyUserProfileImage = spotifyUser.Images.FirstOrDefault()?.Url;
            ViewBag.ID = spotifyUser.ID;
            ViewBag.Country = spotifyUser?.Country;
            ViewBag.AccountType = spotifyUser.AccountType;
            ViewBag.Followers = spotifyUser.Followers.Total;

        }
        return View();
    }

    public async Task<IActionResult> Tracks()
    {
        var accessToken = await HttpContext.GetTokenAsync("access_token");

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var trackEndpoints = "https://api.spotify.com/v1/me/top/tracks?limit=50";

        var trackTask = client.GetAsync(trackEndpoints);

        await Task.WhenAll(trackTask);

        var trackResponse = await trackTask;
        if (trackResponse.IsSuccessStatusCode)
        {
            var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            var contentTrack = await trackResponse.Content.ReadAsStringAsync();
            var spotifyTrack = JsonConvert.DeserializeObject<SpotifyTopTracks>(contentTrack, settings);


            if (spotifyTrack != null)
            {
                foreach (var track in spotifyTrack.Items)
                {
                    var songFeatures = "";
                    var totalCount = track.Album.Artists.Count;
                    var currentIndex = 0;
                    foreach (var artist in track.Album.Artists)
                    {
                        if (currentIndex == 0)
                        {
                            songFeatures += artist.Name;
                        }
                        if (currentIndex > 0 && currentIndex != totalCount - 1)
                        {
                            songFeatures += ", " + artist.Name;
                        }
                        if (currentIndex == totalCount - 1)
                        {
                            songFeatures += ", " + artist.Name + ".";
                        }
                        currentIndex++;
                    }
                    if (track.Album != null && track.Album.Image.Count > 0)
                    {
                        //Get the last image from the list and assign it back to the Image property

                        //track.Album = new List<SpotifyImage> { track.Album.Image.Last() };
                        track.Album.FirstImageUrl = track.Album.Image.LastOrDefault()?.Url;
                        track.Album.stringArtists = songFeatures;

                    }

                }
            }
            else
            {
                // Handle the case when spotifyArtists is null
            }

            ViewBag.Tracks = spotifyTrack.Items;


        }

        return View();
    }

    private async Task<object> GetDataBasedOnTimeFrameAsync(string timeFrame)
    {
        /*
        var accessToken = await HttpContext.GetTokenAsync("access_token");

        
        */

        timeFrame = "4weeks";

        string accessToken = HttpContext.Session.GetString("AccessToken");
        string refreshToken = HttpContext.Session.GetString("RefreshToken");

        if (IsAccessTokenExpired())
        {
            accessToken = await RefreshAccessTokenAsync(refreshToken);
        }

        var client = _httpClientFactory.CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        switch (timeFrame)
        {
            case "4weeks":
                var artistShortTermEndpoint = "https://api.spotify.com/v1/me/top/artists?time_range=short_term&limit=50";
                var artistResponseShort = await client.GetAsync(artistShortTermEndpoint);

                if (artistResponseShort.IsSuccessStatusCode)
                {
                    var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
                    var contentArtist = await artistResponseShort.Content.ReadAsStringAsync();
                    var spotifyArtist = JsonConvert.DeserializeObject<SpotifyTopArtists>(contentArtist, settings);

                    if (spotifyArtist != null)
                    {
                        foreach (var artist in spotifyArtist.Items)
                        {
                            if (artist.Image != null && artist.Image.Count > 0)
                            {
                                // Get the last image from the list and assign it back to the Image property
                                artist.Image = new List<SpotifyImage> { artist.Image.Last() };
                                artist.FirstImageUrl = artist.Image.FirstOrDefault()?.Url;
                            }
                        }
                    }
                    else
                    {
                        // Handle the case when spotifyArtists is null
                    }

                    ViewBag.Artists = spotifyArtist.Items;


                }
                return new { message = "Data for 4 weeks" };
            case "6months":
                var artistMediumTermEndpoint = "https://api.spotify.com/v1/me/top/artists?time_range=medium_term&limit=50";

                var artistResponseMedium = await client.GetAsync(artistMediumTermEndpoint);

                if (artistResponseMedium.IsSuccessStatusCode)
                {
                    var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
                    var contentArtist = await artistResponseMedium.Content.ReadAsStringAsync();
                    var spotifyArtist = JsonConvert.DeserializeObject<SpotifyTopArtists>(contentArtist, settings);

                    if (spotifyArtist != null)
                    {
                        foreach (var artist in spotifyArtist.Items)
                        {
                            if (artist.Image != null && artist.Image.Count > 0)
                            {
                                // Get the last image from the list and assign it back to the Image property
                                artist.Image = new List<SpotifyImage> { artist.Image.Last() };
                                artist.FirstImageUrl = artist.Image.FirstOrDefault()?.Url;
                            }
                        }
                    }
                    else
                    {
                        // Handle the case when spotifyArtists is null
                    }

                    ViewBag.Artists = spotifyArtist.Items;


                }
                return new { message = "Data for 6 months" };
            case "1year":
                var artistLongTermEndpoint = "https://api.spotify.com/v1/me/top/artists?time_range=long_term&limit=50";
                var artistResponseLong = await client.GetAsync(artistLongTermEndpoint);

                if (artistResponseLong.IsSuccessStatusCode)
                {
                    var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
                    var contentArtist = await artistResponseLong.Content.ReadAsStringAsync();
                    var spotifyArtist = JsonConvert.DeserializeObject<SpotifyTopArtists>(contentArtist, settings);

                    if (spotifyArtist != null)
                    {
                        foreach (var artist in spotifyArtist.Items)
                        {
                            if (artist.Image != null && artist.Image.Count > 0)
                            {
                                // Get the last image from the list and assign it back to the Image property
                                artist.Image = new List<SpotifyImage> { artist.Image.Last() };
                                artist.FirstImageUrl = artist.Image.FirstOrDefault()?.Url;
                            }
                        }
                    }
                    else
                    {
                        // Handle the case when spotifyArtists is null
                    }

                    ViewBag.Artists = spotifyArtist.Items;


                }
                return new { message = "Data for 1 year" };
            default:
                return new { message = "Invalid time frame" };
        }
       

    }

    [HttpGet]
    public JsonResult GetArtistsData(string timeFrame)
    {
        var data = GetDataBasedOnTimeFrameAsync(timeFrame);
        return Json(data);
    }

    public async Task<IActionResult> Artists()
    {
        /*
        var accessToken = await HttpContext.GetTokenAsync("access_token");

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var artistEndpoint = "https://api.spotify.com/v1/me/top/artists?limit=50";

        var artistShortTermEndpoint = "https://api.spotify.com/v1/me/top/artists?time_range=short_term&limit=50";
        var artistMediumTermEndpoint = "https://api.spotify.com/v1/me/top/artists?time_range=medium_term&limit=50";
        var artistLongTermEndpoint = "https://api.spotify.com/v1/me/top/artists?time_range=long_term&limit=50";

        var artistTask = client.GetAsync(artistEndpoint);

        await Task.WhenAll(artistTask);

        var artistResponse = await artistTask;

        if (artistResponse.IsSuccessStatusCode)
        {
            var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            var contentArtist = await artistResponse.Content.ReadAsStringAsync();
            var spotifyArtist = JsonConvert.DeserializeObject<SpotifyTopArtists>(contentArtist, settings);

            if (spotifyArtist != null)
            {
                foreach (var artist in spotifyArtist.Items)
                {
                    if (artist.Image != null && artist.Image.Count > 0)
                    {
                        // Get the last image from the list and assign it back to the Image property
                        artist.Image = new List<SpotifyImage> { artist.Image.Last() };
                        artist.FirstImageUrl = artist.Image.FirstOrDefault()?.Url;
                    }
                }
            }
            else
            {
                // Handle the case when spotifyArtists is null
            }

            ViewBag.Artists = spotifyArtist.Items;


        }
        */
        return View();
    }

    [Authorize]
    public async Task<IActionResult> Index()
    {
        var accessToken = await HttpContext.GetTokenAsync("access_token");

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var profileEndpoint = "https://api.spotify.com/v1/me";
        var artistEndpoint = "https://api.spotify.com/v1/me/top/artists?limit=50";
        var trackEndpoints = "https://api.spotify.com/v1/me/top/tracks?limit=50";

        var profileTask = client.GetAsync(profileEndpoint);
        var artistTask = client.GetAsync(artistEndpoint);
        var trackTask = client.GetAsync(trackEndpoints);

        await Task.WhenAll(profileTask, artistTask, trackTask);

        var profileResponse = await profileTask;
        var artistResponse = await artistTask;
        var trackResponse = await trackTask;

        if (profileResponse.IsSuccessStatusCode && artistResponse.IsSuccessStatusCode && trackResponse.IsSuccessStatusCode)
        {
            var settings = new JsonSerializerSettings{ NullValueHandling = NullValueHandling.Ignore};
            var contentProfile = await profileResponse.Content.ReadAsStringAsync();
            var contentArtist =  await artistResponse.Content.ReadAsStringAsync();
            var contentTrack = await trackResponse.Content.ReadAsStringAsync();
            var spotifyUser = JsonConvert.DeserializeObject<SpotifyUser>(contentProfile, settings);
            var spotifyArtist = JsonConvert.DeserializeObject<SpotifyTopArtists>(contentArtist, settings);
            var spotifyTrack = JsonConvert.DeserializeObject<SpotifyTopTracks>(contentTrack, settings);


            if (spotifyArtist != null)
            {
                foreach (var artist in spotifyArtist.Items)
                {
                    if (artist.Image != null && artist.Image.Count > 0)
                    {
                        // Get the last image from the list and assign it back to the Image property
                        artist.Image = new List<SpotifyImage> { artist.Image.Last() };
                        artist.FirstImageUrl = artist.Image.FirstOrDefault()?.Url;
                    }
                }
            }
            else
            {
                // Handle the case when spotifyArtists is null
            }

            if (spotifyTrack != null)
            {
                foreach (var track in spotifyTrack.Items)
                {
                    var songFeatures = "";
                    var totalCount = track.Album.Artists.Count;
                    var currentIndex = 0;
                    foreach (var artist in track.Album.Artists)
                    {
                        if(currentIndex == 0)
                        {
                            songFeatures += artist.Name;
                        }
                        if(currentIndex > 0 && currentIndex != totalCount - 1)
                        {
                            songFeatures += ", " + artist.Name;
                        }
                        if (currentIndex == totalCount - 1)
                        {
                            songFeatures += ", " + artist.Name + ".";
                        }
                        currentIndex++;
                    }
                    if (track.Album != null && track.Album.Image.Count > 0)
                    {
                        //Get the last image from the list and assign it back to the Image property
                        
                        //track.Album = new List<SpotifyImage> { track.Album.Image.Last() };
                        track.Album.FirstImageUrl = track.Album.Image.LastOrDefault()?.Url;
                        track.Album.stringArtists = songFeatures;

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

            ViewBag.Tracks = spotifyTrack.Items;
            



        }

        return View();
    }
    public async Task<IActionResult> Genres()
    {
        return View(); 
    }
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return RedirectToAction("Index"); // Redirect to home or another page after logout
    }
}
