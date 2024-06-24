using Newtonsoft.Json;

namespace SpotifyWebApp.Models
{
    public class SpotifyTracks
    {
        [JsonProperty("track")]
        public SpotifyTrack Track { get; set; }
    }
}
