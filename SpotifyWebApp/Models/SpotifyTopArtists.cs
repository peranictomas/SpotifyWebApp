using Newtonsoft.Json;
namespace SpotifyWebApp.Models
    
{
    public class SpotifyTopArtists 
    {
        [JsonProperty("items")]
        public List<SpotifyArtist> Items { get; set; }
    }
}
