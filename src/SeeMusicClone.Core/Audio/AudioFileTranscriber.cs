using NAudio.Wave;
using SeeMusicClone.Core.Models;

namespace SeeMusicClone.Core.Audio;

public sealed class AudioFileTranscriber
{
    private const double MinimumFrequencyHz = 55.0;
    private const double MaximumFrequencyHz = 2000.0;
    private const double HopDurationSeconds = 0.05;
    private const double MinimumNoteDurationSeconds = 0.08;

    public MidiSong Transcribe(
        string filePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var reader = new AudioFileReader(filePath);
        var sampleRate = reader.WaveFormat.SampleRate;
        var channels = reader.WaveFormat.Channels;
        var durationSeconds = reader.TotalTime.TotalSeconds;
        var frameSize = Math.Max(2048, (int)Math.Round(sampleRate * 0.06));
        var hopSize = Math.Max(1, (int)Math.Round(sampleRate * HopDurationSeconds));
        var detector = new PitchDetector(sampleRate, MinimumFrequencyHz, MaximumFrequencyHz);
        var tracker = new PitchTracker(2, TimeSpan.FromSeconds(HopDurationSeconds * 1.25));
        var notes = new List<PianoNote>();
        var frame = new float[frameSize];
        var interleavedBuffer = new float[frameSize * channels];

        var available = ReadMonoSamples(reader, frame, 0, frameSize, interleavedBuffer, channels);
        long frameStartSample = 0;
        int? activeNoteNumber = null;
        double activeStart = 0;
        double activeEnd = 0;
        var activeVelocity = 0;

        while (available >= Math.Min(frameSize, sampleRate / 20))
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<float> detectionSamples = available == frameSize
                ? frame
                : frame[..available];
            var rawPitch = detector.DetectPitch(detectionSamples);
            var frameTime = (double)frameStartSample / sampleRate;
            var trackedPitch = tracker.Update(
                rawPitch,
                DateTime.UnixEpoch.AddSeconds(frameTime));
            var noteNumber = trackedPitch?.MidiNoteNumber;
            if (noteNumber is < PianoLayoutHelper.LowestNote or > PianoLayoutHelper.HighestNote)
                noteNumber = null;

            var frameEnd = Math.Min(durationSeconds, frameTime + HopDurationSeconds);
            var velocity = CalculateVelocity(detectionSamples);

            if (noteNumber == activeNoteNumber)
            {
                if (noteNumber != null)
                {
                    activeEnd = frameEnd;
                    activeVelocity = Math.Max(activeVelocity, velocity);
                }
            }
            else
            {
                AddCompletedNote(notes, activeNoteNumber, activeStart, activeEnd, activeVelocity);
                activeNoteNumber = noteNumber;
                activeStart = frameTime;
                activeEnd = frameEnd;
                activeVelocity = velocity;
            }

            progress?.Report(durationSeconds <= 0
                ? 1.0
                : Math.Clamp(frameTime / durationSeconds, 0.0, 1.0));

            if (available <= hopSize)
                break;

            var remaining = available - hopSize;
            Array.Copy(frame, hopSize, frame, 0, remaining);
            var read = ReadMonoSamples(
                reader,
                frame,
                remaining,
                frameSize - remaining,
                interleavedBuffer,
                channels);
            available = remaining + read;
            frameStartSample += hopSize;
        }

        AddCompletedNote(notes, activeNoteNumber, activeStart, activeEnd, activeVelocity);
        progress?.Report(1.0);

        return new MidiSong
        {
            FilePath = filePath,
            Notes = notes,
            DurationSeconds = durationSeconds,
            SourceType = SongSourceType.Audio
        };
    }

    private static int ReadMonoSamples(
        AudioFileReader reader,
        float[] destination,
        int destinationOffset,
        int sampleCount,
        float[] interleavedBuffer,
        int channels)
    {
        var monoSamplesRead = 0;

        while (monoSamplesRead < sampleCount)
        {
            var framesRequested = sampleCount - monoSamplesRead;
            var valuesRead = reader.Read(interleavedBuffer, 0, framesRequested * channels);
            var framesRead = valuesRead / channels;
            if (framesRead == 0)
                break;

            for (var frameIndex = 0; frameIndex < framesRead; frameIndex++)
            {
                var sum = 0.0f;
                var sourceOffset = frameIndex * channels;
                for (var channel = 0; channel < channels; channel++)
                    sum += interleavedBuffer[sourceOffset + channel];

                destination[destinationOffset + monoSamplesRead + frameIndex] = sum / channels;
            }

            monoSamplesRead += framesRead;
        }

        return monoSamplesRead;
    }

    private static int CalculateVelocity(IReadOnlyList<float> samples)
    {
        var sum = 0.0;
        for (var i = 0; i < samples.Count; i++)
            sum += samples[i] * samples[i];

        var rms = Math.Sqrt(sum / samples.Count);
        return (int)Math.Clamp(Math.Round(35 + rms * 180), 35, 120);
    }

    private static void AddCompletedNote(
        ICollection<PianoNote> notes,
        int? noteNumber,
        double startTime,
        double endTime,
        int velocity)
    {
        if (noteNumber == null || endTime - startTime < MinimumNoteDurationSeconds)
            return;

        notes.Add(new PianoNote
        {
            NoteNumber = noteNumber.Value,
            StartTimeSeconds = startTime,
            DurationSeconds = endTime - startTime,
            Velocity = velocity,
            Channel = 0
        });
    }
}
