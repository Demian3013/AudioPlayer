using System.IO;

namespace AudioPlayer.Structs;

public record struct AudioFileInfo
{
    public AudioFileInfo(int index, string filePath, string title, string artist, string album, TimeSpan duration)
        : this()
    {
        Index = $"{index + 1}.";
        
        FilePath = filePath;

        Title = string.IsNullOrEmpty(title)
            ? Path.GetFileNameWithoutExtension(filePath)
            : title;

        Artist = string.IsNullOrEmpty(artist)
            ? "Неизвестен"
            : artist;

        Album = string.IsNullOrEmpty(album)
            ? "Неизвестен"
            : album;

        Duration = duration.ToString(@"mm\:ss");
    }

    public string Index {  get; set; }
    public string FilePath { get; set; }
    public string Title { get; set; }
    public string Artist { get; set; }
    public string Album { get; set; }
    public string Duration { get; set; }
    public bool IsListened { get; set; } = false;
}