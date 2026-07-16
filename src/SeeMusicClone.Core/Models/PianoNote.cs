namespace SeeMusicClone.Core.Models;

/// <summary>
/// A single note, flattened out of a MIDI file, in absolute seconds.
/// </summary>
public sealed class PianoNote
{
    /// <summary>MIDI note number, 0-127. Piano range used here is 21 (A0) - 108 (C8), 88 keys.</summary>
    public int NoteNumber { get; init; }

    public double StartTimeSeconds { get; init; }

    public double DurationSeconds { get; init; }

    public double EndTimeSeconds => StartTimeSeconds + DurationSeconds;

    public int Velocity { get; init; }

    public int Channel { get; init; }

    public bool IsBlackKey => PianoLayoutHelper.IsBlackKey(NoteNumber);
}

/// <summary>Small static helper shared by layout/rendering code.</summary>
public static class PianoLayoutHelper
{
    private static readonly bool[] BlackKeyPattern =
    {
        false, true, false, true, false, false, true, false, true, false, true, false
    };

    public static bool IsBlackKey(int midiNoteNumber) => BlackKeyPattern[midiNoteNumber % 12];

    public const int LowestNote = 21;  // A0
    public const int HighestNote = 108; // C8
    public const int KeyCount = HighestNote - LowestNote + 1; // 88
}
