namespace AudioPlayer.Data;

public class Playlist(string name, IEnumerable<string> tracks)
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = name;
    public List<string> Tracks { get; set; } = tracks.ToList();
}