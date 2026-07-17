using NAudio.Wave;
using SeeMusicClone.Core.Audio;
using SeeMusicClone.Core.Models;

namespace SeeMusicClone.Core.Tests.Audio;

public sealed class AudioFileTranscriberTests
{
    private const int SampleRate = 44100;

    [Fact]
    public void Transcribe_ConvertsWavMelodyIntoTimedPianoNotes()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"see-music-transcription-{Guid.NewGuid():N}.wav");

        try
        {
            WriteTestMelody(filePath);

            var song = new AudioFileTranscriber().Transcribe(filePath);

            Assert.Equal(SongSourceType.Audio, song.SourceType);
            Assert.InRange(song.DurationSeconds, 1.95, 2.05);
            Assert.Contains(song.Notes, note => note.NoteNumber == 69 && note.DurationSeconds >= 0.5);
            Assert.Contains(song.Notes, note => note.NoteNumber == 72 && note.DurationSeconds >= 0.5);
            Assert.All(song.Notes, note => Assert.True(note.StartTimeSeconds >= 0));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Transcribe_HonorsCancellation()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"see-music-transcription-{Guid.NewGuid():N}.wav");

        try
        {
            WriteTestMelody(filePath);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                new AudioFileTranscriber().Transcribe(filePath, cancellationToken: cancellation.Token));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static void WriteTestMelody(string filePath)
    {
        using var writer = new WaveFileWriter(filePath, new WaveFormat(SampleRate, 16, 1));
        WriteTone(writer, 440.0, 0.8);
        WriteSilence(writer, 0.2);
        WriteTone(writer, 523.251, 0.8);
        WriteSilence(writer, 0.2);
    }

    private static void WriteTone(WaveFileWriter writer, double frequencyHz, double durationSeconds)
    {
        var sampleCount = (int)(SampleRate * durationSeconds);
        for (var sample = 0; sample < sampleCount; sample++)
        {
            var fade = Math.Min(1.0, Math.Min(sample / 220.0, (sampleCount - sample - 1) / 220.0));
            writer.WriteSample((float)(0.6 * fade * Math.Sin(2 * Math.PI * frequencyHz * sample / SampleRate)));
        }
    }

    private static void WriteSilence(WaveFileWriter writer, double durationSeconds)
    {
        var sampleCount = (int)(SampleRate * durationSeconds);
        for (var sample = 0; sample < sampleCount; sample++)
            writer.WriteSample(0);
    }
}
