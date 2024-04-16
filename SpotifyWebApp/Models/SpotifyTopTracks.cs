using Newtonsoft.Json;

namespace SpotifyWebApp.Models
{
    public class SpotifyTopTracks
    {
        [JsonProperty("items")]
        public List<SpotifyTrack> Items { get; set; }

    }
}
