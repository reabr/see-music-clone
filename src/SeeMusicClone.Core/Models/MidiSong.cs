namespace SeeMusicClone.Core.Models;

public sealed class MidiSong
{
    public string FilePath { get; init; } = string.Empty;

    public string FileName => Path.GetFileName(FilePath);

    public IReadOnlyList<PianoNote> Notes { get; init; } = Array.Empty<PianoNote>();

    public double DurationSeconds { get; init; }
}
