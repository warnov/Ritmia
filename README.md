# Ritmia

Ritmia is an offline-first .NET MAUI app that guides short exercise sessions with clear visual and audio cues. It is designed for people who want to move while they work without watching the clock or counting repetitions.

## How it works

Create exercises in the library. Each exercise has a name, a reference description, and any number of alternative set variations. A variation defines its own repetition count and ordered movement steps, each with a duration in seconds.

For a session, choose an exercise, select one variation, and set how many times to perform that same variation. For example, **Main × 3** means three identical sets, each repeating Main's steps for its configured number of repetitions. Start each set manually when you are ready. Ritmia never switches to another variation or starts the next set automatically.

The player shows the current exercise, set and repetition counters, the active step, its remaining time, and session progress. Pause and resume preserve the current step. The current step has a large high-contrast display; transitions trigger a brief full-screen shift-light flash as well as sound:

- **Step change:** a short tone and lime-green flash.
- **Repetition complete:** two tones and a blue flash.
- **Set complete:** three ascending tones and an amber flash.

The set-complete cue replaces the repetition cue at the final repetition so the signals do not overlap. Audio can be disabled or adjusted in settings. Sample tones are generated locally by the app.

## Platforms and behavior

Ritmia is a native .NET MAUI application targeting Windows and Android, with iOS and Mac Catalyst target frameworks included in the project. Apple-platform builds require a Mac with the relevant development tools.

On Windows, the timer continues while you work in another window. On mobile, sending Ritmia to the background pauses the session; resume it when you return. The display stays awake during playback. In-progress session state is held in memory and ends when the app closes.

## Local data and privacy

Exercises, descriptions, variations, and steps are saved locally to `ejercicios.yaml` in MAUI's application data directory. The library is not part of the source tree and is excluded by `.gitignore`. Each save keeps the previous file as `ejercicios.yaml.bak`. Ritmia uses no service or network connection during normal use. Sound and volume preferences are stored locally by the platform.

On first launch, Ritmia creates two editable example exercises if no library exists. The examples are only defaults; your own library stays on your device.

## Run on Windows

Requirements: Windows 10 version 1809 or later, .NET 10 SDK, and the `maui-windows` .NET workload.

Open `Ritmia.slnx` in Visual Studio with the .NET MAUI workload installed, or run this script from PowerShell:

```powershell
./run-windows.ps1
```

The solution contains:

- `PausaActiva/` — the MAUI user interface and platform projects.
- `PausaActiva.Core/` — exercise models, validation, playback timing, and YAML storage.
- `PausaActiva.Checks/` — a lightweight executable check suite for playback and YAML behavior.

`Plugin.Maui.Audio` provides cross-platform playback; `YamlDotNet` reads and writes the local exercise library.

## Exercise library format

```yaml
version: 1
exercises:
  - id: ankle-circles
    name: Ankle circles
    description: A short movement guide.
    series:
      - name: Main
        type: repetitions
        repetitions: 20
        steps:
          - name: Circle clockwise
            seconds: 3
          - name: Circle counterclockwise
            seconds: 3
```

Each series entry is an alternative to choose for a session; the entries are not played one after another. Only `type: repetitions` is supported currently. Step durations may use decimals, with a minimum of 0.1 seconds.

## License

No license has been selected for this repository. All rights are reserved by default.
