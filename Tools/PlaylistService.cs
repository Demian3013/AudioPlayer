using System.IO;
using System.Text.Json;
using AudioPlayer.Data;

namespace AudioPlayer.Tools;

public class PlaylistService
{
    private readonly string _filePath;
    private PlaylistDatabase _database = new();
    private readonly JsonSerializerOptions _serializeOptions = new() { WriteIndented = true };

    public PlaylistService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var appFolder = Path.Combine(appData, "AudioPlayer");

        Directory.CreateDirectory(appFolder);

        _filePath = Path.Combine(appFolder, "playlists.json");
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
        _database = JsonSerializer.Deserialize<PlaylistDatabase>(json) ?? new PlaylistDatabase();
    }

    public async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_database, _serializeOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }
}