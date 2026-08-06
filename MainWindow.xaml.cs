using AudioPlayer.Manager;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TagLib;

namespace AudioPlayer;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();

        FolderTreeManager.BuildTree(FoldersTreeView);

        DataContext = this;

        this.Loaded += MainWindow_Loaded;
    }

    private static readonly Regex SoundFileRegex = MyRegex();
    private static readonly string TrackPlaceholderPath = "Image/track_placeholder.png";

    [GeneratedRegex(@"^.*\.(mp3|wav|wma|asf|avi)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex();

    public ObservableCollection<AudioFileInfo> AudioFiles { get; set; } = [];

    private AudioFileInfo? _currentTrack = null;

    private MediaPlayer Player { get; set; } = new();

    private bool _isPlaying = false;
    private bool _isDragging = false;
    private TimeSpan _totalDuration;
    private DispatcherTimer _updateTimer;

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
                    Name = Path.GetFileNameWithoutExtension(file),
                    Artist = "Ошибка",
                    Album = "Ошибка",
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
            return;
        }

        if (_isPlaying)
        {
            Player.Pause();
            PlayPauseImage.Source = new BitmapImage(new Uri("Image/stop.png", UriKind.Relative));
            _isPlaying = false;
        }
        else
        {
            Player.Play();
            PlayPauseImage.Source = new BitmapImage(new Uri("Image/play.png", UriKind.Relative));
            _isPlaying = true;
        }
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {

    }

    private void SpeedButton_Click(object sender, RoutedEventArgs e)
    {
        Button? button = sender as Button;
        if (button == null) return;

        string content = button.Content.ToString();

        if (double.TryParse(content, out double speed))
        {
            Player.SpeedRatio = speed;
        }
    }

    private void TrackDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TrackDataGrid.SelectedItem is not AudioFileInfo selectedTrackInfo) return;

        PlayTrack(selectedTrackInfo);
    }

    private void OnMediaOpened(object sender, EventArgs e)
    {
        _totalDuration = Player.NaturalDuration.TimeSpan;
        ProgressSlider.Value = 0;
        StartUpdateTimer();
        Player.Play();
        _isPlaying = true;

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

    private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {

    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ProgressSlider.ApplyTemplate();

        var track = ProgressSlider.Template.FindName("PART_Track", ProgressSlider) as Track;
        var thumb = track?.Thumb;
        if (thumb != null)
        {
            thumb.DragStarted += Thumb_DragStarted;
            thumb.DragCompleted += Thumb_DragCompleted;
        }
        Player.Volume = VolumeSlider.Value;
    }

    private void Thumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        _isDragging = true;
    }

    private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isDragging = false;

        if (Player.NaturalDuration.HasTimeSpan)
        {
            double newPositionSeconds = _totalDuration.TotalSeconds * ProgressSlider.Value;
            Player.Position = TimeSpan.FromSeconds(newPositionSeconds);
        }
    }
    private void StartUpdateTimer()
    {
        _updateTimer = new DispatcherTimer();
        _updateTimer.Interval = TimeSpan.FromMilliseconds(500);
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();
    }

    private void UpdateTimer_Tick(object sender, EventArgs e)
    {
        if (!_isDragging && Player.NaturalDuration.HasTimeSpan && _totalDuration.TotalSeconds > 0)
        {
            double current = Player.Position.TotalSeconds;
            double total = _totalDuration.TotalSeconds;
            ProgressSlider.Value = current / total;
        }
    }

    private void StopUpdateTimer()
    {
        _updateTimer?.Stop();
        _updateTimer = null;
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Player.Volume = e.NewValue;
    }

    private void PlayTrack(AudioFileInfo trackInfo)
    {
        _currentTrack = trackInfo;

        using var tagFile = TagLib.File.Create(trackInfo.FilePath);

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

        TrackNameTextBlock.Text = trackInfo.Name;

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

        var selectedTrackUri = new Uri(trackInfo.FilePath, UriKind.Absolute);

        Player.Stop();
        StopUpdateTimer();
        ProgressSlider.Value = 0;
        _isPlaying = false;
        PlayPauseImage.Source = new BitmapImage(new Uri("/Image/play.png", UriKind.Relative));

        Player.MediaOpened -= OnMediaOpened;
        Player.MediaOpened += OnMediaOpened;

        Player.Close();
        Player.Open(selectedTrackUri);
    }
}