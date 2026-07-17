namespace SeeMusicClone.Core.Rendering;

public static class NoteColorPalette
{
    public const int HandSplitNoteNumber = 60;

    public static (byte R, byte G, byte B) GetRgb(int noteNumber)
    {
        return noteNumber >= HandSplitNoteNumber
            ? ((byte)79, (byte)195, (byte)247)
            : ((byte)255, (byte)138, (byte)101);
    }
}
