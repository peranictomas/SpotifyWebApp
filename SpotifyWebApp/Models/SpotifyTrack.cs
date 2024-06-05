using Newtonsoft.Json;

namespace SpotifyWebApp.Models
{
    public class SpotifyTrack
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("album")]
        public SpotifyAlbum Album { get; set; }
      
    }
}
