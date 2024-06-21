using Newtonsoft.Json;

namespace SpotifyWebApp.Models
{
    public class SpotifyUsersPlaylists
    {
        [JsonProperty("items")]
        public List<SpotifyPlaylist> Items { get; set; }
    }
}
