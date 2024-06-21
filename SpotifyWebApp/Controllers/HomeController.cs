using Azure.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SpotifyWebApp.Models;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    //private static readonly HttpClient client = new HttpClient();

    public HomeController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    //Checks whether the user is authenticated or not, if not dont display the homepage.
    [Authorize]
    public async Task<IActionResult> Index()
    {
        var accessToken = await EnsureValidAccessTokenAsync();
        if (string.IsNullOrEmpty(accessToken))
        {
            return Challenge(new AuthenticationProperties { RedirectUri = "/" }, "Spotify");
        }

        // Use the access token to call Spotify APIs
        var userProfile = await GetSpotifyUserProfileAsync(accessToken);

        return View();
    }

   
//public async Task<IActionResult> Index()
//{
//    var accessToken = await EnsureValidAccessTokenAsync();
//    if (string.IsNullOrEmpty(accessToken))
//    {
//        return RedirectToAction("Logout");
//    }
//    else
//    {
//        return View();
//    }
//}

//Displays the users profile statistics
public async Task<IActionResult> Profile()
    {
        var accessToken = await EnsureValidAccessTokenAsync();

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var profileResponse = await client.GetAsync("https://api.spotify.com/v1/me");

        if (profileResponse.IsSuccessStatusCode)
        {
            var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            var contentProfile = await profileResponse.Content.ReadAsStringAsync();
            var spotifyUser = JsonConvert.DeserializeObject<SpotifyUser>(contentProfile, settings);

            // Store the variables in ViewBags for use in the view
            ViewBag.Email = spotifyUser.Email; 
            ViewBag.DisplayName = spotifyUser.DisplayName;
            ViewBag.SpotifyUserProfileImage = spotifyUser.Images.LastOrDefault()?.Url;
            ViewBag.ID = spotifyUser.ID;
            ViewBag.Country = spotifyUser?.Country;
            ViewBag.AccountType = spotifyUser.AccountType;
            ViewBag.Followers = spotifyUser.Followers.Total;

        }
        return View();
    }

    public async Task<IActionResult> Privacy()
    {
        await CreatePlaylist();
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
                    //var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseString);
                    var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseString);

                    var newAccessToken = tokenResponse.AccessToken;
                    var newRefreshToken = tokenResponse.RefreshToken ?? refreshToken;
                    var newExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn).ToString();

                    var authInfo = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    authInfo.Properties.UpdateTokenValue("access_token", newAccessToken);
                    authInfo.Properties.UpdateTokenValue("refresh_token", newRefreshToken);
                    authInfo.Properties.UpdateTokenValue("expires_at", newExpiresAt);

                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, authInfo.Principal, authInfo.Properties);

                    return newAccessToken;
                }
                else
                {
                    throw new Exception("Could not refresh access token.");
                }
            }
        }

        return accessToken;
    }

    private async Task<object> GetSpotifyUserProfileAsync(string accessToken)
    {
        using (var client = _httpClientFactory.CreateClient())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await client.GetAsync("https://api.spotify.com/v1/me");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            //return JsonSerializer.Deserialize<object>(content);
            return JsonConvert.DeserializeObject<object>(content);
        }
    }


//This method will check if the users access token is still valid.
//This is achieved through the use of the refresh token as well as checking
//What time the token will expire at. If expired method will update the users access token,
//refresh token, and the new expiry date.
//private async Task<string> EnsureValidAccessTokenAsync()
//{
//    var accessToken = await HttpContext.GetTokenAsync("access_token");
//    var refreshToken = await HttpContext.GetTokenAsync("refresh_token");
//    var expiresAt = await HttpContext.GetTokenAsync("expires_at");

//    if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(expiresAt))
//    {
//        throw new Exception("Tokens are missing in the session.");
//    }

//    if (DateTime.TryParse(expiresAt, out var expiryTime) && DateTime.UtcNow >= expiryTime)
//    {
//        // Token has expired, refresh it
//        using (var client = _httpClientFactory.CreateClient())
//        {
//            var parameters = new Dictionary<string, string>
//            {
//                { "grant_type", "refresh_token" },
//                { "refresh_token", refreshToken },
//                { "client_id", "9d8836eff00a4ac49132fd687fa862a7" },
//                { "client_secret", "0da6a9e4992a417ca8e9f81d77708cbe" }
//            };

//            var content = new FormUrlEncodedContent(parameters);
//            var response = await client.PostAsync("https://accounts.spotify.com/api/token", content);

//            if (response.IsSuccessStatusCode)
//            {
//                var responseString = await response.Content.ReadAsStringAsync();
//                var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(responseString);

//                // Log the new tokens.
//                // Use the existing refresh token if it's not included in the response
//                var newAccessToken = tokenResponse.AccessToken;
//                var newRefreshToken = tokenResponse.RefreshToken ?? refreshToken;
//                var newExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn).ToString();

//                var authInfo = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
//                authInfo.Properties.UpdateTokenValue("access_token", newAccessToken);
//                authInfo.Properties.UpdateTokenValue("refresh_token", newRefreshToken);
//                authInfo.Properties.UpdateTokenValue("expires_at", newExpiresAt);

//                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, authInfo.Principal, authInfo.Properties);

//                return tokenResponse.AccessToken;
//            }
//            else
//            {
//                throw new Exception("Could not refresh access token.");
//            }
//        }
//    }

//    return accessToken;
//}

    //This method is used to update the tab views to the proper time period selected and for their respective views.
    //timeRange : short_term, medium_term, long_term
    //type : artists, tracks
    [HttpGet]
    public async Task<JsonResult> GetArtistTrackData(string timeRange, string type, bool genres = false, int genreAmount = 10)
    {
        var data = await GetDataBasedOnTimeFrameAsync(timeRange, type, genres, genreAmount);
        return Json(data);
    }

    //The method will verify the access token and will call either artists or tracks methods in the return statement to get
    //the desired information. It also creates the endpoint and sends the data contents to either method.
    //timeRange : short_term, medium_term, long_term
    //type : artists, tracks
    private async Task<object> GetDataBasedOnTimeFrameAsync(string timeRange, string type, bool genres, int genreAmount)
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
            "artists" => await ProcessArtistsResponseAsync(content, settings, genres, genreAmount),
            "tracks" => await ProcessTracksResponseAsync(content, settings),
            _ => new { message = "Invalid time frame or type." }
        };
    }

    //Converts the endpoint string information into my object for SpotifyTopArtists.
    //settings: json serialized settings for the endpoint.
    //content: api endpoint returned data in string format.
    private Task<object> ProcessArtistsResponseAsync(string content, JsonSerializerSettings settings, bool genres, int genreAmount)
    {
        var spotifyArtist = JsonConvert.DeserializeObject<SpotifyTopArtists>(content, settings);
        if (spotifyArtist == null)
        {
            return Task.FromResult<object>(new { message = "No Artists Found" });
        }

        if(genres == false)
        {
            foreach (var artist in spotifyArtist.Items)
            {
                if (artist.Image?.Count > 0)
                {
                    artist.FirstImageUrl = artist.Image.Last()?.Url;
                }
            }
        }
        else
        {
            //    SpotifyGenres test = new SpotifyGenres();

            //    foreach (var artist in spotifyArtist.Items)
            //    {
            //        foreach(var genre in artist.Genres)
            //        {
            //            if (!test.Genres.ContainsKey(genre))
            //            {
            //                test.Genres.Add(genre, 1);
            //            }
            //            else
            //            {
            //                test.Genres[genre] += 1;
            //            }
            //        }


            //    }
            //    return Task.FromResult<object>(new { message = "Data retrieved successfully", artists = test.Genres.OrderByDescending(genre => genre.Value).Take(genreAmount)
            //});
            var genreCounts = spotifyArtist.Items
           .SelectMany(artist => artist.Genres)
           .GroupBy(genre => genre)
           .ToDictionary(group => group.Key, group => group.Count());

            return Task.FromResult<object>(new { message = "Data retrieved successfully", artists = genreCounts.OrderByDescending(kv => kv.Value).Take(genreAmount) });
        }

        return Task.FromResult<object>(new { message = "Data retrieved successfully", artists = spotifyArtist.Items });
    }

    //Converts the endpoint string information into my object for SpotifyTopTracks.
    //settings: json serialized settings for the endpoint.
    //content: api endpoint returned data in string format.
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

    private async Task<(SpotifyUser User, string AccessToken)> GetSpotifyClient()
    {
        var accessToken = await EnsureValidAccessTokenAsync();
        var client = _httpClientFactory.CreateClient();
       client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var profileResponse = await client.GetAsync("https://api.spotify.com/v1/me");

       if (profileResponse.IsSuccessStatusCode)
       {
           var settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
           var contentProfile = await profileResponse.Content.ReadAsStringAsync();
            var spotifyUser = JsonConvert.DeserializeObject<SpotifyUser>(contentProfile, settings);

           return (spotifyUser, accessToken);
        }

        return (null, null);

    }

    //check if playlist exists already
    private async Task<object> CreatePlaylist()
    {
        try
        {
            var (spotifyUser, accessToken) = await GetSpotifyClient();
            if (spotifyUser == null || accessToken == null)
            {
                return new { message = "Failed to retrieve Spotify client information" };
            }

            var userId = spotifyUser.ID;

            var values = new Dictionary<string, string>
        {
            { "name", "PlaylistForeverWrapped" },
            { "description", "Playlist From Forever Wrapped" },
            { "public", "false" }
        };

            var json = JsonConvert.SerializeObject(values);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.PostAsync($"https://api.spotify.com/v1/users/{userId}/playlists", content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return new { message = "Playlist created successfully" };
            }
            else
            {
                return new { message = "Failed to create playlist. Error: " + responseString };
            }
        }
        catch (Exception ex)
        {
            return new { message = "An error occurred: " + ex.Message };
        }
    }

    [HttpGet]
    public async Task<object> GetUsersPlaylist()
    {
        try
        {
            var (spotifyUser, accessToken) = await GetSpotifyClient();
            if (spotifyUser == null || accessToken == null)
            {
                return new { message = "Failed to retrieve Spotify client information" };
            }

            var userId = spotifyUser.ID;

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await client.GetAsync($"https://api.spotify.com/v1/users/{userId}/playlists");
            var responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {

                var playlistUris = JsonConvert.DeserializeObject<SpotifyUsersPlaylists>(responseString);

                var playlistForeverWrapped = playlistUris.Items.Where(p => p.Name.Equals("PlaylistForeverWrapped")).ToList();

                var playlistID = playlistForeverWrapped.FirstOrDefault().Id;
                //send over the id for the playlist to update and modify it.

                return new { message = responseString };
            }
            else
            {
                return new { message = "Failed to create playlist. Error: " + responseString };
            }
        }
        catch (Exception ex)
        {
            return new { message = "An error occurred: " + ex.Message };
        }


    }

    public async Task<object> SaveToPlaylist()
    {

        return new { message = "Playlist Saved" };
    }





}
