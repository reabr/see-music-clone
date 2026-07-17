using System.Windows;
using System.Windows.Media;
using SeeMusicClone.Core.Models;
using SeeMusicClone.Core.Rendering;

namespace SeeMusicClone.App.Controls;

/// <summary>
/// Draws notes falling toward the keyboard beneath it. A note reaches the keyboard
/// exactly at CurrentTime == note.StartTimeSeconds. NoteSpeed controls how many
/// pixels correspond to one second of fall time (bind this to a slider for
/// "adjustable note speed").
/// </summary>
public sealed class FallingNotesControl : FrameworkElement
{
    public static readonly DependencyProperty NotesProperty = DependencyProperty.Register(
        nameof(Notes), typeof(IReadOnlyList<PianoNote>), typeof(FallingNotesControl),
        new FrameworkPropertyMetadata(Array.Empty<PianoNote>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CurrentTimeProperty = DependencyProperty.Register(
        nameof(CurrentTime), typeof(double), typeof(FallingNotesControl),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty NoteSpeedProperty = DependencyProperty.Register(
        nameof(NoteSpeed), typeof(double), typeof(FallingNotesControl),
        new FrameworkPropertyMetadata(140.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<PianoNote> Notes
    {
        get => (IReadOnlyList<PianoNote>)GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    public double CurrentTime
    {
        get => (double)GetValue(CurrentTimeProperty);
        set => SetValue(CurrentTimeProperty, value);
    }

    /// <summary>Pixels of fall distance per second. Higher = faster falling notes.</summary>
    public double NoteSpeed
    {
        get => (double)GetValue(NoteSpeedProperty);
        set => SetValue(NoteSpeedProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0) return;

        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(18, 18, 24)), null, new Rect(0, 0, width, height));

        var keys = PianoLayout.Compute(width, height);
        var keyByNote = keys.ToDictionary(k => k.NoteNumber);
        var nextSameKeyStartTimes = NoteRenderStyle.BuildNextSameKeyStartTimes(Notes);
        foreach (var note in Notes)
        {
            double secondsUntilHit = note.StartTimeSeconds - CurrentTime;
            double bottomY = height - secondsUntilHit * NoteSpeed;
            double fullHeightPx = note.DurationSeconds * NoteSpeed;
            double topY = bottomY - fullHeightPx;
            topY += NoteRenderStyle.GetSameKeyGapTrim(note, nextSameKeyStartTimes, NoteSpeed, fullHeightPx);

            if (bottomY < 0 || topY > height) continue;
            if (!keyByNote.TryGetValue(note.NoteNumber, out var key)) continue;

            var noteColor = NoteColorPalette.GetRgb(note.NoteNumber);
            var baseColor = Color.FromRgb(noteColor.R, noteColor.G, noteColor.B);

            double noteY = Math.Max(0, topY);
            double noteBottom = Math.Min(height, bottomY);
            double noteHeight = noteBottom - noteY;
            if (noteHeight <= 0) continue;

            var noteRect = new Rect(key.X + 1, noteY, Math.Max(1, key.Width - 2), noteHeight);

            var gradientBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
        {
            new GradientStop(LightenColor(baseColor, 0.35), 0.0),
            new GradientStop(baseColor, 0.5),
            new GradientStop(DarkenColor(baseColor, 0.25), 1.0)
        }
            };

            dc.DrawRoundedRectangle(
                new SolidColorBrush(Color.FromArgb(60, baseColor.R, baseColor.G, baseColor.B)),
                null,
                Inflate(noteRect, 1),
                5, 5);

            dc.DrawRoundedRectangle(gradientBrush, null, noteRect, 3, 3);

            // Onset marker: a thin bright line at the exact moment this note starts,
            // so repeated/rapid notes on the same key are still individually readable
            // without visually chopping the bar into disconnected capsules.
            double onsetLineY = noteBottom - 2;
            if (onsetLineY > noteY)
            {
                var onsetPen = new Pen(new SolidColorBrush(LightenColor(baseColor, 0.7)), 1.5);
                dc.DrawLine(onsetPen, new Point(noteRect.X + 2, onsetLineY), new Point(noteRect.Right - 2, onsetLineY));
            }
        }
    }
    private static Color LightenColor(Color c, double amount)
    {
        return Color.FromRgb(
            (byte)Math.Min(255, c.R + (255 - c.R) * amount),
            (byte)Math.Min(255, c.G + (255 - c.G) * amount),
            (byte)Math.Min(255, c.B + (255 - c.B) * amount));
    }

    private static Color DarkenColor(Color c, double amount)
    {
        return Color.FromRgb(
            (byte)(c.R * (1 - amount)),
            (byte)(c.G * (1 - amount)),
            (byte)(c.B * (1 - amount)));
    }

    private static Rect Inflate(Rect r, double amount)
    {
        return new Rect(r.X - amount, r.Y - amount, r.Width + amount * 2, r.Height + amount * 2);
    }
}
