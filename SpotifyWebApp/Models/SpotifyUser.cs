using Newtonsoft.Json;

public class SpotifyUser
{
    [JsonProperty("email")]
    public string Email { get; set; }

    // Add other properties as needed
}
