using SeeMusicClone.Core.Audio;

namespace SeeMusicClone.Core.Tests.Audio;

public sealed class PitchDetectorTests
{
    private const int SampleRate = 44100;
    private const int FrameSize = 4096;

    [Theory]
    [InlineData(110.0, 45)]
    [InlineData(220.0, 57)]
    [InlineData(440.0, 69)]
    [InlineData(523.251, 72)]
    [InlineData(1046.502, 84)]
    public void DetectPitch_RecognizesSingleNotes(double frequencyHz, int expectedMidiNote)
    {
        var detector = new PitchDetector(SampleRate);
        var samples = GenerateTone(frequencyHz);

        var result = detector.DetectPitch(samples);

        Assert.NotNull(result);
        Assert.Equal(expectedMidiNote, result.MidiNoteNumber);
        Assert.InRange(result.FrequencyHz, frequencyHz - 0.6, frequencyHz + 0.6);
        Assert.True(result.Confidence >= 0.9);
    }

    [Fact]
    public void DetectPitch_FindsFundamentalWhenSecondHarmonicIsStronger()
    {
        const double fundamentalHz = 196.0;
        var detector = new PitchDetector(SampleRate);
        var samples = Enumerable.Range(0, FrameSize)
            .Select(i =>
            {
                var time = (double)i / SampleRate;
                return (float)(0.22 * Math.Sin(2 * Math.PI * fundamentalHz * time) +
                               0.55 * Math.Sin(2 * Math.PI * fundamentalHz * 2 * time));
            })
            .ToArray();

        var result = detector.DetectPitch(samples);

        Assert.NotNull(result);
        Assert.Equal(55, result.MidiNoteNumber);
        Assert.InRange(result.FrequencyHz, 195.4, 196.6);
    }

    [Fact]
    public void DetectPitch_RejectsSilenceAndQuietInput()
    {
        var detector = new PitchDetector(SampleRate);

        Assert.Null(detector.DetectPitch(new float[FrameSize]));
        Assert.Null(detector.DetectPitch(GenerateTone(440, amplitude: 0.005)));
    }

    [Fact]
    public void DetectPitch_RejectsBroadbandNoise()
    {
        var detector = new PitchDetector(SampleRate);
        var random = new Random(12345);
        var samples = Enumerable.Range(0, FrameSize)
            .Select(_ => (float)((random.NextDouble() * 2 - 1) * 0.3))
            .ToArray();

        Assert.Null(detector.DetectPitch(samples));
    }

    [Fact]
    public void PitchDetectionResult_ReportsTuningOffset()
    {
        var result = CreatePitch(445.0, 69);

        Assert.Equal("A4", result.NoteName);
        Assert.Equal(20, result.CentsOffset);
    }

    private static float[] GenerateTone(double frequencyHz, double amplitude = 0.6)
    {
        return Enumerable.Range(0, FrameSize)
            .Select(i => (float)(amplitude * Math.Sin(2 * Math.PI * frequencyHz * i / SampleRate)))
            .ToArray();
    }

    private static PitchDetectionResult CreatePitch(double frequencyHz, int midiNoteNumber)
    {
        return new PitchDetectionResult
        {
            FrequencyHz = frequencyHz,
            MidiNoteNumber = midiNoteNumber,
            Confidence = 0.95
        };
    }
}
