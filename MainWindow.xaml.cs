using AudioPlayer.Manager;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TagLib;

namespace AudioPlayer;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();

        FolderTreeManager.BuildTree(FoldersTreeView);
        
        DataContext = this;
    }

    private static readonly Regex SoundFileRegex = MyRegex();
    private static readonly string TrackPlaceholderPath = "Image/track_placeholder.png";

    [GeneratedRegex(@"^.*\.(mp3|wav|wma|asf|avi)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex();

    public ObservableCollection<AudioFileInfo> AudioFiles { get; set; } = [];

    private MediaPlayer Player { get; set; } = new();
    
    private void FoldersTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (FoldersTreeView.SelectedItem is not TreeViewItem item) return;
        if (item.Tag is not string path) return;

        Debug.WriteLine("Вы выбрали папку: " + path);

        AudioFiles.Clear();
        IEnumerable<string> files = [];

        try
        {
            files = Directory.EnumerateFiles(path)
                             .Where(file => SoundFileRegex.IsMatch(file));
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine("UnauthorizedAccessException: " + ex.Message);
        }

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
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"Ошибка чтения {file}: {ex.Message}");
                AudioFiles.Add(new AudioFileInfo
                {
                    FilePath = file,
                    Name     = Path.GetFileNameWithoutExtension(file),
                    Artist   = "Ошибка",
                    Album    = "Ошибка",
                    Duration = "00:00"
                });
            }
        }
    }

    private void RewindButton_Click(object sender, RoutedEventArgs e)
    {
        
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Player.HasAudio)
        {
            MessageBox.Show("Не выбран трек или выбран медиафайл без аудио!", "Ошибка!", MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // в начале трека, то есть не начали ещё проигрывание
        if (Player.Position == TimeSpan.Zero)
        {
            Player.Play();
        }
        else
        {
            Player.Pause();
        }
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        
    }

    private void SpeedButton_Click(object sender, RoutedEventArgs e) 
    { 

    }

    private void TrackDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TrackDataGrid.SelectedItem is not AudioFileInfo selectedTrackInfo) return;
        
        using var tagFile = TagLib.File.Create(selectedTrackInfo.FilePath);

        #region TackInfoExtraction

            if (tagFile.Tag.Pictures.Length != 0)
            {
                var picture = tagFile.Tag.Pictures[0];
                var bitmapImageFromPicture = GetBitmapImageFromPicture(picture);
                if (bitmapImageFromPicture != null)
                {
                    TrackImage.Source = bitmapImageFromPicture;
                }
            }
            else
            {
                TrackImage.Source = new BitmapImage(new Uri(TrackPlaceholderPath, UriKind.Relative));
            }
            
            TrackNameTextBlock.Text = selectedTrackInfo.Name;

            TrackArtistTextBlock.Text = string.IsNullOrEmpty(tagFile.Tag.FirstPerformer)
                ? "Неизвестен"
                : tagFile.Tag.FirstPerformer;

            TrackYearTextBlock.Text = tagFile.Tag.Year > 0
                ? tagFile.Tag.Year.ToString()
                : "Неизвестно";

            TrackGenreTextBlock.Text = tagFile.Tag.Genres.Length > 0
                ? string.Join(", ", tagFile.Tag.Genres)
                : "Неизвестен";

            TrackDistributionTextBlock.Text = string.IsNullOrEmpty(tagFile.Tag.Comment)
                ? "Нет описания"
                : tagFile.Tag.Comment;

        #endregion

        var selectedTackUri = new Uri(selectedTrackInfo.FilePath, UriKind.Absolute);
        
        Player.Pause();
        ProgressSlider.Value = 0;
        Player.Close();
        Player.Open(selectedTackUri);
    }

    private static BitmapImage? GetBitmapImageFromPicture(IPicture p)
    {
        if (p.Data.IsEmpty) return null;
        
        using var stream = new MemoryStream(p.Data.Data);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = stream;
        bmp.EndInit();
        
        return bmp;
    }
}