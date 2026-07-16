# Falling Notes — Piano Visualizer (Synthesia-style)

A Windows desktop app: falling notes, 88-key keyboard, adjustable note speed,
and batch rendering of many MIDI files to video.

## Architecture

- **SeeMusicClone.Core** — plain .NET 8 class library, no UI dependency.
  - `Midi/MidiLoader.cs` — parses `.mid` files with DryWetMidi into flat `NoteEvent`s (absolute seconds).
  - `Rendering/PianoLayout.cs` — computes x/width for all 88 keys; shared by the live UI and batch renderer so notes always line up with keys exactly.
  - `Playback/PlaybackController.cs` — wraps DryWetMidi's `Playback`, which has a built-in `Speed` property for real-time-adjustable playback speed (also affects the actual MIDI audio, not just the animation).
  - `Rendering/FrameRenderer.cs` — headless SkiaSharp renderer that draws one frame (piano + falling notes) to raw pixels, used only for batch video export.
  - `Batch/BatchRenderer.cs` — loops over every `.mid`/`.midi` file in a folder, renders frames, and pipes them into `ffmpeg` to produce an `.mp4` per file.

- **SeeMusicClone.App** — WPF app (net8.0-windows).
  - `Controls/PianoKeyboardControl.cs` — custom `FrameworkElement`, draws the 88 keys, highlights active notes.
  - `Controls/FallingNotesControl.cs` — custom `FrameworkElement`, draws falling note rectangles synced to `CurrentTime`, with a `NoteSpeed` property for adjustable fall speed.
  - `ViewModels/MainViewModel.cs` — open file, play/pause/stop, note-speed slider, playback-speed slider.
  - `Views/BatchRenderWindow.xaml(.cs)` — pick input/output folders, resolution, fps, note speed, and render every MIDI file in the folder to video with a progress bar.

Why WPF: best-in-class custom 2D drawing + animation performance on Windows desktop, mature MIDI/audio ecosystem, and no extra runtime dependencies beyond .NET 8 Desktop Runtime.

## Prerequisites

1. **.NET 8 SDK** (with the Desktop Runtime workload) — https://dotnet.microsoft.com/download
2. **ffmpeg** on your `PATH` (or set `BatchRenderOptions.FfmpegPath` to the full exe path) — only needed for batch video export, not for live playback. https://ffmpeg.org/download.html
3. A MIDI output device for audio playback. Windows always has the built-in "Microsoft GS Wavetable Synth", so playback works out of the box even without a real synth/soundfont.

## Build & run

```
cd SeeMusicClone
dotnet restore
dotnet build
dotnet run --project src/SeeMusicClone.App
```

Or just open `SeeMusicClone.sln` in Visual Studio 2022, set `SeeMusicClone.App` as the startup project, and press F5.

NuGet packages used (restored automatically):
- `Melanchall.DryWetMidi` — MIDI parsing + playback + speed control
- `SkiaSharp` — headless frame rendering for batch export

> Package versions pinned in the `.csproj` files are what was current at time of writing;
> if `dotnet restore` complains a version isn't found, bump to whatever's latest on nuget.org.

## Using the app

- **Open MIDI...** loads a file and shows it in the falling-notes view.
- **Play / Stop**, **Note Speed** slider (pixels/second of fall), **Playback Speed** slider (0.25x–2x, affects both audio and the falling-note animation together, since both are driven by the same DryWetMidi `Playback` clock).
- **Batch Render...** opens a dialog: pick a folder of MIDI files and an output folder, choose resolution/fps/note speed, and it renders one `.mp4` per file (no audio track — this is a visual export; muxing the original MIDI audio into the video is a good next step if you need that, e.g. render audio via a soundfont synth like `FluidSynth`/`NAudio` alongside the frames and add `-i audio.wav` to the ffmpeg args).

## Extending

- **Soundfont/quality audio**: swap the default GS Wavetable synth for a SoundFont-based synth (e.g. via NAudio + a `.sf2` file) if you want nicer piano tone.
- **Note labels / hand coloring**: `FrameRenderer`/`FallingNotesControl` color by MIDI channel right now — easy to switch to left/right hand coloring by splitting channels or note ranges.
- **Audio in exported video**: render a WAV of the MIDI (e.g. with a software synth) and add `-i audio.wav -c:a aac` to the ffmpeg arguments in `BatchRenderer`, muxing it with the video frames.
