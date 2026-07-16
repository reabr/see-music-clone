using SeeMusicClone.Core.Models;

namespace SeeMusicClone.Core.Rendering;

public readonly struct KeyRect
{
    public int NoteNumber { get; init; }
    public double X { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public bool IsBlack { get; init; }
}

/// <summary>
/// Computes pixel positions for all 88 keys given a total width. Both the
/// PianoKeyboardControl (live) and the FrameRenderer (batch/video) use this
/// so the falling notes always line up with the keys exactly.
/// </summary>
public static class PianoLayout
{
    public static List<KeyRect> Compute(double totalWidth, double whiteKeyHeight, double blackKeyHeightRatio = 0.62)
    {
        var whiteKeys = new List<int>();
        for (int n = PianoLayoutHelper.LowestNote; n <= PianoLayoutHelper.HighestNote; n++)
            if (!PianoLayoutHelper.IsBlackKey(n)) whiteKeys.Add(n);

        double whiteKeyWidth = totalWidth / whiteKeys.Count;
        var result = new List<KeyRect>(PianoLayoutHelper.KeyCount);
        var whiteX = new Dictionary<int, double>();

        for (int i = 0; i < whiteKeys.Count; i++)
        {
            int note = whiteKeys[i];
            double x = i * whiteKeyWidth;
            whiteX[note] = x;
            result.Add(new KeyRect
            {
                NoteNumber = note,
                X = x,
                Width = whiteKeyWidth,
                Height = whiteKeyHeight,
                IsBlack = false
            });
        }

        double blackWidth = whiteKeyWidth * 0.62;
        for (int n = PianoLayoutHelper.LowestNote; n <= PianoLayoutHelper.HighestNote; n++)
        {
            if (!PianoLayoutHelper.IsBlackKey(n)) continue;

            // Black key sits between the previous and next white key, offset toward the next one.
            int prevWhite = n - 1;
            while (PianoLayoutHelper.IsBlackKey(prevWhite)) prevWhite--;

            double baseX = whiteX.TryGetValue(prevWhite, out var px) ? px : 0;
            double x = baseX + whiteKeyWidth - blackWidth / 2;

            result.Add(new KeyRect
            {
                NoteNumber = n,
                X = x,
                Width = blackWidth,
                Height = whiteKeyHeight * blackKeyHeightRatio,
                IsBlack = true
            });
        }

        return result;
    }
}
