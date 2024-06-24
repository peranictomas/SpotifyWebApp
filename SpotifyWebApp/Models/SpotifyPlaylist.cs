using Newtonsoft.Json;

namespace SpotifyWebApp.Models
{
    public class SpotifyPlaylist
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("description")]
        public string Description { get; set; }
        [JsonProperty("uri")]
        public string Uri { get; set; }
    }
}
