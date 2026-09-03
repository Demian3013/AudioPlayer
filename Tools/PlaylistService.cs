using AudioPlayer.Data;
using System.IO;
using System.Text.Json;

namespace AudioPlayer.Tools;

public class PlaylistService
{
    #region Singletone

    private static PlaylistService? _instance;
    public static PlaylistService GetInstance()
    {
        return _instance ??= _instance = new PlaylistService();
    }

    #endregion
    
    public PlaylistDatabase Database { get; set; } = new();
    
    private readonly string _filePath;
    private readonly JsonSerializerOptions _serializeOptions = new() { WriteIndented = true };
    
    private PlaylistService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var appFolder = Path.Combine(appData, "AudioPlayer");

        Directory.CreateDirectory(appFolder);

        _filePath = Path.Combine(appFolder, "playlists.json");

        Database.Playlists.CollectionChanged += _PlaylistsCollectionChanged; 
    }

    private async void _PlaylistsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        await SaveAsync();     
    }

    public AddPlaylistResult AddPlaylist(string name, List<string> tracks)
    {
        return Database.AddPlaylist(name, tracks);
    }
    
    public bool RemovePlaylist(Playlist playlist)
    {
        return Database.RemovePlaylist(playlist);
    }
    
    public bool RemovePlaylist(string name)
    {
        return Database.RemovePlaylist(name);
    }
    
    public bool RemovePlaylist(int index)
    {
        return Database.RemovePlaylist(index);
    }
    
    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            Database = new PlaylistDatabase();
            return;
        }

        var json = await File.ReadAllTextAsync(_filePath);
        var loaded = JsonSerializer.Deserialize<PlaylistDatabase>(json) ?? new PlaylistDatabase();

        if (loaded != null)
        {
            Database.Playlists.Clear();
            foreach (var playlist in loaded.Playlists)
                Database.Playlists.Add(playlist);
        }
    }

    public async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(Database, _serializeOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }
}