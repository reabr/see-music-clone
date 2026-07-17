# See Music Clone

A Windows desktop piano-roll visualizer for MIDI files. Load a `.mid` or `.midi`
file, play it with synchronized falling notes, tune the visual timing, and export
single files or whole folders as MP4 videos.

## Features

- 88-key piano keyboard with live note highlighting
- Falling-note visualization synced to MIDI playback
- Playback controls with seek, stop, and play/pause
- Adjustable note fall speed
- Adjustable playback speed from 0.25x to 2x
- Audio sync offset control for fine-tuning visual alignment
- Export the current MIDI file to MP4
- Batch render every MIDI file in a folder
- Shared layout logic between the live WPF view and the headless video renderer

## Requirements

- Windows
- .NET 8 SDK with the Windows desktop workload
- ffmpeg on `PATH` for video export

Live playback uses the system MIDI output device. On Windows, the built-in
Microsoft GS Wavetable Synth is usually available without extra setup.

## Getting Started

From the repository root:

```powershell
dotnet restore
dotnet build
dotnet run --project src/SeeMusicClone.App
```

You can also open `see-music-clone.sln` in Visual Studio 2022, set
`SeeMusicClone.App` as the startup project, and run it with F5.

## Using The App

1. Click `Open MIDI` and choose a `.mid` or `.midi` file.
2. Use `Play`, `Pause`, `Stop`, and the seek bar to control playback.
3. Adjust `Note Speed` to change how quickly notes fall on screen.
4. Adjust `Playback` to change both audio speed and animation timing.
5. Adjust `Audio Sync` if the visuals need to lead or lag the audio slightly.
6. Click `Export Video` to render the loaded file as an MP4.
7. Click `Batch Render` to export every MIDI file in a folder.

## Video Export

The renderer sends raw frames to ffmpeg and writes H.264 MP4 files. Exported
videos are visual-only by default; they do not include an audio track.

For batch export, choose:

- input folder containing `.mid` or `.midi` files
- output folder for generated `.mp4` files
- resolution
- frame rate
- note speed

If ffmpeg is not on `PATH`, set `BatchRenderOptions.FfmpegPath` to the full path
of `ffmpeg.exe`.

## Project Structure

```text
src/
  SeeMusicClone.App/      WPF desktop application
  SeeMusicClone.Core/     MIDI loading, playback helpers, rendering, and export
```

Key files:

- `src/SeeMusicClone.App/MainWindow.xaml` - main playback and visualization UI
- `src/SeeMusicClone.App/Controls/FallingNotesControl.cs` - live falling notes
- `src/SeeMusicClone.App/Controls/PianoKeyboardControl.cs` - 88-key keyboard
- `src/SeeMusicClone.App/ViewModels/MainViewModel.cs` - playback, export, and UI state
- `src/SeeMusicClone.App/Views/BatchRenderWindow.xaml` - batch export dialog
- `src/SeeMusicClone.Core/Midi/MidiLoader.cs` - MIDI parsing
- `src/SeeMusicClone.Core/Playback/PlaybackController.cs` - MIDI playback control
- `src/SeeMusicClone.Core/Rendering/PianoLayout.cs` - shared key layout
- `src/SeeMusicClone.Core/Rendering/FrameRenderer.cs` - headless frame renderer
- `src/SeeMusicClone.Core/Batch/BatchRenderer.cs` - ffmpeg video export pipeline

## Dependencies

- `Melanchall.DryWetMidi` for MIDI parsing and playback
- `SkiaSharp` for headless frame rendering during export
- `ffmpeg` for MP4 encoding

NuGet dependencies are restored automatically by `dotnet restore`.

## Troubleshooting

If playback has no sound, check that Windows has an available MIDI output device
and that system audio is not muted.

If export fails, confirm that `ffmpeg` is installed and available from a terminal:

```powershell
ffmpeg -version
```

If restore fails because a package version is unavailable, update the affected
package version in the relevant `.csproj` file and restore again.
