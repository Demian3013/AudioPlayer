namespace AudioPlayer.Data;

public class Playlist
{
    public Guid Id { get; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public List<string> Tracks { get; set; } = [];
}