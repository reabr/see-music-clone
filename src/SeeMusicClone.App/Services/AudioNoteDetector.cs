using NAudio.Wave;
using SeeMusicClone.Core.Audio;

namespace SeeMusicClone.App.Services;

public sealed class AudioNoteDetector : IDisposable
{
    private const int SampleRate = 44100;
    private const int FrameSize = 4096;
    private const int BytesPerSample = 2;
    private static readonly TimeSpan DetectionInterval = TimeSpan.FromMilliseconds(100);

    private readonly PitchDetector _pitchDetector = new(SampleRate);
    private readonly PitchTracker _pitchTracker = new();
    private readonly float[] _sampleFrame = new float[FrameSize];
    private readonly object _syncRoot = new();

    private WaveInEvent? _waveIn;
    private int _writeIndex;
    private int _samplesWritten;
    private DateTime _lastDetectionUtc = DateTime.MinValue;
    private bool _disposed;

    public event EventHandler<AudioPitchDetectedEventArgs>? PitchDetected;
    public event EventHandler<AudioCaptureStoppedEventArgs>? CaptureStopped;

    public bool IsListening
    {
        get
        {
            lock (_syncRoot)
                return _waveIn != null;
        }
    }

    public static IReadOnlyList<AudioInputDevice> GetInputDevices()
    {
        var devices = new List<AudioInputDevice>();
        for (var deviceNumber = 0; deviceNumber < WaveIn.DeviceCount; deviceNumber++)
        {
            var capabilities = WaveIn.GetCapabilities(deviceNumber);
            devices.Add(new AudioInputDevice(deviceNumber, capabilities.ProductName));
        }

        return devices;
    }

    public void Start(int deviceNumber)
    {
        WaveInEvent waveIn;

        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_waveIn != null)
                return;

            ResetDetectionState();

            waveIn = new WaveInEvent
            {
                DeviceNumber = deviceNumber,
                BufferMilliseconds = 40,
                WaveFormat = new WaveFormat(SampleRate, 16, 1)
            };

            waveIn.DataAvailable += OnDataAvailable;
            waveIn.RecordingStopped += OnRecordingStopped;
            _waveIn = waveIn;
        }

        try
        {
            waveIn.StartRecording();
        }
        catch
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_waveIn, waveIn))
                    _waveIn = null;
                ResetDetectionState();
            }

            ReleaseWaveIn(waveIn, stopRecording: false);
            throw;
        }
    }

    public void Stop()
    {
        WaveInEvent? waveIn;
        lock (_syncRoot)
        {
            waveIn = _waveIn;
            _waveIn = null;
            ResetDetectionState();
        }

        if (waveIn != null)
            ReleaseWaveIn(waveIn, stopRecording: true);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var shouldDetect = false;

        lock (_syncRoot)
        {
            if (!ReferenceEquals(sender, _waveIn))
                return;

            for (var i = 0; i + 1 < e.BytesRecorded; i += BytesPerSample)
            {
                var sample = BitConverter.ToInt16(e.Buffer, i) / 32768f;
                _sampleFrame[_writeIndex] = sample;
                _writeIndex = (_writeIndex + 1) % _sampleFrame.Length;
                _samplesWritten = Math.Min(FrameSize, _samplesWritten + 1);
            }

            shouldDetect = _samplesWritten >= FrameSize &&
                           DateTime.UtcNow - _lastDetectionUtc >= DetectionInterval;
        }

        if (!shouldDetect)
            return;

        float[] orderedFrame;
        lock (_syncRoot)
        {
            orderedFrame = new float[FrameSize];
            var tailLength = FrameSize - _writeIndex;
            Array.Copy(_sampleFrame, _writeIndex, orderedFrame, 0, tailLength);
            Array.Copy(_sampleFrame, 0, orderedFrame, tailLength, _writeIndex);
            _lastDetectionUtc = DateTime.UtcNow;
        }

        var rawPitch = _pitchDetector.DetectPitch(orderedFrame);
        var inputLevel = CalculateRms(orderedFrame);
        PitchDetectionResult? trackedPitch;

        lock (_syncRoot)
        {
            if (!ReferenceEquals(sender, _waveIn))
                return;

            trackedPitch = _pitchTracker.Update(rawPitch, DateTime.UtcNow);
        }

        PitchDetected?.Invoke(this, new AudioPitchDetectedEventArgs(trackedPitch, inputLevel));
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (sender is not WaveInEvent waveIn)
            return;

        lock (_syncRoot)
        {
            if (!ReferenceEquals(_waveIn, waveIn))
                return;

            _waveIn = null;
            ResetDetectionState();
        }

        ReleaseWaveIn(waveIn, stopRecording: false);
        CaptureStopped?.Invoke(this, new AudioCaptureStoppedEventArgs(e.Exception));
    }

    private void ResetDetectionState()
    {
        Array.Clear(_sampleFrame);
        _writeIndex = 0;
        _samplesWritten = 0;
        _lastDetectionUtc = DateTime.MinValue;
        _pitchTracker.Reset();
    }

    private static double CalculateRms(IReadOnlyList<float> samples)
    {
        var sum = 0.0;
        for (var i = 0; i < samples.Count; i++)
            sum += samples[i] * samples[i];

        return Math.Sqrt(sum / samples.Count);
    }

    private void ReleaseWaveIn(WaveInEvent waveIn, bool stopRecording)
    {
        waveIn.DataAvailable -= OnDataAvailable;
        waveIn.RecordingStopped -= OnRecordingStopped;

        try
        {
            if (stopRecording)
                waveIn.StopRecording();
        }
        finally
        {
            waveIn.Dispose();
        }
    }

    public void Dispose()
    {
        Stop();
        lock (_syncRoot)
            _disposed = true;
    }
}
