using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using AudioPlayer.Structs;
using AudioPlayer.Tools;
using TagLib;
using Path = System.IO.Path;
using WpfAnimatedGif;

namespace AudioPlayer;

public partial class MainWindow
{
    private enum PlaybackMode
    {
        DontRepeat,
        RepeatSelected,
        RepeatAll,
        Random
    }

    public ObservableCollection<AudioFileInfo> AudioFiles { get; } = [];

    private const string TrackPlaceholderImgPath = "Image/track_placeholder.png";
    private const string TrackStopImgPath = "Image/stop.png";
    private const string TrackPlayImgPath = "Image/play.png";
    private const string ProgressSliderTemplateName = "PART_Track";
    private const double TrackProgressTimerTickFreq = 0.15;
    
    private bool _isTrackPlaying;
    private bool _isDragging;
    private double _trackSpeed = 1;
    private TimeSpan _totalDuration;
    private AudioFileInfo? _currentTrack;
    private PlaybackMode _currentMode;

    private readonly MediaPlayer _player = new();
    private readonly DispatcherTimer? _trackProgressTimer;
    private readonly PlaylistService _playlistService = new();

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

        _player.Volume = VolumeSlider.Value;

        _currentMode = GetSliderPlaybackMode();

        var gifUri = new Uri("loading.gif", UriKind.Relative);
        var bitmap = new BitmapImage(gifUri);
        ImageBehavior.SetAnimatedSource(LoadingGif, bitmap);
    }

    protected override async void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        await _playlistService.LoadAsync();
    }

    private PlaybackMode GetSliderPlaybackMode()
    {
        var playbackModes = Enum.GetValues<PlaybackMode>();

        if (TrackEndModeSlider.Value >= playbackModes.Length)
        {
            throw new InvalidEnumArgumentException(
                "Нет соответствующего этому индексу значения в PlaybackMode перечислении");
        }

        return (PlaybackMode)TrackEndModeSlider.Value;
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

    private CancellationTokenSource? _cancellationTokenSource;
    private int _loadVersion;

    private async void FoldersTreeView_OnSelectedItemChanged(
        object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        if (FoldersTreeView.SelectedItem is not TreeViewItem item)
            return;

        if (item.Tag is not string path)
            return;

        // Отменяем предыдущую загрузку
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();

        var cts = new CancellationTokenSource();
        _cancellationTokenSource = cts;

        var token = cts.Token;
        var loadVersion = ++_loadVersion;

        Debug.WriteLine($"Вы выбрали папку: {path}");

        AudioFiles.Clear();

        try
        {
            // Поиск файлов выполняем в background-потоке,
            // поскольку Directory.* — синхронный API.
            var files = await Task.Run(
                () => FolderTreeManager.GetFilesRecursive(
                    path,
                    SoundFileRegex,
                    token),
                token);

            token.ThrowIfCancellationRequested();

            // Читаем metadata также не в UI-потоке.
            var audioFiles = await Task.Run(
                () => ReadAudioMetadata(files.ToList(), token),
                token);

            token.ThrowIfCancellationRequested();

            // Защита от устаревшей операции.
            if (loadVersion != _loadVersion)
                return;

            // После await мы снова на UI thread.
            foreach (var audioFile in audioFiles)
            {
                token.ThrowIfCancellationRequested();

                AudioFiles.Add(audioFile);
            }
        }
        catch (OperationCanceledException)
        {
            // Нормальная ситуация:
            // пользователь выбрал другую папку.
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine(
                $"Нет доступа к папке {path}: {ex.Message}");
        }
        catch (DirectoryNotFoundException ex)
        {
            Debug.WriteLine(
                $"Папка не найдена {path}: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"Ошибка загрузки папки {path}: {ex}");
        }

        LoadingOverlay.Visibility = Visibility.Collapsed;
    }

    private static List<AudioFileInfo> ReadAudioMetadata(
        IReadOnlyList<string> files,
        CancellationToken cancellationToken)
    {
        var result = new List<AudioFileInfo>(files.Count);

        for (var index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = files[index];

            try
            {
                using var tagFile = TagLib.File.Create(filePath);

                var title = tagFile.Tag.Title;
                var artist = tagFile.Tag.FirstPerformer;
                var album = tagFile.Tag.Album;
                var duration = tagFile.Properties.Duration;

                result.Add(new AudioFileInfo(index, filePath, title, artist, album, duration));
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine(
                    $"Нет доступа к файлу {filePath}: {ex.Message}");
            }
            catch (ArgumentOutOfRangeException ex)
            {
                Debug.WriteLine(
                    $"Ошибка чтения файла {filePath}: {ex.Message}");
            }
            catch (CorruptFileException ex)
            {
                Debug.WriteLine(
                    $"Повреждённый файл {filePath}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(
                    $"Ошибка обработки {filePath}: {ex.Message}");
            }
        }

        return result;
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

        TrackNameTextBlock.Text = _currentTrack.Value.Title;

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

        _player.Close();
        _player.Open(selectedTrackUri);

        _player.MediaOpened -= OnMediaOpened;
        _player.MediaOpened += OnMediaOpened;

        _player.MediaEnded -= OnMediaEnded;
        _player.MediaEnded += OnMediaEnded;
    }

    private static BitmapImage? GetBitmapImageFromPicture(IPicture p)
    {
        if (p.Data.IsEmpty) return null;

        try
        {
            using var stream = new MemoryStream(p.Data.Data);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = stream;
            bmp.EndInit();
            return bmp;
        }
        catch (NotSupportedException ex)
        {
            Debug.WriteLine("NotSupportedException: " + ex.Message);
            return null;
        }
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        _totalDuration = _player.NaturalDuration.TimeSpan;
        _player.Volume = VolumeSlider.Value;
        _player.SpeedRatio = _trackSpeed;
        PlayTrack();
    }

    private void PlayTrack()
    {
        PlayPauseImage.Source = _stopTrackBitmapImage;
        StartTrackProgressTimer();
        _player.Play();
        _isTrackPlaying = true;
    }

    private void PauseTrack()
    {
        PlayPauseImage.Source = _playTrackBitmapImage;
        _player.Pause();
        StopTrackProgressTimer();
        _isTrackPlaying = false;
    }

    private void StopTrack()
    {
        PlayPauseImage.Source = _playTrackBitmapImage;
        _player.Stop();
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
        if (_isDragging || !_player.NaturalDuration.HasTimeSpan || !(_totalDuration.TotalSeconds > 0)) return;

        var current = _player.Position.TotalSeconds;
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
        if (_isTrackPlaying)
        {
            PauseTrack();
            return;
        }

        switch (_currentMode)
        {
            case PlaybackMode.DontRepeat when IsTrackEnded():
                StopTrack();
                PlayTrack();
                return;
            case PlaybackMode.DontRepeat when !IsTrackEnded():
                return;
            case PlaybackMode.RepeatAll:
                SetCurrentTrack(AudioFiles[0]);
                PlayTrack();
                return;
            case PlaybackMode.Random:
                PlayRandomTrack();
                return;
            case PlaybackMode.RepeatSelected:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (!_player.HasAudio)
        {
            MessageBox.Show("Не выбран трек или выбран медиафайл без аудио!", "Ошибка!",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        PlayTrack();
    }

    private bool IsTrackEnded()
    {
        return _currentTrack != null && _player.Position == _totalDuration;
    }

    private void RewindButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_player.HasAudio) return;

        PlayPreviousTrack();
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        StopTrack();

        if (_currentMode == PlaybackMode.Random)
        {
            if (_currentTrack == null) return;

            var curTrackIndex = AudioFiles.IndexOf((AudioFileInfo)_currentTrack);
            if (curTrackIndex == -1) return;

            var file = AudioFiles[curTrackIndex];
            file.IsListened = true;
            AudioFiles[curTrackIndex] = file;

            PlayRandomTrack();
            return;
        }

        PlayNextTrack();
    }

    private void Thumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        _isDragging = true;
    }

    private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isDragging = false;
        if (!_player.NaturalDuration.HasTimeSpan) return;

        var newPositionSeconds = _totalDuration.TotalSeconds * ProgressSlider.Value;
        _player.Position = TimeSpan.FromSeconds(newPositionSeconds);
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _player.Volume = e.NewValue;
    }

    private void SpeedButton_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLineIf(sender is not Button, "ERROR: sender is not Button");
        if (sender is not Button button) return;

        var content = button.Content.ToString();
        var condition = !double.TryParse(content, out var speed);

        Debug.WriteLineIf(condition, "ERROR: !double.TryParse(content, ...)");
        if (condition) return;

        _player.SpeedRatio = speed;
        _trackSpeed = speed;
    }

    private void ProgressSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Ellipse) return;

        if (sender is not Slider slider) return;

        var fraction = Math.Clamp(e.GetPosition(slider).X / slider.ActualWidth, 0, 1);
        slider.Value = fraction;

        if (!_player.HasAudio) return;

        _player.Position = TimeSpan.FromSeconds(_totalDuration.TotalSeconds * fraction);

        e.Handled = true;
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        switch (_currentMode)
        {
            case PlaybackMode.DontRepeat:
                StopTrack();
                break;
            case PlaybackMode.RepeatSelected:
                StopTrack();
                PlayTrack();
                break;
            case PlaybackMode.RepeatAll:
                PlayNextTrack();
                break;
            case PlaybackMode.Random:
                PlayRandomTrack();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (_currentTrack == null) return;

        var curTrackIndex = AudioFiles.IndexOf((AudioFileInfo)_currentTrack);
        if (curTrackIndex == -1) return;

        var file = AudioFiles[curTrackIndex];
        file.IsListened = true;
        AudioFiles[curTrackIndex] = file;
    }

    private void PlayNextTrack()
    {
        if (_currentTrack is null) return;
        var currentIndex = AudioFiles.IndexOf((AudioFileInfo)_currentTrack);

        if (currentIndex == 0 || currentIndex == AudioFiles.Count - 1) return;
        var nextTrack = AudioFiles[currentIndex + 1];

        SetCurrentTrack(nextTrack);
        TrackDataGrid.SelectedItem = nextTrack;
    }

    private void PlayPreviousTrack()
    {
        if (_currentTrack is null) return;
        var currentIndex = AudioFiles.IndexOf((AudioFileInfo)_currentTrack);

        if (currentIndex is 0 or -1) return;
        var previousTrack = AudioFiles[currentIndex - 1];

        SetCurrentTrack(previousTrack);
        TrackDataGrid.SelectedItem = previousTrack;
    }

    private void PlayRandomTrack()
    {
        if (AudioFiles.Count is 0 or 1 || AllTrackIsListened()) return;

        var rand = new Random();
        var randomIndex = rand.Next(AudioFiles.Count);

        if (AudioFiles.Count > 1 && _currentTrack != null)
        {   
            var currentIndex = AudioFiles.IndexOf((AudioFileInfo)_currentTrack);
            while (randomIndex == currentIndex || AudioFiles[randomIndex].IsListened == true)
            {
                randomIndex = rand.Next(AudioFiles.Count);
            }
        }

        var randomTrack = AudioFiles[randomIndex];
        SetCurrentTrack(randomTrack);
        TrackDataGrid.SelectedItem = randomTrack;
    }

    private bool AllTrackIsListened()
    {
        foreach (var file in AudioFiles)
        {
            if (file.IsListened == false) return false;
        }
        return true;
    }

    private void TrackEndModeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
    }

    private void InvisibleSliderButton_Click(object sender, RoutedEventArgs e)
    {
        var current = (int)TrackEndModeSlider.Value;
        var next = (current + 1) % 4;
        TrackEndModeSlider.Value = next;
        _currentMode = (PlaybackMode)next;

        TrackEndModeLabel.Content = next switch
        {
            0 => "Не повторять",
            1 => "Только этот трек",
            2 => "Все треки подряд",
            3 => "Случайный трек",
            _ => TrackEndModeLabel.Content
        };
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
    }
}