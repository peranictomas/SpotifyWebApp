using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
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

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var profileEndpoint = "https://api.spotify.com/v1/me";

        var profileResponse = await client.GetAsync(profileEndpoint);

        if (profileResponse.IsSuccessStatusCode)
        {
            var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            var contentProfile = await profileResponse.Content.ReadAsStringAsync();
            var spotifyUser = JsonConvert.DeserializeObject<SpotifyUser>(contentProfile, settings);

            var userProfile = new SpotifyUserProfile
            {
                Email = spotifyUser.Email,
                DisplayName = spotifyUser.DisplayName,
                UserProfileImage = spotifyUser.Images.FirstOrDefault()?.Url,
                ID = spotifyUser.ID,
                Country = spotifyUser?.Country,
                AccountType = spotifyUser.AccountType,
                Followers = spotifyUser.Followers.Total
            };
            return View(userProfile);
        }
        else
        {
            return View("Error");
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
            ViewBag.SpotifyUserProfileImage = spotifyUser.Images.FirstOrDefault()?.Url;
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
        return View(); // Redirect to home or another page after logout
    }

    public async Task<IActionResult> Artists()
    {
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

    public async Task<IActionResult> Genres()
    {
        return View();
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
    public async Task<JsonResult> GetArtistTrackData(string timeFrame)
    {
        var data = await GetDataBasedOnTimeFrameAsync(timeFrame);
        return Json(data);
    }

    private async Task<object> GetDataBasedOnTimeFrameAsync(string timeFrame)
    {
        var accessToken = await EnsureValidAccessTokenAsync();

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
                        return new { message = "Data retrieved successfully", artists = spotifyArtist.Items };

                    }
                    else
                    {
                        return new { message = "No Artists Found" };

                    }

                }
                return new { message = "Data for 4 weeks"};
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
                        return new { message = "Data retrieved successfully", artists = spotifyArtist.Items };
                    }
                    else
                    {
                        return new { message = "No Artists Found" };
                    }

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
                        return new { message = "Data retrieved successfully", artists = spotifyArtist.Items };
                    }
                    else
                    {
                        return new { message = "No Artists Found" };
                    }
                }
                return new { message = "Data for 1 year" };
            case "4weeksTrack":
                var trackEndpoints = "https://api.spotify.com/v1/me/top/tracks?time_range=short_term&limit=50";
                var trackResponse = await client.GetAsync(trackEndpoints);

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
                                track.Album.FirstImageUrl = track.Album.Image.FirstOrDefault()?.Url;
                                track.Album.stringArtists = songFeatures;
                                track.Album.Uri = track.Album.Uri;
                            }
                        }
                        return new { message = "Data retrieved successfully", tracks = spotifyTrack.Items };
                    }
                    else
                    {
                        return new { message = "No Tracks Found" };

                    }
                }
                return new { message = "Data for 4 weeks track" };

            case "6monthsTrack":
                var trackEndpointsMedium = "https://api.spotify.com/v1/me/top/tracks?time_range=medium_term&limit=50";
                var trackResponseMedium = await client.GetAsync(trackEndpointsMedium);

                if (trackResponseMedium.IsSuccessStatusCode)
                {
                    var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
                    var contentTrack = await trackResponseMedium.Content.ReadAsStringAsync();
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
                                track.Album.FirstImageUrl = track.Album.Image.FirstOrDefault()?.Url;
                                track.Album.stringArtists = songFeatures;
                                track.Album.Uri = track.Album.Uri;
                            }
                        }
                        return new { message = "Data retrieved successfully", tracks = spotifyTrack.Items };
                    }
                    else
                    {
                        return new { message = "No Tracks Found" };

                    }
                }
                return new { message = "Data for 6 months track" };
            case "1yearTrack":
                var trackEndpointsLong = "https://api.spotify.com/v1/me/top/tracks?time_range=long_term&limit=50";
                var trackResponseLong = await client.GetAsync(trackEndpointsLong);

                if (trackResponseLong.IsSuccessStatusCode)
                {
                    var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
                    var contentTrack = await trackResponseLong.Content.ReadAsStringAsync();
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
                                track.Album.FirstImageUrl = track.Album.Image.FirstOrDefault()?.Url;
                                track.Album.stringArtists = songFeatures;
                                track.Album.Uri = track.Album.Uri;
                            }
                        }
                        return new { message = "Data retrieved successfully", tracks = spotifyTrack.Items };
                    }
                    else
                    {
                        return new { message = "No Tracks Found" };

                    }
                }
                return new { message = "Data for 1 year track" };

            default:
                return new { message = "Invalid time frame" };
        }
       

    }


  
}
