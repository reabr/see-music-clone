using SeeMusicClone.Core.Models;

namespace SeeMusicClone.Core.Rendering;

public static class ActiveNoteCalculator
{
    public static HashSet<int> GetActiveNotes(
        IReadOnlyList<PianoNote> notes,
        double currentTimeSeconds,
        double noteSpeedPixelsPerSecond,
        double fallAreaHeight)
    {
        var active = new HashSet<int>();
        if (noteSpeedPixelsPerSecond <= 0 || fallAreaHeight <= 0)
            return active;

        var nextSameKeyStartTimes = NoteRenderStyle.BuildNextSameKeyStartTimes(notes);

        foreach (var note in notes)
        {
            double secondsUntilHit = note.StartTimeSeconds - currentTimeSeconds;
            double bottomY = fallAreaHeight - secondsUntilHit * noteSpeedPixelsPerSecond;
            double fullHeightPx = note.DurationSeconds * noteSpeedPixelsPerSecond;
            double topY = bottomY - fullHeightPx;
            topY += NoteRenderStyle.GetSameKeyGapTrim(note, nextSameKeyStartTimes, noteSpeedPixelsPerSecond, fullHeightPx);

            if (bottomY >= fallAreaHeight && topY <= fallAreaHeight)
                active.Add(note.NoteNumber);
        }

        return active;
    }
}
