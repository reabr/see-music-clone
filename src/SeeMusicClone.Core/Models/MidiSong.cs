namespace SeeMusicClone.Core.Models;

public sealed class MidiSong
{
    public string FilePath { get; init; } = string.Empty;

    public string FileName => Path.GetFileName(FilePath);

    public IReadOnlyList<NoteEvent> Notes { get; init; } = Array.Empty<NoteEvent>();

    public double DurationSeconds { get; init; }
}
