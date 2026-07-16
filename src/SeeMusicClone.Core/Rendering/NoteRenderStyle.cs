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
    /// <summary>Notes up to this length render as a normal solid block.</summary>
    public const double MaxHeadSeconds = 1.2;

    /// <summary>Width of the sustain tail, as a fraction of the key width.</summary>
    public const double TailWidthFraction = 0.34;

    /// <summary>Opacity (0-1) applied to the tail so it visually recedes behind the head.</summary>
    public const double TailOpacity = 0.55;
}
