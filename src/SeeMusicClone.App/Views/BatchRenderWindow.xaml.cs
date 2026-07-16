using System.Windows;
using Microsoft.Win32;
using SeeMusicClone.Core.Batch;

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
        if (path != null) InputFolderBox.Text = path;
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var path = PickFolder();
        if (path != null) OutputFolderBox.Text = path;
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
        if (string.IsNullOrWhiteSpace(InputFolderBox.Text) || string.IsNullOrWhiteSpace(OutputFolderBox.Text))
        {
            MessageBox.Show("Please choose both an input and output folder.");
            return;
        }

        var resolutionText = ((System.Windows.Controls.ComboBoxItem)ResolutionCombo.SelectedItem).Content!.ToString()!;
        var parts = resolutionText.Split('x');
        int width = int.Parse(parts[0]);
        int height = int.Parse(parts[1]);
        int fps = int.Parse(((System.Windows.Controls.ComboBoxItem)FpsCombo.SelectedItem).Content!.ToString()!);

        var options = new BatchRenderOptions
        {
            Width = width,
            Height = height,
            Fps = fps,
            NoteSpeedPixelsPerSecond = SpeedSlider.Value,
            OutputFolder = OutputFolderBox.Text
        };

        RenderButton.IsEnabled = false;
        LogList.Items.Clear();
        _cts = new CancellationTokenSource();

        var progress = new Progress<BatchRenderProgress>(p =>
        {
            Progress.Value = p.FractionComplete * 100;

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
            await renderer.RenderFolderAsync(InputFolderBox.Text, options, progress, _cts.Token);
            LogList.Items.Add("All files complete.");
        }
        catch (OperationCanceledException)
        {
            LogList.Items.Add("Cancelled.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Batch render failed:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RenderButton.IsEnabled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Cancel();
        base.OnClosed(e);
    }
}
