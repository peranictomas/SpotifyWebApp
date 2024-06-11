namespace SpotifyWebApp.Models
{
    public class SpotifyGenres
    {
        //private Dictionary<string, int> genreDict;

        //public SpotifyGenres()
        //{

        //}
        //public SpotifyGenres(Dictionary<string, int> genreDict)
        //{
       //     this.genreDict = genreDict;
       // }

       // public Dictionary<string, int> Genres { get; set; }

        public Dictionary<string, int> Genres { get; set; }

        public SpotifyGenres()
        {
            Genres = new Dictionary<string, int>();
        }
    }
}
