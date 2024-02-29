using Newtonsoft.Json;

namespace SpotifyWebApp.Models
{
    public class SpotifyFollowers
    {
        [JsonProperty("href")]
        public string? Href { get; set; }
        [JsonProperty("total")]
        public int Total { get; set; }
    }
}
