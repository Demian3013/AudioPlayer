using System.Collections.ObjectModel;

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
        var playlist = Playlists.FirstOrDefault(p => p.Name == name);
        if (playlist == null)
            return false;

        Playlists.Remove(playlist);
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
    
    public ObservableCollection<Playlist> Playlists { get; init; } = [];
}