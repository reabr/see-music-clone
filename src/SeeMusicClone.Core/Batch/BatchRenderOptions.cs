namespace SeeMusicClone.Core.Batch;

public sealed class BatchRenderOptions
{
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public int Fps { get; set; } = 30;
    public double NoteSpeedPixelsPerSecond { get; set; } = 220;
    public string OutputFolder { get; set; } = string.Empty;

    /// <summary>Full path to ffmpeg.exe. If null, assumes "ffmpeg" is on PATH.</summary>
    public string? FfmpegPath { get; set; }
}

public sealed class BatchRenderProgress
{
    public string FileName { get; init; } = string.Empty;
    public int FileIndex { get; init; }
    public int TotalFiles { get; init; }
    public double FractionComplete { get; init; } // 0..1 for the current file
    public bool IsFileComplete { get; init; }
    public string? ErrorMessage { get; init; }
}
