namespace AudioPlayer.Data;

public class PlaylistDatabase
{
    public bool AddPlaylist(string name, List<string> tracks)
    {
        if (tracks.Count == 0)
        {
            return false;
        }
        
        if (Playlists.Any(p => p.Name == name))
        {
            return false;
        }

        Playlists.Add(new Playlist(name, tracks));
        return true;
    }
    
    public bool RemovePlaylist(string name)
    {
        if (Playlists.All(p => p.Name != name))
        {
            return false;
        }

        var pIndex = Playlists.FindIndex(p => p.Name == name);
        Playlists.RemoveAt(pIndex);
        return true;
    }
    
    public bool RemovePlaylist(int index)
    {
        if (index >= Playlists.Count)
        {
            return false;
        }
        
        Playlists.RemoveAt(index);
        return true;
    }
    
    public List<Playlist> Playlists { get; init; } = [];
}