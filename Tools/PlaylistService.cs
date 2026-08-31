using AudioPlayer.Data;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace AudioPlayer.Tools;

public class PlaylistService
{
    private readonly string _filePath;
    public PlaylistDatabase _database = new();
    private readonly JsonSerializerOptions _serializeOptions = new() { WriteIndented = true };

    public PlaylistService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var appFolder = Path.Combine(appData, "AudioPlayer");

        Directory.CreateDirectory(appFolder);

        _filePath = Path.Combine(appFolder, "playlists.json");

        _database.Playlists.CollectionChanged += _PlaylistsCollectionChanged; 
    }

    private async void _PlaylistsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        await SaveAsync();     
    }

    public bool AddPlaylist(string name, List<string> tracks)
    {
        return _database.AddPlaylist(name, tracks);
    }
    
    public bool RemovePlaylist(string name)
    {
        return _database.RemovePlaylist(name);
    }
    
    public bool RemovePlaylist(int index)
    {
        return _database.RemovePlaylist(index);
    }
    
    public async Task LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            _database = new PlaylistDatabase();
            return;
        }

        var json = await File.ReadAllTextAsync(_filePath);
        var loaded = JsonSerializer.Deserialize<PlaylistDatabase>(json) ?? new PlaylistDatabase();

        if (loaded != null)
        {
            _database.Playlists.Clear();
            foreach (var playlist in loaded.Playlists)
                _database.Playlists.Add(playlist);
        }
    }

    public async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_database, _serializeOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }
}