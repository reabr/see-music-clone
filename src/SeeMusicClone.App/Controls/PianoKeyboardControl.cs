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
    public static readonly DependencyProperty NotesProperty = DependencyProperty.Register(
        nameof(Notes), typeof(IReadOnlyList<PianoNote>), typeof(PianoKeyboardControl),
        new FrameworkPropertyMetadata(Array.Empty<PianoNote>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CurrentTimeProperty = DependencyProperty.Register(
        nameof(CurrentTime), typeof(double), typeof(PianoKeyboardControl),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty NoteSpeedProperty = DependencyProperty.Register(
        nameof(NoteSpeed), typeof(double), typeof(PianoKeyboardControl),
        new FrameworkPropertyMetadata(140.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FallAreaHeightProperty = DependencyProperty.Register(
        nameof(FallAreaHeight), typeof(double), typeof(PianoKeyboardControl),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

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

    public double NoteSpeed
    {
        get => (double)GetValue(NoteSpeedProperty);
        set => SetValue(NoteSpeedProperty, value);
    }

    public double FallAreaHeight
    {
        get => (double)GetValue(FallAreaHeightProperty);
        set => SetValue(FallAreaHeightProperty, value);
    }

    private static readonly Brush WhiteKeyBrush = new SolidColorBrush(Color.FromRgb(250, 250, 250));
    private static readonly Brush BlackKeyBrush = new SolidColorBrush(Color.FromRgb(20, 20, 20));
    private static readonly Brush LeftHandActiveBrush = CreateActiveBrush(NoteColorPalette.HandSplitNoteNumber - 1);
    private static readonly Brush RightHandActiveBrush = CreateActiveBrush(NoteColorPalette.HandSplitNoteNumber);
    private static readonly Pen KeyBorderPen = new(new SolidColorBrush(Color.FromRgb(180, 180, 180)), 1);

    static PianoKeyboardControl()
    {
        WhiteKeyBrush.Freeze(); BlackKeyBrush.Freeze();
        LeftHandActiveBrush.Freeze(); RightHandActiveBrush.Freeze();
        KeyBorderPen.Freeze();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0) return;

        var keys = PianoLayout.Compute(width, height);
        var active = ActiveNoteCalculator.GetActiveNotes(Notes, CurrentTime, NoteSpeed, FallAreaHeight);

        foreach (var key in keys.Where(k => !k.IsBlack))
        {
            var brush = active.Contains(key.NoteNumber) ? GetActiveBrush(key.NoteNumber) : WhiteKeyBrush;
            var rect = new Rect(key.X, 0, key.Width - 1, height);
            dc.DrawRectangle(brush, KeyBorderPen, rect);
        }

        foreach (var key in keys.Where(k => k.IsBlack))
        {
            var brush = active.Contains(key.NoteNumber) ? GetActiveBrush(key.NoteNumber) : BlackKeyBrush;
            var rect = new Rect(key.X, 0, key.Width, key.Height);
            dc.DrawRectangle(brush, null, rect);
        }
    }

    private static Brush GetActiveBrush(int noteNumber)
    {
        return noteNumber >= NoteColorPalette.HandSplitNoteNumber
            ? RightHandActiveBrush
            : LeftHandActiveBrush;
    }

    private static SolidColorBrush CreateActiveBrush(int noteNumber)
    {
        var color = NoteColorPalette.GetRgb(noteNumber);
        return new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
    }
}
