namespace SpotifyWebApp.Models
{
    public class SpotifyGenres
    {

        public Dictionary<string, int> Genres { get; set; }

        public SpotifyGenres()
        {
            Genres = new Dictionary<string, int>();
        }
    }
}
