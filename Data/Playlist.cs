using System.Text.Json.Serialization;

namespace AudioPlayer.Data;


public class Playlist
{
    [JsonConstructor]
    public Playlist(Guid id, string name, List<string> tracks)
    {
        Id = id;
        Name = name;
        Tracks = tracks ?? new List<string>();
    }

    public Playlist(string name, IEnumerable<string> tracks)
    : this(Guid.NewGuid(), name, tracks?.ToList() ?? new List<string>())
    {
    }


    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; }
    public List<string> Tracks { get; set; }
}