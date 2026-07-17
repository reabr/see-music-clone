using SeeMusicClone.Core.Audio;

namespace SeeMusicClone.App.Services;

public sealed class AudioPitchDetectedEventArgs : EventArgs
{
    public AudioPitchDetectedEventArgs(PitchDetectionResult? pitch, double inputLevel)
    {
        Pitch = pitch;
        InputLevel = Math.Clamp(inputLevel, 0.0, 1.0);
    }

    public PitchDetectionResult? Pitch { get; }

    public double InputLevel { get; }
}
