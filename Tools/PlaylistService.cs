using System.IO;
using System.Text.Json;
using AudioPlayer.Data;

namespace AudioPlayer.Tools;

public class PlaylistService
{
    private readonly string _filePath;

    private PlaylistDatabase _database = new();

    public PlaylistService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var appFolder = Path.Combine(appData, "AudioPlayer");

        Directory.CreateDirectory(appFolder);

        _filePath = Path.Combine(appFolder, "playlists.json");
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
        var options = new JsonSerializerOptions()
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(_database, options);

        await File.WriteAllTextAsync(_filePath, json);
    }
}