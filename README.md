# See Music Clone

A Windows desktop piano-roll visualizer for MIDI and monophonic audio files. Load
a MIDI file or analyze a single-note melody from audio, play it with synchronized
falling notes, tune the visual timing, and export MIDI visualizations as MP4 video.

## Features

- 88-key piano keyboard with live note highlighting
- Falling-note visualization synced to MIDI playback
- Monophonic audio transcription with synchronized original-audio playback
- WAV, MP3, AIFF, WMA, M4A, and AAC file import
- Live microphone note detection with tuning feedback
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

1. Click `Open File` and choose a MIDI or supported audio file.
2. For audio, wait for pitch analysis to finish. The button can cancel analysis.
3. Use `Play`, `Pause`, `Stop`, and the seek bar to control playback.
4. Adjust `Note Speed` to change how quickly notes fall on screen.
5. Adjust `Playback` to change MIDI playback speed. Audio files play at original speed.
6. Adjust `Audio Sync` if the visuals need to lead or lag the audio slightly.
7. Click `Export Video` to render a loaded MIDI file as an MP4.
8. Click `Batch Render` to export every MIDI file in a folder.

Audio transcription follows one dominant pitch at a time. It works best for voice,
whistling, flute, or isolated melody instruments. Chords and full mixed songs need
a polyphonic transcription model and are not interpreted accurately by this mode.

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
Directory.Build.props    shared compiler/analyzer settings
Directory.Packages.props central NuGet package versions
src/
  SeeMusicClone.App/      WPF desktop application
  SeeMusicClone.Core/     MIDI loading, playback helpers, rendering, and export
tests/
  SeeMusicClone.Core.Tests/ focused unit tests for core audio behavior
```

Key files:

- `src/SeeMusicClone.App/MainWindow.xaml` - main playback and visualization UI
- `src/SeeMusicClone.App/Controls/FallingNotesControl.cs` - live falling notes
- `src/SeeMusicClone.App/Controls/PianoKeyboardControl.cs` - 88-key keyboard
- `src/SeeMusicClone.App/ViewModels/MainViewModel.cs` - playback, export, and UI state
- `src/SeeMusicClone.App/Views/BatchRenderWindow.xaml` - batch export dialog
- `src/SeeMusicClone.Core/Midi/MidiLoader.cs` - MIDI parsing
- `src/SeeMusicClone.Core/Audio/AudioFileTranscriber.cs` - audio-to-note transcription
- `src/SeeMusicClone.Core/Playback/PlaybackController.cs` - MIDI playback control
- `src/SeeMusicClone.Core/Rendering/PianoLayout.cs` - shared key layout
- `src/SeeMusicClone.Core/Rendering/FrameRenderer.cs` - headless frame renderer
- `src/SeeMusicClone.Core/Batch/BatchRenderer.cs` - ffmpeg video export pipeline

## Dependencies

- `Melanchall.DryWetMidi` for MIDI parsing and playback
- `NAudio` for microphone capture and audio-file decoding/playback
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
