using Newtonsoft.Json;

namespace SpotifyWebApp.Models
{
    public class SpotifyAlbum
    {

        [JsonProperty("images")]
        public List<SpotifyImage> Image { get; set; }
        public string FirstImageUrl { get; set; }
        [JsonProperty("artists")]
        public List<SpotifyArtist> Artists { get; set; }
        [JsonProperty("uri")]
        public string Uri { get; set; }
        public string stringArtists { get; set; }

    }
}
