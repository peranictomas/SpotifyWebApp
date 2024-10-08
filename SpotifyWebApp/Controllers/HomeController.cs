﻿using Azure.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SpotifyWebApp.Models;
using System;
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
        var userProfile = await GetSpotifyUserProfile(accessToken);

        return View();
    }

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

    public Task<IActionResult> Privacy()
    {
        return Task.FromResult<IActionResult>(View());
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

    //This method will check if the users access token is still valid.
    //This is achieved through the use of the refresh token as well as checking
    //What time the token will expire at. If expired method will update the users access token,
    //refresh token, and the new expiry date.
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

    private async Task<object> GetSpotifyUserProfile(string accessToken)
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

    //This method is used to update the tab views to the proper time period selected and for their respective views.
    //timeRange : short_term, medium_term, long_term
    //type : artists, tracks
    [HttpGet]
    public async Task<JsonResult> GetArtistTrackData(string timeRange, string type, bool genres = false, int genreAmount = 10)
    {
        var data = await GetDataBasedOnTimeFrame(timeRange, type, genres, genreAmount);
        return Json(data);
    }

    //The method will verify the access token and will call either artists or tracks methods in the return statement to get
    //the desired information. It also creates the endpoint and sends the data contents to either method.
    //timeRange : short_term, medium_term, long_term
    //type : artists, tracks
    private async Task<object> GetDataBasedOnTimeFrame(string timeRange, string type, bool genres, int genreAmount)
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
            "artists" => await ProcessArtistsResponse(content, settings, genres, genreAmount),
            "tracks" => await ProcessTracksResponse(content, settings),
            _ => new { message = "Invalid time frame or type." }
        };
    }

    //Converts the endpoint string information into my object for SpotifyTopArtists.
    //settings: json serialized settings for the endpoint.
    //content: api endpoint returned data in string format.
    private Task<object> ProcessArtistsResponse(string content, JsonSerializerSettings settings, bool genres, int genreAmount)
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
    private Task<object> ProcessTracksResponse(string content, JsonSerializerSettings settings)
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

    //Returns the instance of the user and the users access token (used to retreive the users id in other methods)
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

    //Creates an empty playlist with the name foreverwrapped
    private async Task<object> CreatePlaylist()
    {
        var (spotifyUser, accessToken) = await GetSpotifyClient();
        if (spotifyUser == null || accessToken == null)
        {
            return new { message = "Failed to retrieve Spotify client information" };
        }

        var userId = spotifyUser.ID;

        var values = new Dictionary<string, string>
        {
            { "name", "ForeverWrapped" },
            { "description", "Playlist created by Forever Wrapped" },
            { "public", "true" }
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

    //Returns a list of tracks that are currently in the spotify playlist
    public async Task<List<SpotifyTrack>> GetPlaylistTracks(string playlistId, string accessToken)
    {

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var getTracksResponse = await client.GetAsync($"https://api.spotify.com/v1/playlists/{playlistId}/tracks");
        if (!getTracksResponse.IsSuccessStatusCode)
        {
            var errorResponse = await getTracksResponse.Content.ReadAsStringAsync();
            throw new Exception($"Failed to retrieve playlist tracks: {errorResponse}");
        }

        var getTracksResponseString = await getTracksResponse.Content.ReadAsStringAsync();
        var playlistTracks = JsonConvert.DeserializeObject<SpotifyUserPlaylist>(getTracksResponseString);

        if (playlistTracks == null || playlistTracks.Items == null)
        {
            throw new Exception("Failed to deserialize playlist tracks");
        }

        var tracks = playlistTracks.Items.Select(item => item.Track).ToList();
        return tracks;
    }

    //This method will update the playlist ForeverWrapped, deletes everything in the playlist then readds the desired tracks to it given the time range
    private async Task UpdatePlaylist(string playlistId, List<string> uris)
        {
            var (spotifyUser, accessToken) = await GetSpotifyClient();
            if (spotifyUser == null || accessToken == null)
            {
                throw new Exception("Failed to retrieve Spotify client information");
            }

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // Step 1: Get the current tracks in the playlist 
            var currentTracks = await GetPlaylistTracks(playlistId, accessToken);
            var trackUrisToRemove = currentTracks.Select(track => new { uri = track.Uri, positions = new int[] { } }).ToList();

            if (trackUrisToRemove.Count > 0)
            {
                // Step 2: Remove all tracks from the playlist
                var removeTracksPayload = new { tracks = trackUrisToRemove };
                var removeTracksContent = new StringContent(JsonConvert.SerializeObject(removeTracksPayload), Encoding.UTF8, "application/json");
                var requestMessage = new HttpRequestMessage(HttpMethod.Delete, $"https://api.spotify.com/v1/playlists/{playlistId}/tracks")
                {
                    Content = removeTracksContent
                };

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                var clearResponse = await client.SendAsync(requestMessage);

                if (!clearResponse.IsSuccessStatusCode)
                {
                    var clearResponseString = await clearResponse.Content.ReadAsStringAsync();
                    throw new Exception("Error clearing playlist: " + clearResponseString);
                }
            }

            // Step 3: Add new tracks to the playlist
            var addTracksPayload = new { uris = uris };
            var addTracksContent = new StringContent(JsonConvert.SerializeObject(addTracksPayload), Encoding.UTF8, "application/json");
            var addTracksResponse = await client.PostAsync($"https://api.spotify.com/v1/playlists/{playlistId}/tracks", addTracksContent);

            if (!addTracksResponse.IsSuccessStatusCode)
            {
                var addTracksResponseString = await addTracksResponse.Content.ReadAsStringAsync();
                throw new Exception("Error adding tracks to playlist: " + addTracksResponseString);
            }
           
    }

    //Returns a list of all tracks that the user has selected
    public async Task<object> GetSongURIList(string timeRange)
    {
        var allTracks = await GetDataBasedOnTimeFrame(timeRange, "tracks", false, 0);

        return allTracks;
    }
    
    //For the Save Tracks to Playlist button for Home/Tracks view
    [HttpGet]
    public async Task VerifyPlaylistFunction(string timeRange)
    {
        var playlistResponse = GetUsersPlaylist().Result.ToString();

        if (playlistResponse.Equals("Create"))
        {
            await CreatePlaylist();
        }

        var playlistId = await GetUsersPlaylist();

        //Gets a list of all the current tracks URIs for a given time range the user selected
        dynamic test = await GetSongURIList(timeRange);
        var tracklist = (IEnumerable<SpotifyTrack>)test.tracks;
        List<string> uriList = tracklist.Select(track => track.Uri).ToList();

        await UpdatePlaylist((string)playlistId, uriList);

    }

    //This method will get all the users playlist and check if they have a playlist already called ForeverWrapped
    //if not it will create one with that name. If you already have one then continue to saving the desired songs.
    //This method returns only the playlist ID for ForeverWrapped
    [HttpGet]
    public async Task<object> GetUsersPlaylist()
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

                var playlistForeverWrapped = playlistUris.Items.Where(p => p.Name.Equals("ForeverWrapped")).ToList();

                if (playlistForeverWrapped.Count == 0)
                {
                    return "Create";
                }

                var playlistID = playlistForeverWrapped.FirstOrDefault().Id;
           

                return playlistID;
            }
            else
            {
                return  null;
            }
    }

}
