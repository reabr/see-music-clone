using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.Multimedia;
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

    public bool IsPlaying => _playback?.IsRunning ?? false;

    public double Speed
    {
        get => _playback?.Speed ?? 1.0;
        set { if (_playback != null) _playback.Speed = Math.Clamp(value, 0.1, 4.0); }
    }

    public double CurrentTimeSeconds
    {
        get
        {
            if (_playback == null) return 0;
            var time = _playback.GetCurrentTime<MetricTimeSpan>();
            return time.TotalMicroseconds / 1_000_000.0;
        }
        set
        {
            if (_playback == null) return;
            var micros = (long)(value * 1_000_000);
            _playback.MoveToTime(new MetricTimeSpan(micros));
        }
    }

    /// <summary>Loads a file and prepares an output device (index 0 = first available synth/MIDI device).</summary>
    public void Load(string filePath, int outputDeviceIndex = 0)
    {
        Stop();

        _midiFile = MidiFile.Read(filePath);

        if (OutputDevice.GetAll().Any())
            _outputDevice = OutputDevice.GetByIndex(outputDeviceIndex);

        _playback = _midiFile.GetPlayback(_outputDevice);
        _playback.Speed = 1.0;
    }

    public void Play() => _playback?.Start();

    public void Pause() => _playback?.Stop();

    public void Stop()
    {
        _playback?.Stop();
        _playback?.Dispose();
        _playback = null;

        _outputDevice?.Dispose();
        _outputDevice = null;
    }

    public void Dispose() => Stop();
}
