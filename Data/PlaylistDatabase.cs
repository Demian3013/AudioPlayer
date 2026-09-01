using System.Collections.ObjectModel;

namespace AudioPlayer.Data;

public enum AddPlaylistResult
{
    Success,
    ZeroTracksError,
    NameDuplicateError
}

public class PlaylistDatabase
{
    public AddPlaylistResult AddPlaylist(string name, List<string> tracks)
    {
        if (tracks.Count == 0)
        {
            return AddPlaylistResult.ZeroTracksError;
        }
        
        if (Playlists.Any(p => p.Name == name))
        {
            return AddPlaylistResult.NameDuplicateError;
        }

        Playlists.Add(new Playlist(name, tracks));
        return AddPlaylistResult.Success;
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