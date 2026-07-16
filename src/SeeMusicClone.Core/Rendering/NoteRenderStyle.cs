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
}
