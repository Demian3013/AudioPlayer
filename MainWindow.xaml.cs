using AudioPlayer.Manager;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using TagLib;

namespace AudioPlayer;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();

        FolderTreeManager.BuildTree(FoldersTreeView);

        AudioFiles = new ObservableCollection<AudioFileInfo>();
        DataContext = this;
    }
    
    private static readonly Regex SoundFileRegex = MyRegex();
    
    [GeneratedRegex(@"^.*\.(mp3|wav|wma|asf|avi)$", 
        RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex();

    public struct AudioFileInfo
    {
        public string FilePath { get; set; }     
        public string Name { get; set; }         
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Duration { get; set; }     
    }

    public ObservableCollection<AudioFileInfo> AudioFiles { get; set; }

    //private void FoldersTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    //{
    //    if (FoldersTreeView.SelectedItem is not TreeViewItem item) return;
    //    if (item.Tag is not string path) return;
        
    //    Debug.WriteLine("Вы выбрали папку: " + path);

    //    var dirFiles = Directory.GetFiles(path)
    //                        .Where(file => SoundFileRegex.IsMatch(file))
    //                        .ToArray();
        
    //    // TrackDataGrid.ItemsSource = dirFiles;
    //}

    private void FoldersTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (FoldersTreeView.SelectedItem is not TreeViewItem item) return;
        if (item.Tag is not string path) return;

        Debug.WriteLine("Вы выбрали папку: " + path);

        try
        {
            AudioFiles.Clear();

            var files = Directory.EnumerateFiles(path)
                                 .Where(file => SoundFileRegex.IsMatch(file));

            foreach (var file in files)
            {
                try
                {
                    using var tagFile = TagLib.File.Create(file);

                    var info = new AudioFileInfo
                    {
                        FilePath = file,
                        Name = string.IsNullOrEmpty(tagFile.Tag.Title)
                               ? Path.GetFileNameWithoutExtension(file)
                               : tagFile.Tag.Title,
                        Artist = string.IsNullOrEmpty(tagFile.Tag.FirstPerformer)
                                 ? "Неизвестен"
                                 : tagFile.Tag.FirstPerformer,
                        Album = string.IsNullOrEmpty(tagFile.Tag.Album)
                                ? "Неизвестен"
                                : tagFile.Tag.Album,
                        Duration = tagFile.Properties.Duration.ToString(@"mm\:ss")
                    };
                    AudioFiles.Add(info);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка чтения {file}: {ex.Message}");
                    AudioFiles.Add(new AudioFileInfo
                    {
                        FilePath = file,
                        Name = Path.GetFileNameWithoutExtension(file),
                        Artist = "Ошибка",
                        Album = "Ошибка",
                        Duration = "00:00"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось прочитать папку: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}