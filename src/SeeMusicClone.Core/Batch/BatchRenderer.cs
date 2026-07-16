using System.Diagnostics;
using SeeMusicClone.Core.Midi;
using SeeMusicClone.Core.Rendering;

namespace SeeMusicClone.Core.Batch;

/// <summary>
/// Renders a folder of MIDI files to individual MP4 videos, one ffmpeg process per file,
/// so many files can be queued up and rendered unattended. Runs off the UI thread.
/// </summary>
public sealed class BatchRenderer
{
    public async Task RenderFolderAsync(
        string inputFolder,
        BatchRenderOptions options,
        IProgress<BatchRenderProgress> progress,
        CancellationToken cancellationToken = default)
    {
        var files = MidiLoader.FindMidiFiles(inputFolder).ToList();
        Directory.CreateDirectory(options.OutputFolder);

        for (int i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[i];

            try
            {
                await RenderSingleFileAsync(file, i, files.Count, options, progress, cancellationToken);
            }
            catch (Exception ex)
            {
                progress.Report(new BatchRenderProgress
                {
                    FileName = Path.GetFileName(file),
                    FileIndex = i,
                    TotalFiles = files.Count,
                    FractionComplete = 0,
                    IsFileComplete = true,
                    ErrorMessage = ex.Message
                });
            }
        }
    }

    private static async Task RenderSingleFileAsync(
        string midiPath,
        int fileIndex,
        int totalFiles,
        BatchRenderOptions options,
        IProgress<BatchRenderProgress> progress,
        CancellationToken cancellationToken)
    {
        var song = MidiLoader.Load(midiPath);
        var renderer = new FrameRenderer(options.Width, options.Height)
        {
            NoteSpeedPixelsPerSecond = options.NoteSpeedPixelsPerSecond
        };

        string outputPath = Path.Combine(
            options.OutputFolder,
            Path.GetFileNameWithoutExtension(midiPath) + ".mp4");

        var ffmpegExe = string.IsNullOrWhiteSpace(options.FfmpegPath) ? "ffmpeg" : options.FfmpegPath;

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegExe,
            Arguments =
                $"-y -f rawvideo -pixel_format bgra -video_size {options.Width}x{options.Height} " +
                $"-framerate {options.Fps} -i - -an -c:v libx264 -pix_fmt yuv420p \"{outputPath}\"",
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Could not start ffmpeg. Check that it is installed and on PATH, or set BatchRenderOptions.FfmpegPath.");

        // Drain stderr so ffmpeg doesn't block on a full pipe buffer.
        _ = Task.Run(() => process.StandardError.ReadToEndAsync(), cancellationToken);

        int totalFrames = (int)Math.Ceiling(song.DurationSeconds * options.Fps);
        totalFrames = Math.Max(totalFrames, 1);

        using (var stdin = process.StandardInput.BaseStream)
        {
            for (int frame = 0; frame < totalFrames; frame++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double t = frame / (double)options.Fps;
                byte[] pixels = renderer.RenderFrame(song.Notes, t);
                await stdin.WriteAsync(pixels, cancellationToken);

                if (frame % 5 == 0 || frame == totalFrames - 1)
                {
                    progress.Report(new BatchRenderProgress
                    {
                        FileName = song.FileName,
                        FileIndex = fileIndex,
                        TotalFiles = totalFiles,
                        FractionComplete = (frame + 1) / (double)totalFrames,
                        IsFileComplete = false
                    });
                }
            }
        }

        await process.WaitForExitAsync(cancellationToken);

        progress.Report(new BatchRenderProgress
        {
            FileName = song.FileName,
            FileIndex = fileIndex,
            TotalFiles = totalFiles,
            FractionComplete = 1.0,
            IsFileComplete = true,
            ErrorMessage = process.ExitCode != 0 ? $"ffmpeg exited with code {process.ExitCode}" : null
        });
    }
}
