namespace SeeMusicClone.Core.Audio;

public sealed class PitchDetectionResult
{
    private static readonly string[] NoteNames =
    {
        "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"
    };

    public required double FrequencyHz { get; init; }

    public required int MidiNoteNumber { get; init; }

    public required double Confidence { get; init; }

    public double ExactMidiNoteNumber => 69.0 + 12.0 * Math.Log2(FrequencyHz / 440.0);

    public int CentsOffset => (int)Math.Round((ExactMidiNoteNumber - MidiNoteNumber) * 100.0);

    public string NoteName => $"{NoteNames[MidiNoteNumber % 12]}{MidiNoteNumber / 12 - 1}";
}
