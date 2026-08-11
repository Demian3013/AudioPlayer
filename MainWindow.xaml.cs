using AudioPlayer.Manager;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using TagLib;

namespace AudioPlayer;

public partial class MainWindow
{
    private enum PlaybackMode
    {
        RepeatAll,   
        RepeatOne,   
        Random
    }
    private const string TrackPlaceholderImgPath = "Image/track_placeholder.png";
    private const string TrackStopImgPath = "Image/stop.png";
    private const string TrackPlayImgPath = "Image/play.png";
    private const string ProgressSliderTemplateName = "PART_Track";
    private const double TrackProgressTimerTickFreq = 0.15;

    private bool _isTrackPlaying;
    private bool _isDragging;
    private TimeSpan _totalDuration;
    private AudioFileInfo? _currentTrack;
    private PlaybackMode _currentMode = PlaybackMode.RepeatAll;

    public ObservableCollection<AudioFileInfo> AudioFiles { get; } = [];
    private MediaPlayer Player { get; } = new();
    private readonly DispatcherTimer? _trackProgressTimer;

    private readonly BitmapImage _stopTrackBitmapImage = new(new Uri(TrackStopImgPath, UriKind.Relative));
    private readonly BitmapImage _playTrackBitmapImage = new(new Uri(TrackPlayImgPath, UriKind.Relative));
    private readonly BitmapImage _trackPlaceholderBitmapImage = new(new Uri(TrackPlaceholderImgPath, UriKind.Relative));

    private static readonly Regex SoundFileRegex = MyRegex();

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
        Loaded += InitializeSliderThumb;

        _trackProgressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(TrackProgressTimerTickFreq)
        };

        Player.Volume = VolumeSlider.Value;
    }

    private void InitializeSliderThumb(object sender, RoutedEventArgs e)
    {
        ProgressSlider.ApplyTemplate();

        var findName = ProgressSlider.Template.FindName(ProgressSliderTemplateName, ProgressSlider);
        if (findName is not Track track) return;

        var thumb = track.Thumb;
        if (thumb == null) return;

        thumb.DragStarted += Thumb_DragStarted;
        thumb.DragCompleted += Thumb_DragCompleted;
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
                           ? System.IO.Path.GetFileNameWithoutExtension(file)
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
                    Name = System.IO.Path.GetFileNameWithoutExtension(file),
                    Artist = "Ошибка",
                    Album = "Ошибка",
                    Duration = "00:00"
                });
            }
        }
    }
    private void TrackDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Debug.WriteLineIf(TrackDataGrid.SelectedItem is not AudioFileInfo,
            "ERROR: TrackDataGrid.SelectedItem is not AudioFileInfo");

        if (TrackDataGrid.SelectedItem is not AudioFileInfo selectedTrackInfo) return;

        SetCurrentTrack(selectedTrackInfo);
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
            TrackImage.Source = _trackPlaceholderBitmapImage;
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

        StopTrack();

        Player.Close();
        Player.Open(selectedTrackUri);

        Player.MediaOpened -= OnMediaOpened;
        Player.MediaOpened += OnMediaOpened;

        Player.MediaEnded -= OnMediaEnded;
        Player.MediaEnded += OnMediaEnded;
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

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        _totalDuration = Player.NaturalDuration.TimeSpan;
        PlayTrack();
    }

    private void PlayTrack()
    {
        PlayPauseImage.Source = _stopTrackBitmapImage;
        StartTrackProgressTimer();
        Player.Play();
        _isTrackPlaying = true;
    }

    private void PauseTrack()
    {
        PlayPauseImage.Source = _playTrackBitmapImage;
        Player.Pause();
        StopTrackProgressTimer();
        _isTrackPlaying = false;
    }

    private void StopTrack()
    {
        PlayPauseImage.Source = _playTrackBitmapImage;
        Player.Stop();
        StopTrackProgressTimer();
        ProgressSlider.Value = 0;
        _isTrackPlaying = false;
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
        var total = _totalDuration.TotalSeconds;
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
            PauseTrack();
        }
        else
        {
            if (IsTrackEnded())
            {
                StopTrack();
                PlayTrack();
            }
            else
            {
                PlayTrack();
            }
        }
    }

    private bool IsTrackEnded()
    {
        return Player.Position == _totalDuration;
    }

    private void RewindButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Player.HasAudio) return;

        Player.Stop();
        ProgressSlider.Value = 0;

        if (_isTrackPlaying)
        {
            Player.Play();
        }
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        StopTrack();
        PlayNextTrack();
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

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Player.Volume = e.NewValue;
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

    private void ProgressSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Ellipse) return;

        if (sender is not Slider slider) return;

        var fraction = Math.Clamp(e.GetPosition(slider).X / slider.ActualWidth, 0, 1);
        slider.Value = fraction;

        if (!Player.HasAudio) return;

        Player.Position = TimeSpan.FromSeconds(_totalDuration.TotalSeconds * fraction);

        e.Handled = true;
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        switch (_currentMode)
        {
            case PlaybackMode.RepeatAll:
                PlayNextTrack();          
                break;
            case PlaybackMode.RepeatOne:
                StopTrack();               
                PlayTrack();               
                break;
            case PlaybackMode.Random:
                PlayRandomTrack();         
                break;
        }
    }

    private void PlayNextTrack()
    {
        if (_currentTrack is null) return;
        var currentIndex = AudioFiles.IndexOf((AudioFileInfo)_currentTrack);

        if (currentIndex < 0 || currentIndex == AudioFiles.Count - 1) return;

        var nextIndex = currentIndex + 1;
        if (nextIndex >= AudioFiles.Count)
        {
            MessageBox.Show("В папке больше нет треков для воспроизведения!", "Ошибка!",
             MessageBoxButton.OK);
            return;
        }

        var nextTrack = AudioFiles[nextIndex];

        SetCurrentTrack(nextTrack);
        TrackDataGrid.SelectedItem = nextTrack;
    }

    private void PlayRandomTrack()
    {
        if (AudioFiles.Count == 0 || _currentTrack is null) return;

        var currentIndex = AudioFiles.IndexOf((AudioFileInfo)_currentTrack);

        int randomIndex = currentIndex;         
        if (AudioFiles.Count > 1)
        {
            var rand = new Random();
            do
            {
                randomIndex = rand.Next(AudioFiles.Count);
            } while (randomIndex == currentIndex);
        }

        var randomTrack = AudioFiles[randomIndex];
        SetCurrentTrack(randomTrack);
        TrackDataGrid.SelectedItem = randomTrack;
    }

    private void TrackEndModeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {

    }

    private void InvisibleSliderButton_Click(object sender, RoutedEventArgs e)
    {
        int current = (int)TrackEndModeSlider.Value;
        int next = (current + 1) % 3;
        TrackEndModeSlider.Value = next;
        _currentMode = (PlaybackMode)next; 

        switch (next)
        {
            case 0:
                TrackEndModeLabel.Content = "Все треки подряд";
                break;
            case 1:
                TrackEndModeLabel.Content = "Только этот трек";
                break;
            case 2:
                TrackEndModeLabel.Content = "Случайный трек";
                break;
        }
    }
}