namespace SeeMusicClone.Core.Audio;

public sealed class PitchDetector
{
    private const double MinimumRms = 0.01;
    private const double YinThreshold = 0.15;
    private const double MinimumConfidence = 0.65;

    private readonly int _sampleRate;
    private readonly double _minimumFrequencyHz;
    private readonly double _maximumFrequencyHz;

    public PitchDetector(int sampleRate, double minimumFrequencyHz = 50.0, double maximumFrequencyHz = 2000.0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumFrequencyHz);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFrequencyHz);

        if (minimumFrequencyHz >= maximumFrequencyHz)
            throw new ArgumentException("Minimum frequency must be lower than maximum frequency.");

        _sampleRate = sampleRate;
        _minimumFrequencyHz = minimumFrequencyHz;
        _maximumFrequencyHz = maximumFrequencyHz;
    }

    public PitchDetectionResult? DetectPitch(IReadOnlyList<float> samples)
    {
        if (samples.Count < 3)
            return null;

        var mean = 0.0;
        for (var i = 0; i < samples.Count; i++)
            mean += samples[i];
        mean /= samples.Count;

        var rms = 0.0;
        for (var i = 0; i < samples.Count; i++)
        {
            var centered = samples[i] - mean;
            rms += centered * centered;
        }

        rms = Math.Sqrt(rms / samples.Count);
        if (rms < MinimumRms)
            return null;

        var minLag = Math.Max(2, (int)Math.Floor(_sampleRate / _maximumFrequencyHz));
        var maxLag = Math.Min(samples.Count - 2, (int)Math.Ceiling(_sampleRate / _minimumFrequencyHz));
        if (maxLag <= minLag)
            return null;

        var difference = new double[maxLag + 1];

        for (var lag = 1; lag <= maxLag; lag++)
        {
            var sum = 0.0;
            var sampleCount = samples.Count - lag;

            for (var i = 0; i < sampleCount; i++)
            {
                var delta = samples[i] - samples[i + lag];
                sum += delta * delta;
            }

            difference[lag] = sum;
        }

        ApplyCumulativeMeanNormalization(difference, maxLag);

        var bestLag = FindFundamentalLag(difference, minLag, maxLag);
        var confidence = 1.0 - difference[bestLag];
        if (confidence < MinimumConfidence)
            return null;

        var refinedLag = RefineLagWithParabolicInterpolation(difference, bestLag, minLag, maxLag);
        var frequencyHz = _sampleRate / refinedLag;
        if (frequencyHz < _minimumFrequencyHz || frequencyHz > _maximumFrequencyHz)
            return null;

        var midiNoteNumber = (int)Math.Round(69.0 + 12.0 * Math.Log2(frequencyHz / 440.0));
        if (midiNoteNumber is < 0 or > 127)
            return null;

        return new PitchDetectionResult
        {
            FrequencyHz = frequencyHz,
            MidiNoteNumber = midiNoteNumber,
            Confidence = Math.Clamp(confidence, 0.0, 1.0)
        };
    }

    private static void ApplyCumulativeMeanNormalization(double[] difference, int maximumLag)
    {
        difference[0] = 1.0;
        var runningSum = 0.0;

        for (var lag = 1; lag <= maximumLag; lag++)
        {
            runningSum += difference[lag];
            difference[lag] = runningSum <= double.Epsilon
                ? 1.0
                : difference[lag] * lag / runningSum;
        }
    }

    private static int FindFundamentalLag(IReadOnlyList<double> difference, int minimumLag, int maximumLag)
    {
        for (var lag = minimumLag; lag <= maximumLag; lag++)
        {
            if (difference[lag] >= YinThreshold)
                continue;

            while (lag < maximumLag && difference[lag + 1] < difference[lag])
                lag++;

            return lag;
        }

        var bestLag = minimumLag;
        for (var lag = minimumLag + 1; lag <= maximumLag; lag++)
        {
            if (difference[lag] < difference[bestLag])
                bestLag = lag;
        }

        return bestLag;
    }

    private static double RefineLagWithParabolicInterpolation(
        IReadOnlyList<double> difference,
        int lag,
        int minimumLag,
        int maximumLag)
    {
        if (lag <= minimumLag || lag >= maximumLag)
            return lag;

        var previous = difference[lag - 1];
        var current = difference[lag];
        var next = difference[lag + 1];
        var denominator = previous - 2.0 * current + next;

        if (Math.Abs(denominator) < 0.000001)
            return lag;

        var offset = 0.5 * (previous - next) / denominator;
        return lag + Math.Clamp(offset, -0.5, 0.5);
    }
}
