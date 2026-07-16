using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
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
        private set => SetField(ref _song, value);
    }

    private double _currentTime;
    public double CurrentTime
    {
        get => _currentTime;
        private set => SetField(ref _currentTime, value);
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
        private set => SetField(ref _isPlaying, value);
    }

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
    public RelayCommand OpenBatchRenderCommand { get; }

    public event EventHandler? TimeAdvanced;

    public MainViewModel()
    {
        OpenFileCommand = new RelayCommand(_ => OpenFile());
        PlayPauseCommand = new RelayCommand(_ => TogglePlayPause(), _ => Song != null);
        StopCommand = new RelayCommand(_ => StopPlayback(), _ => Song != null);
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
            CurrentTime = 0;
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
        CurrentTime = 0;
        if (Song != null) _playback.Load(Song.FilePath);
        _playback.Speed = PlaybackSpeed;
    }

    private void TickUi()
    {
        CurrentTime = _playback.CurrentTimeSeconds;
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

    public void Dispose()
    {
        _uiTimer.Stop();
        _playback.Dispose();
    }
}
