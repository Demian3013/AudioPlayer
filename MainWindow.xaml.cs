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
    private static readonly Regex SoundFileRegex = MyRegex();
    private const string TrackPlaceholderImgPath = "Image/track_placeholder.png";
    private const string TrackStopImgPath = "Image/stop.png";
    private const string TrackPlayImgPath = "Image/play.png";
    private const double TrackProgressTimerTickFreq = 0.5;

    private bool _isTrackPlaying;
    private bool _isDragging;
    private TimeSpan _totalDuration;
    private AudioFileInfo? _currentTrack = null;
    
    public ObservableCollection<AudioFileInfo> AudioFiles { get; set; } = [];
    private MediaPlayer Player { get; set; } = new();
    private readonly DispatcherTimer? _trackProgressTimer;

    #region SoundFileRegex
    
        [GeneratedRegex(@"^.*\.(mp3|wav|wma|asf|avi)$", RegexOptions.IgnoreCase | RegexOptions.Compiled,
            "en-US")]
        private static partial Regex MyRegex();

    #endregion
    
    public MainWindow()
    {
        InitializeComponent();

        FolderTreeManager.BuildTree(FoldersTreeView);
        DataContext = this;
        Loaded += MainWindow_Loaded;
        
        _trackProgressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(TrackProgressTimerTickFreq)
        };
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
            MessageBox.Show("Не выбран трек или выбран медиафайл без аудио!", "Ошибка!", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_isTrackPlaying)
        {
            PauseTrackPlaying();
        }
        else
        {
            ResumeTrackPlaying();
        }
    }

    private void ResumeTrackPlaying(bool resetProgressSlider = false)
    {
        PlayPauseImage.Source = new BitmapImage(new Uri(TrackPlayImgPath, UriKind.Relative));
        StartTrackProgressTimer();
        Player.Play();

        if (resetProgressSlider)
        {
            ProgressSlider.Value = 0;
        }
        
        _isTrackPlaying = true;
    }

    private void PauseTrackPlaying(bool resetProgressSlider = false)
    {
        PlayPauseImage.Source = new BitmapImage(new Uri(TrackStopImgPath, UriKind.Relative));
        Player.Pause();
        StopTrackProgressTimer();
        
        if (resetProgressSlider)
        {
            ProgressSlider.Value = 0;
        }

        _isTrackPlaying = false;
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {

    }

    private void SpeedButton_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLineIf(sender is not Button, "ERROR: sender is not Button");
        if (sender is not Button button) return;

        var content = button.Content.ToString();
        var condition = !double.TryParse(content, out var speed);
        
        Debug.WriteLineIf(condition, "ERROR: !double.TryParse(content, ...)");
        if (condition) return;
        
        Player.SpeedRatio = speed;
    }

    private void TrackDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Debug.WriteLineIf(TrackDataGrid.SelectedItem is not AudioFileInfo,
            "ERROR: TrackDataGrid.SelectedItem is not AudioFileInfo");
        
        if (TrackDataGrid.SelectedItem is not AudioFileInfo selectedTrackInfo) return;

        SetCurrentTrack(selectedTrackInfo);
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        _totalDuration = Player.NaturalDuration.TimeSpan;
        ResumeTrackPlaying(true);
    }

    private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {

    }

    private void Thumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        _isDragging = true;
    }

    private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isDragging = false;
        if (!Player.NaturalDuration.HasTimeSpan) return;
        
        var newPositionSeconds = _totalDuration.TotalSeconds * ProgressSlider.Value;
        Player.Position = TimeSpan.FromSeconds(newPositionSeconds);
    }
    
    private void StartTrackProgressTimer()
    {
        Debug.WriteLineIf(_trackProgressTimer == null, "ERROR: _trackProgressTimer == null");
        if (_trackProgressTimer == null) return;
        
        _trackProgressTimer.Tick += TrackProgressTimerTick;
        _trackProgressTimer.Start();
    }

    private void TrackProgressTimerTick(object? sender, EventArgs e)
    {
        if (_isDragging || !Player.NaturalDuration.HasTimeSpan || !(_totalDuration.TotalSeconds > 0)) return;
        
        var current = Player.Position.TotalSeconds;
        var total   = _totalDuration.TotalSeconds;
        ProgressSlider.Value = current / total;
    }

    private void StopTrackProgressTimer()
    {
        Debug.WriteLineIf(_trackProgressTimer == null, "ERROR: _trackProgressTimer == null");
        
        if (!_isTrackPlaying || _trackProgressTimer == null) return;

        _trackProgressTimer.Tick -= TrackProgressTimerTick;
        _trackProgressTimer.Stop();
        _isTrackPlaying = false;
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Player.Volume = e.NewValue;
    }

    private void SetCurrentTrack(AudioFileInfo trackInfo)
    {
        _currentTrack = trackInfo;

        using var tagFile = TagLib.File.Create(_currentTrack.Value.FilePath);

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
            TrackImage.Source = new BitmapImage(new Uri(TrackPlaceholderImgPath, UriKind.Relative));
        }

        TrackNameTextBlock.Text = _currentTrack.Value.Name;

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

        var selectedTrackUri = new Uri(_currentTrack.Value.FilePath, UriKind.Absolute);

        PauseTrackPlaying(true);

        Player.MediaOpened -= OnMediaOpened;
        Player.MediaOpened += OnMediaOpened;

        Player.Close();
        Player.Open(selectedTrackUri);
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