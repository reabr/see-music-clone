using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using SeeMusicClone.Core.Batch;
using SeeMusicClone.Core.Midi;
using SeeMusicClone.Core.Models;
using SeeMusicClone.Core.Playback;

namespace SeeMusicClone.App.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly PlaybackController _playback = new();
    private readonly DispatcherTimer _uiTimer;

    private MidiSong? _song;
    public MidiSong? Song
    {
        get => _song;
        private set
        {
            if (SetField(ref _song, value))
                OnPropertyChanged(nameof(DurationDisplay));
        }
    }

    private double _currentTime;
    public double CurrentTime
    {
        get => _currentTime;
        set => SeekTo(value);
    }
    public string CurrentTimeDisplay => FormatTime(CurrentTime);
    public string DurationDisplay => FormatTime(Song?.DurationSeconds ?? 0);
    public double VisualTime => CurrentTime + AudioOffsetSeconds;

    private double _audioOffsetSeconds = -0.12; // negative = notes fall later, positive = notes fall earlier
    public double AudioOffsetSeconds
    {
        get => _audioOffsetSeconds;
        set
        {
            if (SetField(ref _audioOffsetSeconds, value))
                OnPropertyChanged(nameof(VisualTime));
        }
    }
    private double _noteSpeed = 140; // pixels/second, bound to a slider in the UI
    public double NoteSpeed
    {
        get => _noteSpeed;
        set => SetField(ref _noteSpeed, value);
    }

    private double _playbackSpeed = 1.0; // 0.25x - 2x actual audio/animation speed
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            if (SetField(ref _playbackSpeed, value))
                _playback.Speed = value;
        }
    }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetField(ref _isPlaying, value))
                OnPropertyChanged(nameof(PlayPauseText));
        }
    }
    public string PlayPauseText => IsPlaying ? "Pause" : "Play";

    private string _statusText = "Open a MIDI file to begin.";
    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public IReadOnlyList<PianoNote> Notes => Song?.Notes ?? Array.Empty<PianoNote>();

    public RelayCommand OpenFileCommand { get; }
    public RelayCommand PlayPauseCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ExportCurrentCommand { get; }
    public RelayCommand OpenBatchRenderCommand { get; }

    public event EventHandler? TimeAdvanced;

    public MainViewModel()
    {
        OpenFileCommand = new RelayCommand(_ => OpenFile());
        PlayPauseCommand = new RelayCommand(_ => TogglePlayPause(), _ => Song != null);
        StopCommand = new RelayCommand(_ => StopPlayback(), _ => Song != null);
        ExportCurrentCommand = new RelayCommand(_ => ExportCurrentVideo(), _ => Song != null && !IsExporting);
        OpenBatchRenderCommand = new RelayCommand(_ => OpenBatchRenderWindow());

        _uiTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0) // 60 fps redraw
        };
        _uiTimer.Tick += (_, _) => TickUi();
    }

    private void OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "MIDI files (*.mid;*.midi)|*.mid;*.midi|All files (*.*)|*.*",
            Title = "Open MIDI file"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            StopPlayback();
            Song = MidiLoader.Load(dialog.FileName);
            _playback.Load(dialog.FileName);
            _playback.Speed = PlaybackSpeed;
            SetCurrentTime(0);
            StatusText = $"Loaded {Song.FileName} — {Song.Notes.Count} notes, {Song.DurationSeconds:0.0}s";
            OnPropertyChanged(nameof(Notes));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not load file:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TogglePlayPause()
    {
        if (Song == null) return;

        if (IsPlaying)
        {
            _playback.Pause();
            _uiTimer.Stop();
            IsPlaying = false;
        }
        else
        {
            _playback.Play();
            _uiTimer.Start();
            IsPlaying = true;
        }
    }

    private void StopPlayback()
    {
        _playback.Stop();
        _uiTimer.Stop();
        IsPlaying = false;
        SetCurrentTime(0);
        if (Song != null) _playback.Load(Song.FilePath);
        _playback.Speed = PlaybackSpeed;
    }

    private void TickUi()
    {
        SetCurrentTime(_playback.CurrentTimeSeconds);
        TimeAdvanced?.Invoke(this, EventArgs.Empty);

        if (Song != null && CurrentTime >= Song.DurationSeconds)
        {
            _uiTimer.Stop();
            IsPlaying = false;
        }
    }

    private void OpenBatchRenderWindow()
    {
        var window = new Views.BatchRenderWindow();
        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();
    }

    private bool _isExporting;
    private bool IsExporting
    {
        get => _isExporting;
        set
        {
            if (SetField(ref _isExporting, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    private async void ExportCurrentVideo()
    {
        if (Song == null || IsExporting) return;
        var songToExport = Song;

        var dialog = new SaveFileDialog
        {
            Filter = "MP4 video (*.mp4)|*.mp4|All files (*.*)|*.*",
            Title = "Export current MIDI as video",
            FileName = $"{Path.GetFileNameWithoutExtension(songToExport.FileName)}.mp4",
            DefaultExt = ".mp4",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true) return;

        IsExporting = true;
        StatusText = "Exporting video...";

        var outputFolder = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
        var options = new BatchRenderOptions
        {
            Width = 1920,
            Height = 1080,
            Fps = 30,
            NoteSpeedPixelsPerSecond = NoteSpeed,
            OutputFolder = outputFolder
        };

        var progress = new Progress<BatchRenderProgress>(p =>
        {
            StatusText = p.ErrorMessage == null
                ? $"Exporting {p.FileName}: {p.FractionComplete:P0}"
                : $"Export failed: {p.ErrorMessage}";
        });

        try
        {
            var renderer = new BatchRenderer();
            await Task.Run(() => renderer.RenderFileAsync(songToExport.FilePath, dialog.FileName, options, progress));
            StatusText = $"Exported {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            StatusText = "Export failed.";
            MessageBox.Show($"Export failed:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsExporting = false;
        }
    }

    public void Dispose()
    {
        _uiTimer.Stop();
        _playback.Dispose();
    }

    private void SeekTo(double seconds)
    {
        if (Song == null)
        {
            SetCurrentTime(0);
            return;
        }

        var clampedSeconds = Math.Clamp(seconds, 0, Song.DurationSeconds);
        _playback.CurrentTimeSeconds = clampedSeconds;
        SetCurrentTime(clampedSeconds);
        TimeAdvanced?.Invoke(this, EventArgs.Empty);
    }

    private void SetCurrentTime(double seconds)
    {
        if (SetField(ref _currentTime, seconds))
        {
            OnPropertyChanged(nameof(CurrentTimeDisplay));
            OnPropertyChanged(nameof(VisualTime));
        }
    }

    private static string FormatTime(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
            seconds = 0;

        var time = TimeSpan.FromSeconds(seconds);
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";
    }
}
