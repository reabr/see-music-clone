using SkiaSharp;
using SeeMusicClone.Core.Models;

namespace SeeMusicClone.Core.Rendering;

public sealed class FrameRenderer
{
    public int Width { get; }
    public int Height { get; }
    public double NoteSpeedPixelsPerSecond { get; set; } = 220; // how fast notes fall
    public double KeyboardHeight { get; set; }

    private static readonly SKColor BackgroundColor = new(18, 18, 24);
    private static readonly SKColor WhiteKeyColor = new(250, 250, 250);
    private static readonly SKColor BlackKeyColor = new(20, 20, 20);
    private static readonly SKColor WhiteKeyActiveColor = new(120, 200, 255);
    private static readonly SKColor BlackKeyActiveColor = new(60, 140, 220);

    private static readonly SKColor[] NotePalette =
    {
        new(66, 165, 245), new(102, 187, 106), new(255, 167, 38),
        new(171, 71, 188), new(239, 83, 80), new(38, 198, 218)
    };

    public FrameRenderer(int width, int height)
    {
        Width = width;
        Height = height;
        KeyboardHeight = height * 0.18;
    }

    /// <summary>Renders a single frame at the given playback time and returns raw BGRA8888 bytes.</summary>
    public byte[] RenderFrame(IReadOnlyList<NoteEvent> notes, double timeSeconds)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(BackgroundColor);

        double fallAreaHeight = Height - KeyboardHeight;
        var keys = PianoLayout.Compute(Width, KeyboardHeight);
        var keyByNote = keys.ToDictionary(k => k.NoteNumber);

        var activeNotes = new HashSet<int>();

        using (var paint = new SKPaint { IsAntialias = true })
        {
            // Falling notes: a note reaches the keyboard (y = fallAreaHeight) exactly at its StartTime.
            foreach (var note in notes)
            {
                double secondsUntilHit = note.StartTimeSeconds - timeSeconds;
                double noteBottomY = fallAreaHeight - secondsUntilHit * NoteSpeedPixelsPerSecond;
                double noteHeightPx = note.DurationSeconds * NoteSpeedPixelsPerSecond;
                double noteTopY = noteBottomY - noteHeightPx;

                if (noteBottomY < 0 || noteTopY > fallAreaHeight) continue; // off-screen
                if (!keyByNote.TryGetValue(note.NoteNumber, out var key)) continue;

                if (timeSeconds >= note.StartTimeSeconds && timeSeconds <= note.EndTimeSeconds)
                    activeNotes.Add(note.NoteNumber);

                paint.Color = NotePalette[note.Channel % NotePalette.Length];
                var rect = new SKRect(
                    (float)(key.X + 1),
                    (float)Math.Max(0, noteTopY),
                    (float)(key.X + key.Width - 1),
                    (float)Math.Min(fallAreaHeight, noteBottomY));
                canvas.DrawRoundRect(rect, 4, 4, paint);
            }

            // Keyboard: white keys first, then black keys on top.
            double keyboardTop = fallAreaHeight;
            foreach (var key in keys.Where(k => !k.IsBlack))
            {
                paint.Color = activeNotes.Contains(key.NoteNumber) ? WhiteKeyActiveColor : WhiteKeyColor;
                canvas.DrawRect(new SKRect((float)key.X, (float)keyboardTop, (float)(key.X + key.Width - 1), (float)Height), paint);

                paint.Color = new SKColor(180, 180, 180);
                paint.Style = SKPaintStyle.Stroke;
                canvas.DrawRect(new SKRect((float)key.X, (float)keyboardTop, (float)(key.X + key.Width - 1), (float)Height), paint);
                paint.Style = SKPaintStyle.Fill;
            }

            foreach (var key in keys.Where(k => k.IsBlack))
            {
                paint.Color = activeNotes.Contains(key.NoteNumber) ? BlackKeyActiveColor : BlackKeyColor;
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
}
