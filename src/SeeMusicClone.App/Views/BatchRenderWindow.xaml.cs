using System.IO;
using System.Windows;
using Microsoft.Win32;
using SeeMusicClone.Core.Batch;
using SeeMusicClone.Core.Midi;

namespace SeeMusicClone.App.Views;

public partial class BatchRenderWindow : Window
{
    private CancellationTokenSource? _cts;

    public BatchRenderWindow()
    {
        InitializeComponent();
    }

    private void BrowseInput_Click(object sender, RoutedEventArgs e)
    {
        var path = PickFolder();
        if (path != null)
        {
            InputFolderBox.Text = path;
            UpdateInputSummary();
        }
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var path = PickFolder();
        if (path != null) OutputFolderBox.Text = path;
    }

    private void BrowseFfmpeg_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "ffmpeg executable (ffmpeg.exe)|ffmpeg.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            Title = "Select ffmpeg.exe"
        };

        if (dialog.ShowDialog() == true)
            FfmpegPathBox.Text = dialog.FileName;
    }

    // OpenFolderDialog requires .NET 8 / WinForms interop is avoided here by using
    // the folder-picking mode of the standard file dialog (Windows 11 / recent .NET SDKs).
    private static string? PickFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select folder" };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private async void RenderButton_Click(object sender, RoutedEventArgs e)
    {
        var inputFolder = InputFolderBox.Text.Trim();
        var outputFolder = OutputFolderBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(inputFolder) || string.IsNullOrWhiteSpace(outputFolder))
        {
            MessageBox.Show("Please choose both an input and output folder.");
            return;
        }

        if (!Directory.Exists(inputFolder))
        {
            MessageBox.Show("The input folder does not exist.");
            return;
        }

        var midiFiles = MidiLoader.FindMidiFiles(inputFolder).ToList();
        if (midiFiles.Count == 0)
        {
            MessageBox.Show("No .mid or .midi files were found in the input folder.");
            return;
        }

        var resolutionText = ((System.Windows.Controls.ComboBoxItem)ResolutionCombo.SelectedItem).Content!.ToString()!;
        var parts = resolutionText.Split('x');
        int width = int.Parse(parts[0]);
        int height = int.Parse(parts[1]);
        int fps = int.Parse(((System.Windows.Controls.ComboBoxItem)FpsCombo.SelectedItem).Content!.ToString()!);
        var ffmpegPath = FfmpegPathBox.Text.Trim();

        var options = new BatchRenderOptions
        {
            Width = width,
            Height = height,
            Fps = fps,
            NoteSpeedPixelsPerSecond = SpeedSlider.Value,
            OutputFolder = outputFolder,
            FfmpegPath = string.IsNullOrWhiteSpace(ffmpegPath) ? null : ffmpegPath
        };

        RenderButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        Progress.Value = 0;
        LogList.Items.Clear();
        LogList.Items.Add($"Rendering {midiFiles.Count} MIDI file(s) at {width}x{height}, {fps} fps.");
        _cts = new CancellationTokenSource();

        var progress = new Progress<BatchRenderProgress>(p =>
        {
            Progress.Value = p.FractionComplete * 100;
            SummaryText.Text = $"File {p.FileIndex + 1} of {p.TotalFiles}: {p.FileName} ({p.FractionComplete:P0})";

            if (p.IsFileComplete)
            {
                var msg = p.ErrorMessage == null
                    ? $"[{p.FileIndex + 1}/{p.TotalFiles}] {p.FileName} — done"
                    : $"[{p.FileIndex + 1}/{p.TotalFiles}] {p.FileName} — ERROR: {p.ErrorMessage}";
                LogList.Items.Add(msg);
            }
        });

        try
        {
            var renderer = new BatchRenderer();
            await renderer.RenderFolderAsync(inputFolder, options, progress, _cts.Token);
            LogList.Items.Add("All files complete.");
            SummaryText.Text = "Batch render complete.";
        }
        catch (OperationCanceledException)
        {
            LogList.Items.Add("Cancelled.");
            SummaryText.Text = "Batch render cancelled.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Batch render failed:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RenderButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        CancelButton.IsEnabled = false;
    }

    private void UpdateInputSummary()
    {
        if (!Directory.Exists(InputFolderBox.Text))
        {
            SummaryText.Text = "Choose an input folder to render every .mid/.midi file found there.";
            return;
        }

        var count = MidiLoader.FindMidiFiles(InputFolderBox.Text).Count();
        SummaryText.Text = count == 1
            ? "1 MIDI file found."
            : $"{count} MIDI files found.";
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Cancel();
        base.OnClosed(e);
    }
}
