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

    private static readonly Color[] Palette =
    {
        Color.FromRgb(66, 165, 245), Color.FromRgb(102, 187, 106), Color.FromRgb(255, 167, 38),
        Color.FromRgb(171, 71, 188), Color.FromRgb(239, 83, 80), Color.FromRgb(38, 198, 218)
    };

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        double height = ActualHeight; // this control sits directly above the keyboard control
        if (width <= 0 || height <= 0) return;

        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(18, 18, 24)), null, new Rect(0, 0, width, height));

        var keys = PianoLayout.Compute(width, height); // reuse same key x/width geometry
        var keyByNote = keys.ToDictionary(k => k.NoteNumber);

        foreach (var note in Notes)
        {
            double secondsUntilHit = note.StartTimeSeconds - CurrentTime;
            double bottomY = height - secondsUntilHit * NoteSpeed;
            double fullHeightPx = note.DurationSeconds * NoteSpeed;
            double topY = bottomY - fullHeightPx;

            if (bottomY < 0 || topY > height) continue; // off-screen, skip
            if (!keyByNote.TryGetValue(note.NoteNumber, out var key)) continue;

            var color = Palette[note.Channel % Palette.Length];
            var headBrush = new SolidColorBrush(color);

            double headHeightPx = Math.Min(fullHeightPx, NoteRenderStyle.MaxHeadSeconds * NoteSpeed);
            double headTopY = bottomY - headHeightPx;

            if (fullHeightPx > headHeightPx)
            {
                var tailColor = Color.FromArgb((byte)(255 * NoteRenderStyle.TailOpacity), color.R, color.G, color.B);
                var tailBrush = new SolidColorBrush(tailColor);
                double tailWidth = Math.Max(1, key.Width * NoteRenderStyle.TailWidthFraction);
                double tailX = key.X + (key.Width - tailWidth) / 2;

                double tailY = Math.Max(0, topY);
                double tailHeight = Math.Min(height, headTopY) - tailY;

                if (tailHeight > 0)
                {
                    var tailRect = new Rect(tailX, tailY, tailWidth, tailHeight);
                    dc.DrawRoundedRectangle(tailBrush, null, tailRect, 2, 2);
                }
            }

            double headY = Math.Max(0, headTopY);
            double headHeight = Math.Min(height, bottomY) - headY;

            if (headHeight > 0)
            {
                var headRect = new Rect(
                    key.X + 1,
                    headY,
                    Math.Max(1, key.Width - 2),
                    headHeight);

                dc.DrawRoundedRectangle(headBrush, null, headRect, 3, 3);
            }
        }
    }
}
