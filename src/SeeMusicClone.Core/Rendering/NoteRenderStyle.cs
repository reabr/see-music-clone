using SeeMusicClone.Core.Models;

namespace SeeMusicClone.Core.Rendering;

/// <summary>
/// A note whose duration is longer than <see cref="MaxHeadSeconds"/> is drawn as a
/// fixed-size solid "head" (the readable, full-width block) plus a thin, dimmer
/// "tail" above it representing the remaining sustain — instead of one giant
/// full-width block that would dwarf everything else on screen for long
/// pedaled/sustained chords. Short notes are unaffected and render as before.
/// </summary>
public static class NoteRenderStyle
{
    public const double MaxHeadSeconds = 1.2;
    public const double TailWidthFraction = 0.18;  // was 0.34 — thinner, more clearly secondary
    public const double TailOpacity = 0.35;         // was 0.55 — recedes further behind the head
    public const double MinSameKeyGapPixels = 6.0;

    public static Dictionary<PianoNote, double> BuildNextSameKeyStartTimes(IReadOnlyList<PianoNote> notes)
    {
        var nextStartTimes = new Dictionary<PianoNote, double>();

        foreach (var group in notes.GroupBy(n => n.NoteNumber))
        {
            var sameKeyNotes = group.OrderBy(n => n.StartTimeSeconds).ToArray();
            for (int i = 0; i < sameKeyNotes.Length - 1; i++)
                nextStartTimes[sameKeyNotes[i]] = sameKeyNotes[i + 1].StartTimeSeconds;
        }

        return nextStartTimes;
    }

    public static double GetSameKeyGapTrim(
        PianoNote note,
        IReadOnlyDictionary<PianoNote, double> nextSameKeyStartTimes,
        double noteSpeed,
        double fullHeightPx)
    {
        if (!nextSameKeyStartTimes.TryGetValue(note, out double nextStartTime))
            return 0;

        double existingGapPx = (nextStartTime - note.EndTimeSeconds) * noteSpeed;
        double trimPx = MinSameKeyGapPixels - existingGapPx;
        if (trimPx <= 0)
            return 0;

        return Math.Min(trimPx, Math.Max(0, fullHeightPx - 1));
    }
}
