using Newtonsoft.Json;

namespace SpotifyWebApp.Models
{
    public class SpotifyUserPlaylist
    {
        [JsonProperty("items")]
        public List<SpotifyTracks> Items { get; set; }
    }
}
