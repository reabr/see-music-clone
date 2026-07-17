using System.IO;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using SeeMusicClone.App.Services;
using SeeMusicClone.Core.Batch;
using SeeMusicClone.Core.Audio;
using SeeMusicClone.Core.Midi;
using SeeMusicClone.Core.Models;
using SeeMusicClone.Core.Playback;

namespace SeeMusicClone.App.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly PlaybackController _playback = new();
    private readonly AudioNoteDetector _audioNoteDetector = new();
    private readonly DispatcherTimer _uiTimer;
    private CancellationTokenSource? _audioTranscriptionCts;

    private MidiSong? _song;
    public MidiSong? Song
    {
        get => _song;
        private set
        {
            if (SetField(ref _song, value))
            {
                OnPropertyChanged(nameof(DurationDisplay));
                OnPropertyChanged(nameof(CanChangePlaybackSpeed));
                CommandManager.InvalidateRequerySuggested();
            }
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
    public bool CanChangePlaybackSpeed => !IsAnalyzingAudio && Song?.SourceType != SongSourceType.Audio;

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

    private bool _isListeningForAudio;
    public bool IsListeningForAudio
    {
        get => _isListeningForAudio;
        private set
        {
            if (SetField(ref _isListeningForAudio, value))
            {
                OnPropertyChanged(nameof(ListenButtonText));
                OnPropertyChanged(nameof(CanSelectAudioInputDevice));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string ListenButtonText => IsListeningForAudio ? "Stop Listening" : "Start Listening";
    public bool CanSelectAudioInputDevice => !IsListeningForAudio;

    public ObservableCollection<AudioInputDevice> AudioInputDevices { get; } = new();

    private AudioInputDevice? _selectedAudioInputDevice;
    public AudioInputDevice? SelectedAudioInputDevice
    {
        get => _selectedAudioInputDevice;
        set
        {
            if (SetField(ref _selectedAudioInputDevice, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    private PitchDetectionResult? _detectedPitch;
    public PitchDetectionResult? DetectedPitch
    {
        get => _detectedPitch;
        private set
        {
            if (SetField(ref _detectedPitch, value))
            {
                OnPropertyChanged(nameof(DetectedNoteNumber));
                OnPropertyChanged(nameof(DetectedNoteDisplay));
                OnPropertyChanged(nameof(DetectedFrequencyDisplay));
                OnPropertyChanged(nameof(DetectedConfidenceDisplay));
                OnPropertyChanged(nameof(DetectedCentsDisplay));
                OnPropertyChanged(nameof(DetectedTuningDisplay));
            }
        }
    }

    public int? DetectedNoteNumber => DetectedPitch?.MidiNoteNumber;
    public string DetectedNoteDisplay => DetectedPitch?.NoteName ?? "--";
    public string DetectedFrequencyDisplay => DetectedPitch == null ? "-- Hz" : $"{DetectedPitch.FrequencyHz:0.0} Hz";
    public string DetectedConfidenceDisplay => DetectedPitch == null ? "--" : $"{DetectedPitch.Confidence:P0}";
    public string DetectedCentsDisplay => DetectedPitch == null ? "-- cents" : $"{DetectedPitch.CentsOffset:+0;-0;0} cents";
    public string DetectedTuningDisplay => DetectedPitch switch
    {
        null => "No pitch",
        { CentsOffset: < -5 } => "Flat",
        { CentsOffset: > 5 } => "Sharp",
        _ => "In tune"
    };

    private double _inputLevel;
    public double InputLevel
    {
        get => _inputLevel;
        private set => SetField(ref _inputLevel, value);
    }

    private string _statusText = "Open a MIDI or audio file to begin.";
    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public IReadOnlyList<PianoNote> Notes => Song?.Notes ?? Array.Empty<PianoNote>();

    private bool _isAnalyzingAudio;
    public bool IsAnalyzingAudio
    {
        get => _isAnalyzingAudio;
        private set
        {
            if (SetField(ref _isAnalyzingAudio, value))
            {
                OnPropertyChanged(nameof(OpenFileText));
                OnPropertyChanged(nameof(CanChangePlaybackSpeed));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string OpenFileText => IsAnalyzingAudio ? "Cancel Analysis" : "Open File";

    public RelayCommand OpenFileCommand { get; }
    public RelayCommand PlayPauseCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand SeekBackwardCommand { get; }
    public RelayCommand SeekForwardCommand { get; }
    public RelayCommand ExportCurrentCommand { get; }
    public RelayCommand OpenBatchRenderCommand { get; }
    public RelayCommand ToggleAudioDetectionCommand { get; }
    public RelayCommand RefreshAudioDevicesCommand { get; }

    public event EventHandler? TimeAdvanced;

    public MainViewModel()
    {
        OpenFileCommand = new RelayCommand(_ => OpenFile());
        PlayPauseCommand = new RelayCommand(_ => TogglePlayPause(), _ => Song != null && !IsAnalyzingAudio);
        StopCommand = new RelayCommand(_ => StopPlayback(), _ => Song != null && !IsAnalyzingAudio);
        SeekBackwardCommand = new RelayCommand(_ => SeekBy(-5), _ => Song != null && !IsAnalyzingAudio);
        SeekForwardCommand = new RelayCommand(_ => SeekBy(5), _ => Song != null && !IsAnalyzingAudio);
        ExportCurrentCommand = new RelayCommand(
            _ => ExportCurrentVideo(),
            _ => Song?.SourceType == SongSourceType.Midi && !IsExporting && !IsAnalyzingAudio);
        OpenBatchRenderCommand = new RelayCommand(
            _ => OpenBatchRenderWindow(),
            _ => !IsAnalyzingAudio);
        ToggleAudioDetectionCommand = new RelayCommand(
            _ => ToggleAudioDetection(),
            _ => IsListeningForAudio || (!IsAnalyzingAudio && SelectedAudioInputDevice != null));
        RefreshAudioDevicesCommand = new RelayCommand(
            _ => RefreshAudioInputDevices(),
            _ => !IsListeningForAudio && !IsAnalyzingAudio);

        RefreshAudioInputDevices();

        _audioNoteDetector.PitchDetected += OnAudioPitchDetected;
        _audioNoteDetector.CaptureStopped += OnAudioCaptureStopped;

        _uiTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0) // 60 fps redraw
        };
        _uiTimer.Tick += (_, _) => TickUi();
    }

    private void ToggleAudioDetection()
    {
        try
        {
            if (IsListeningForAudio)
            {
                _audioNoteDetector.Stop();
                IsListeningForAudio = false;
                DetectedPitch = null;
                InputLevel = 0;
                StatusText = GetIdleStatusText();
                return;
            }

            if (SelectedAudioInputDevice == null)
                return;

            _audioNoteDetector.Start(SelectedAudioInputDevice.DeviceNumber);
            IsListeningForAudio = true;
            StatusText = $"Listening on {SelectedAudioInputDevice.Name}.";
        }
        catch (Exception ex)
        {
            _audioNoteDetector.Stop();
            IsListeningForAudio = false;
            DetectedPitch = null;
            InputLevel = 0;
            MessageBox.Show($"Could not start audio detection:\n{ex.Message}", "Audio detection",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText = "Audio detection could not be started.";
        }
    }

    private void RefreshAudioInputDevices()
    {
        try
        {
            var previousName = SelectedAudioInputDevice?.Name;
            var devices = AudioNoteDetector.GetInputDevices();

            AudioInputDevices.Clear();
            foreach (var device in devices)
                AudioInputDevices.Add(device);

            SelectedAudioInputDevice = AudioInputDevices.FirstOrDefault(d => d.Name == previousName)
                                       ?? AudioInputDevices.FirstOrDefault();
            StatusText = SelectedAudioInputDevice == null
                ? "No microphone input was found."
                : GetIdleStatusText();
            CommandManager.InvalidateRequerySuggested();
        }
        catch (Exception ex)
        {
            StatusText = $"Could not enumerate microphones: {ex.Message}";
        }
    }

    private void OnAudioPitchDetected(object? sender, AudioPitchDetectedEventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            DetectedPitch = e.Pitch;
            InputLevel = Math.Clamp(e.InputLevel * 4.0, 0.0, 1.0);

            if (IsListeningForAudio && e.Pitch != null)
            {
                StatusText = $"Detected {e.Pitch.NoteName}: {DetectedTuningDisplay.ToLowerInvariant()} " +
                             $"({e.Pitch.CentsOffset:+0;-0;0} cents).";
            }
        });
    }

    private void OnAudioCaptureStopped(object? sender, AudioCaptureStoppedEventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            IsListeningForAudio = false;
            DetectedPitch = null;
            InputLevel = 0;

            if (e.Error == null)
            {
                StatusText = GetIdleStatusText();
                return;
            }

            StatusText = $"Microphone stopped: {e.Error.Message}";
            MessageBox.Show($"Microphone capture stopped:\n{e.Error.Message}", "Audio detection",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        });
    }

    private async void OpenFile()
    {
        if (_audioTranscriptionCts != null)
        {
            _audioTranscriptionCts.Cancel();
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Music files|*.mid;*.midi;*.wav;*.mp3;*.aiff;*.aif;*.wma;*.m4a;*.aac|" +
                     "MIDI files (*.mid;*.midi)|*.mid;*.midi|" +
                     "Audio files|*.wav;*.mp3;*.aiff;*.aif;*.wma;*.m4a;*.aac|All files (*.*)|*.*",
            Title = "Open MIDI or audio file"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            if (IsListeningForAudio)
            {
                _audioNoteDetector.Stop();
                IsListeningForAudio = false;
                DetectedPitch = null;
                InputLevel = 0;
            }

            if (IsMidiFile(dialog.FileName))
            {
                LoadSong(MidiLoader.Load(dialog.FileName));
                return;
            }

            if (IsPlaying)
            {
                _playback.Pause();
                _uiTimer.Stop();
                IsPlaying = false;
            }

            using var cancellation = new CancellationTokenSource();
            _audioTranscriptionCts = cancellation;
            IsAnalyzingAudio = true;
            var progress = new Progress<double>(value =>
                StatusText = $"Analyzing {Path.GetFileName(dialog.FileName)}: {value:P0}");
            var transcriber = new AudioFileTranscriber();
            var song = await Task.Run(() =>
                transcriber.Transcribe(dialog.FileName, progress, cancellation.Token));

            LoadSong(song);
        }
        catch (OperationCanceledException)
        {
            StatusText = GetIdleStatusText();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not load file:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText = GetIdleStatusText();
        }
        finally
        {
            _audioTranscriptionCts = null;
            IsAnalyzingAudio = false;
        }
    }

    private void LoadSong(MidiSong song)
    {
        _playback.Stop();
        _uiTimer.Stop();
        IsPlaying = false;
        _playback.Load(song.FilePath);
        Song = song;

        if (song.SourceType == SongSourceType.Audio)
            PlaybackSpeed = 1.0;
        else
            _playback.Speed = PlaybackSpeed;

        SetCurrentTime(0);
        StatusText = $"Loaded {song.FileName} — {song.Notes.Count} notes, {song.DurationSeconds:0.0}s";
        OnPropertyChanged(nameof(Notes));
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
            if (CurrentTime >= Song.DurationSeconds - 0.01)
                SeekTo(0);

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
        if (Song?.SourceType != SongSourceType.Midi || IsExporting) return;
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
        _audioTranscriptionCts?.Cancel();
        _uiTimer.Stop();
        _audioNoteDetector.PitchDetected -= OnAudioPitchDetected;
        _audioNoteDetector.CaptureStopped -= OnAudioCaptureStopped;
        _audioNoteDetector.Dispose();
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

    private void SeekBy(double seconds)
    {
        SeekTo(CurrentTime + seconds);
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

    private string GetIdleStatusText()
    {
        if (Song != null)
            return $"Loaded {Song.FileName} — {Song.Notes.Count} notes, {Song.DurationSeconds:0.0}s";

        return AudioInputDevices.Count == 0
            ? "No microphone input was found."
            : "Open a MIDI or audio file to begin.";
    }

    private static bool IsMidiFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".mid", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".midi", StringComparison.OrdinalIgnoreCase);
    }
}
