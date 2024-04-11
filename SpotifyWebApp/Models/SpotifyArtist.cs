using Newtonsoft.Json;
namespace SpotifyWebApp.Models
{
    public class SpotifyArtist
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("images")]
        public List<SpotifyImage> Image { get; set; }
        public string ImageTest { get; set; }
    }
}
