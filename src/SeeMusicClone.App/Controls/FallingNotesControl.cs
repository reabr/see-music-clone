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
        nameof(Notes), typeof(IReadOnlyList<NoteEvent>), typeof(FallingNotesControl),
        new FrameworkPropertyMetadata(Array.Empty<NoteEvent>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CurrentTimeProperty = DependencyProperty.Register(
        nameof(CurrentTime), typeof(double), typeof(FallingNotesControl),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty NoteSpeedProperty = DependencyProperty.Register(
        nameof(NoteSpeed), typeof(double), typeof(FallingNotesControl),
        new FrameworkPropertyMetadata(220.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<NoteEvent> Notes
    {
        get => (IReadOnlyList<NoteEvent>)GetValue(NotesProperty);
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

    private static readonly Brush BackgroundBrush = new LinearGradientBrush(
        Color.FromRgb(14, 14, 20),
        Color.FromRgb(25, 25, 34),
        90);

    private static readonly Pen KeyGuidePen = new(new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)), 1);
    private static readonly Pen OctaveGuidePen = new(new SolidColorBrush(Color.FromArgb(72, 255, 255, 255)), 1);
    private static readonly Pen HitLinePen = new(new SolidColorBrush(Color.FromRgb(185, 210, 230)), 2);

    static FallingNotesControl()
    {
        BackgroundBrush.Freeze();
        KeyGuidePen.Freeze();
        OctaveGuidePen.Freeze();
        HitLinePen.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        double height = ActualHeight; // this control sits directly above the keyboard control
        if (width <= 0 || height <= 0) return;

        dc.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, width, height));

        var keys = PianoLayout.Compute(width, height); // reuse same key x/width geometry
        var keyByNote = keys.ToDictionary(k => k.NoteNumber);

        foreach (var key in keys.Where(k => !k.IsBlack))
        {
            var pen = key.NoteNumber % 12 == 0 ? OctaveGuidePen : KeyGuidePen;
            dc.DrawLine(pen, new Point(key.X, 0), new Point(key.X, height));
        }

        foreach (var note in Notes)
        {
            double secondsUntilHit = note.StartTimeSeconds - CurrentTime;
            double bottomY = height - secondsUntilHit * NoteSpeed;
            double noteHeightPx = note.DurationSeconds * NoteSpeed;
            double topY = bottomY - noteHeightPx;

            if (bottomY < 0 || topY > height) continue; // off-screen, skip
            if (!keyByNote.TryGetValue(note.NoteNumber, out var key)) continue;

            var brush = new SolidColorBrush(Palette[note.Channel % Palette.Length])
            {
                Opacity = 0.45 + Math.Clamp(note.Velocity, 1, 127) / 127.0 * 0.55
            };
            var rect = new Rect(
                key.X + 1,
                Math.Max(0, topY),
                Math.Max(1, key.Width - 2),
                Math.Min(height, bottomY) - Math.Max(0, topY));

            dc.DrawRoundedRectangle(brush, null, rect, 3, 3);
        }

        dc.DrawLine(HitLinePen, new Point(0, height - 1), new Point(width, height - 1));
    }
}
