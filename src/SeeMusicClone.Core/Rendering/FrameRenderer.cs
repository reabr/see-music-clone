using SkiaSharp;
using SeeMusicClone.Core.Models;

namespace SeeMusicClone.Core.Rendering;

public sealed class FrameRenderer
{
    public int Width { get; }
    public int Height { get; }
    public double NoteSpeedPixelsPerSecond { get; set; } = 140; // how fast notes fall
    public double KeyboardHeight { get; set; }

    private static readonly SKColor BackgroundColor = new(18, 18, 24);
    private static readonly SKColor WhiteKeyColor = new(250, 250, 250);
    private static readonly SKColor BlackKeyColor = new(20, 20, 20);

    public FrameRenderer(int width, int height)
    {
        Width = width;
        Height = height;
        KeyboardHeight = height * 0.18;
    }

    /// <summary>Renders a single frame at the given playback time and returns raw BGRA8888 bytes.</summary>
    public byte[] RenderFrame(IReadOnlyList<PianoNote> notes, double timeSeconds)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(BackgroundColor);

        double fallAreaHeight = Height - KeyboardHeight;
        var keys = PianoLayout.Compute(Width, KeyboardHeight);
        var keyByNote = keys.ToDictionary(k => k.NoteNumber);
        var nextSameKeyStartTimes = NoteRenderStyle.BuildNextSameKeyStartTimes(notes);
        var activeNotes = ActiveNoteCalculator.GetActiveNotes(notes, timeSeconds, NoteSpeedPixelsPerSecond, fallAreaHeight);

        using (var paint = new SKPaint { IsAntialias = true })
        {
            // Falling notes: a note reaches the keyboard (y = fallAreaHeight) exactly at its StartTime.
            foreach (var note in notes)
            {
                double secondsUntilHit = note.StartTimeSeconds - timeSeconds;
                double noteBottomY = fallAreaHeight - secondsUntilHit * NoteSpeedPixelsPerSecond;
                double fullHeightPx = note.DurationSeconds * NoteSpeedPixelsPerSecond;
                double noteTopY = noteBottomY - fullHeightPx;
                double visualNoteTopY = noteTopY + NoteRenderStyle.GetSameKeyGapTrim(note, nextSameKeyStartTimes, NoteSpeedPixelsPerSecond, fullHeightPx);

                if (noteBottomY < 0 || visualNoteTopY > fallAreaHeight) continue; // off-screen
                if (!keyByNote.TryGetValue(note.NoteNumber, out var key)) continue;

                var color = GetNoteColor(note.NoteNumber);
                double headHeightPx = Math.Min(fullHeightPx, NoteRenderStyle.MaxHeadSeconds * NoteSpeedPixelsPerSecond);
                double headTopY = noteBottomY - headHeightPx;
                double visualHeadTopY = Math.Max(headTopY, visualNoteTopY);

                // Long note: thin, dimmer sustain tail above the head, so it doesn't dwarf everything else.
                if (fullHeightPx > headHeightPx && visualNoteTopY < visualHeadTopY)
                {
                    paint.Color = color.WithAlpha((byte)(255 * NoteRenderStyle.TailOpacity));
                    double tailWidth = Math.Max(1, key.Width * NoteRenderStyle.TailWidthFraction);
                    double tailX = key.X + (key.Width - tailWidth) / 2;
                    var tailRect = new SKRect(
                        (float)tailX,
                        (float)Math.Max(0, visualNoteTopY),
                        (float)(tailX + tailWidth),
                        (float)Math.Min(fallAreaHeight, visualHeadTopY));
                    if (tailRect.Height > 0)
                        canvas.DrawRoundRect(tailRect, 2, 2, paint);
                }

                paint.Color = color;
                var headRect = new SKRect(
                    (float)(key.X + 1),
                    (float)Math.Max(0, visualHeadTopY),
                    (float)(key.X + key.Width - 1),
                    (float)Math.Min(fallAreaHeight, noteBottomY));
                if (headRect.Height > 0)
                    canvas.DrawRoundRect(headRect, 4, 4, paint);
            }

            // Keyboard: white keys first, then black keys on top.
            double keyboardTop = fallAreaHeight;
            foreach (var key in keys.Where(k => !k.IsBlack))
            {
                paint.Color = activeNotes.Contains(key.NoteNumber) ? GetNoteColor(key.NoteNumber) : WhiteKeyColor;
                canvas.DrawRect(new SKRect((float)key.X, (float)keyboardTop, (float)(key.X + key.Width - 1), (float)Height), paint);

                paint.Color = new SKColor(180, 180, 180);
                paint.Style = SKPaintStyle.Stroke;
                canvas.DrawRect(new SKRect((float)key.X, (float)keyboardTop, (float)(key.X + key.Width - 1), (float)Height), paint);
                paint.Style = SKPaintStyle.Fill;
            }

            foreach (var key in keys.Where(k => k.IsBlack))
            {
                paint.Color = activeNotes.Contains(key.NoteNumber) ? GetNoteColor(key.NoteNumber) : BlackKeyColor;
                canvas.DrawRect(new SKRect((float)key.X, (float)keyboardTop, (float)(key.X + key.Width), (float)(keyboardTop + key.Height)), paint);
            }
        }

        // Raw BGRA pixel bytes, ready to pipe into ffmpeg's rawvideo input.
        using var image = surface.Snapshot();
        using var pixmap = image.PeekPixels();
        var bytes = new byte[pixmap.BytesSize];
        System.Runtime.InteropServices.Marshal.Copy(pixmap.GetPixels(), bytes, 0, bytes.Length);
        return bytes;
    }

    private static SKColor GetNoteColor(int noteNumber)
    {
        var color = NoteColorPalette.GetRgb(noteNumber);
        return new SKColor(color.R, color.G, color.B);
    }
}
