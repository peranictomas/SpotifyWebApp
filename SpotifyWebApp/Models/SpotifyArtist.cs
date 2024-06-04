using Newtonsoft.Json;
namespace SpotifyWebApp.Models
{
    public class SpotifyArtist
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("images")]
        public List<SpotifyImage> Image { get; set; }
        public string FirstImageUrl { get; set; }
        [JsonProperty("uri")]
        public string Uri { get; set; }
    }
}
