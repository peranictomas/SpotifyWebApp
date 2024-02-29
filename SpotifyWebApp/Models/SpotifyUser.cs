using Newtonsoft.Json;
using SpotifyWebApp.Models;

public class SpotifyUser
{
    [JsonProperty("email")]
    public string Email { get; set; }
    [JsonProperty("display_name")]
    public string DisplayName { get; set; }
    [JsonProperty("images")]
    public List<SpotifyImage> Images { get; set; }
    [JsonProperty("id")]
    public string ID { get; set; }
    [JsonProperty("country")]
    public string Country { get; set; }
    [JsonProperty("product")]
    public string AccountType { get; set; }
    [JsonProperty("followers")]
    public SpotifyFollowers Followers { get; set; }
    // Add other properties as needed
}
