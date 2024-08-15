using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http.Headers;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration.AddJsonFile("appsettings.json");

// Services
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "Spotify";
})
.AddCookie()
.AddOAuth("Spotify", options =>
{
    options.ClientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID");
    options.ClientSecret = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET");
    options.CallbackPath = new Microsoft.AspNetCore.Http.PathString("/signin-spotify");
    options.AuthorizationEndpoint = "https://accounts.spotify.com/authorize";
    options.TokenEndpoint = "https://accounts.spotify.com/api/token";
    //options.RedirectUri = Environment.GetEnvironmentVariable("SPOTIFY_REDIRECT_URI") ?? "http://localhost:5000/signin-spotify";
    options.Scope.Add("user-read-private");
    options.Scope.Add("user-read-email");
    options.Scope.Add("user-top-read");
    options.Scope.Add("playlist-read-private");
    options.Scope.Add("playlist-modify-private");
    options.Scope.Add("playlist-modify-public");
    options.SaveTokens = true;
    options.ClaimActions.MapJsonKey("urn:spotify:displayname", "display_name");
    options.ClaimActions.MapJsonKey("urn:spotify:email", "email");
    options.Events = new OAuthEvents
    {

        OnRedirectToAuthorizationEndpoint = async context =>
        {
            var baseUrl = builder.Configuration["BaseUrl"] ?? "https://localhost:7196";
            var redirectUri = $"{baseUrl}{options.CallbackPath}";
            context.Response.Redirect(redirectUri);
            await Task.CompletedTask;
        },

        OnCreatingTicket = async context =>
        {
            var accessToken = context.AccessToken;
            var refreshToken = context.RefreshToken;
            var expiresIn = context.ExpiresIn?.TotalSeconds;

            var claims = new List<Claim>
            {
                new Claim("access_token", accessToken),
                new Claim("refresh_token", refreshToken),
                new Claim("expires_at", DateTime.UtcNow.AddSeconds(expiresIn.Value).ToString())
            };

            context.Identity.AddClaims(claims);

            await Task.CompletedTask;
        }
    };
});

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (app.Environment.IsProduction())
{
    var port = Environment.GetEnvironmentVariable("PORT");
    if (port != null)
    {
        app.Urls.Add($"http://*:{port}");
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
