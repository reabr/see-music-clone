using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
using NAudio.Wave;
using SeeMusicClone.Core.Models;

namespace SeeMusicClone.Core.Playback;

/// <summary>
/// Drives audio playback of a loaded MIDI file and exposes the current
/// playback position so the UI can drive the falling-notes animation in sync.
/// Speed is adjustable in real time (0.25x - 3x etc) via DryWetMidi's own Speed property,
/// which also correctly slows/speeds up the actual MIDI audio output.
/// </summary>
public sealed class PlaybackController : IDisposable
{
    private OutputDevice? _outputDevice;
    private Melanchall.DryWetMidi.Multimedia.Playback? _playback;
    private MidiFile? _midiFile;
    private AudioFileReader? _audioReader;
    private WaveOutEvent? _audioOutput;
    private double _speed = 1.0;

    public bool IsPlaying => _playback?.IsRunning == true || _audioOutput?.PlaybackState == PlaybackState.Playing;

    public bool SupportsVariableSpeed => _audioReader == null;

    public double Speed
    {
        get => _playback?.Speed ?? _speed;
        set
        {
            _speed = Math.Clamp(value, 0.1, 4.0);
            if (_playback != null)
                _playback.Speed = _speed;
        }
    }

    public double CurrentTimeSeconds
    {
        get
        {
            if (_playback != null)
            {
                var time = _playback.GetCurrentTime<MetricTimeSpan>();
                return time.TotalMicroseconds / 1_000_000.0;
            }

            return _audioReader?.CurrentTime.TotalSeconds ?? 0;
        }
        set
        {
            if (_playback != null)
            {
                var micros = (long)(value * 1_000_000);
                _playback.MoveToTime(new MetricTimeSpan(micros));
            }
            else if (_audioReader != null)
            {
                _audioReader.CurrentTime = TimeSpan.FromSeconds(Math.Clamp(value, 0, _audioReader.TotalTime.TotalSeconds));
            }
        }
    }

    /// <summary>Loads a MIDI or audio file and prepares the matching output device.</summary>
    public void Load(string filePath, int outputDeviceIndex = 0)
    {
        Stop();

        try
        {
            if (IsMidiFile(filePath))
            {
                _midiFile = MidiFile.Read(filePath);

                if (OutputDevice.GetAll().Any())
                    _outputDevice = OutputDevice.GetByIndex(outputDeviceIndex);

                _playback = _midiFile.GetPlayback(_outputDevice);
                _playback.Speed = _speed;
                return;
            }

            _audioReader = new AudioFileReader(filePath);
            _audioOutput = new WaveOutEvent();
            _audioOutput.Init(_audioReader);
        }
        catch
        {
            Stop();
            throw;
        }
    }

    public void Play()
    {
        _playback?.Start();
        _audioOutput?.Play();
    }

    public void Pause()
    {
        _playback?.Stop();
        _audioOutput?.Pause();
    }

    public void Stop()
    {
        _playback?.Stop();
        _playback?.Dispose();
        _playback = null;
        _midiFile = null;

        _outputDevice?.Dispose();
        _outputDevice = null;

        _audioOutput?.Stop();
        _audioOutput?.Dispose();
        _audioOutput = null;

        _audioReader?.Dispose();
        _audioReader = null;
    }

    public void Dispose() => Stop();

    private static bool IsMidiFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".mid", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".midi", StringComparison.OrdinalIgnoreCase);
    }
}
