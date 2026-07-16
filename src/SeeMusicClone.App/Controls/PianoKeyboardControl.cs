using System.Windows;
using System.Windows.Media;
using SeeMusicClone.Core.Models;
using SeeMusicClone.Core.Rendering;

namespace SeeMusicClone.App.Controls;

/// <summary>
/// Draws all 88 piano keys and highlights the ones that are currently sounding.
/// Uses the shared PianoLayout so key positions line up exactly with FallingNotesControl.
/// </summary>
public sealed class PianoKeyboardControl : FrameworkElement
{
    public static readonly DependencyProperty ActiveNotesProperty = DependencyProperty.Register(
        nameof(ActiveNotes), typeof(HashSet<int>), typeof(PianoKeyboardControl),
        new FrameworkPropertyMetadata(new HashSet<int>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public HashSet<int> ActiveNotes
    {
        get => (HashSet<int>)GetValue(ActiveNotesProperty);
        set => SetValue(ActiveNotesProperty, value);
    }

    private static readonly Brush WhiteKeyBrush = new SolidColorBrush(Color.FromRgb(250, 250, 250));
    private static readonly Brush BlackKeyBrush = new SolidColorBrush(Color.FromRgb(20, 20, 20));
    private static readonly Brush WhiteKeyActiveBrush = new SolidColorBrush(Color.FromRgb(120, 200, 255));
    private static readonly Brush BlackKeyActiveBrush = new SolidColorBrush(Color.FromRgb(60, 140, 220));
    private static readonly Pen KeyBorderPen = new(new SolidColorBrush(Color.FromRgb(180, 180, 180)), 1);

    static PianoKeyboardControl()
    {
        WhiteKeyBrush.Freeze(); BlackKeyBrush.Freeze();
        WhiteKeyActiveBrush.Freeze(); BlackKeyActiveBrush.Freeze();
        KeyBorderPen.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0) return;

        var keys = PianoLayout.Compute(width, height);
        var active = ActiveNotes;

        foreach (var key in keys.Where(k => !k.IsBlack))
        {
            var brush = active.Contains(key.NoteNumber) ? WhiteKeyActiveBrush : WhiteKeyBrush;
            var rect = new Rect(key.X, 0, key.Width - 1, height);
            dc.DrawRectangle(brush, KeyBorderPen, rect);
        }

        foreach (var key in keys.Where(k => k.IsBlack))
        {
            var brush = active.Contains(key.NoteNumber) ? BlackKeyActiveBrush : BlackKeyBrush;
            var rect = new Rect(key.X, 0, key.Width, key.Height);
            dc.DrawRectangle(brush, null, rect);
        }
    }

    /// <summary>Call from the parent whenever CurrentTime changes to refresh which keys light up.</summary>
    public void UpdateActiveNotes(IReadOnlyList<PianoNote> notes, double currentTimeSeconds)
    {
        var active = new HashSet<int>();
        foreach (var n in notes)
        {
            if (currentTimeSeconds >= n.StartTimeSeconds && currentTimeSeconds <= n.EndTimeSeconds)
                active.Add(n.NoteNumber);
        }
        ActiveNotes = active;
    }
}
