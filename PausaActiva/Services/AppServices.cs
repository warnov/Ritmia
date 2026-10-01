using System.Diagnostics;
using Encoding = System.Text.Encoding;
using PausaActiva.Core;
using Plugin.Maui.Audio;

namespace PausaActiva.Services;

public sealed class AppServices
{
    public static AppServices Current { get; } = new();
    public YamlLibraryStore Store { get; } = new(Path.Combine(FileSystem.AppDataDirectory, "ejercicios.yaml"));
    public ExerciseLibrary Library { get; private set; } = new();
    public bool Loaded { get; private set; }
    public SignalService Signals { get; } = new();
    public PlaybackController Playback { get; }
    private readonly SemaphoreSlim gate = new(1, 1);
    private AppServices() => Playback = new(Signals);
    public async Task EnsureLoadedAsync()
    {
        await gate.WaitAsync();
        try { if (!Loaded) { Library = await Store.LoadAsync(); Loaded = true; } }
        finally { gate.Release(); }
    }
    public async Task ReloadAsync()
    {
        await gate.WaitAsync();
        try
        {
            var library = await Store.LoadAsync();
            Library = library;
            Loaded = true;
        }
        finally { gate.Release(); }
    }
    public async Task SaveExerciseAsync(Exercise exercise)
    {
        await EnsureLoadedAsync();
        await gate.WaitAsync();
        try
        {
            var library = new ExerciseLibrary { Exercises = Library.Exercises.Select(e => e.Copy()).ToList() };
            int index = library.Exercises.FindIndex(e => e.Id == exercise.Id);
            if (index < 0) library.Exercises.Add(exercise.Copy()); else library.Exercises[index] = exercise.Copy();
            await Store.SaveAsync(library);
            Library = library;
        }
        finally { gate.Release(); }
    }
    public async Task DeleteExerciseAsync(string id)
    {
        await EnsureLoadedAsync();
        await gate.WaitAsync();
        try
        {
            var library = new ExerciseLibrary { Exercises = Library.Exercises.Where(e => e.Id != id).Select(e => e.Copy()).ToList() };
            await Store.SaveAsync(library);
            Library = library;
        }
        finally { gate.Release(); }
    }
}

public sealed class SignalService
{
    private readonly Dictionary<CueKind, IAudioPlayer> players = [];
    public bool Enabled { get => Preferences.Get("sound", true); set => Preferences.Set("sound", value); }
    public double Volume { get => Preferences.Get("volume", 0.75); set => Preferences.Set("volume", value); }
    public string? Error { get; private set; }
    public void Play(CueKind kind, bool preview = false)
    {
        if (!Enabled && !preview) return;
        try
        {
            if (!players.TryGetValue(kind, out var player))
            {
                player = AudioManager.Current.CreatePlayer(CreateTone(kind));
                players.Add(kind, player);
            }
            foreach (var other in players.Values) other.Stop();
            player.Volume = Volume;
            player.Seek(0);
            player.Play();
            Error = null;
        }
        catch (Exception ex) { Error = $"No se pudo reproducir el sonido: {ex.Message}"; }
    }
    // PCM WAV with a short envelope to avoid clicks. No network or audio files required.
    private static MemoryStream CreateTone(CueKind kind)
    {
        double[] frequencies = kind switch { CueKind.Step => [880], CueKind.Repetition => [523.25, 659.25], _ => [523.25, 659.25, 783.99] };
        const int sampleRate = 44100;
        double duration = kind == CueKind.Step ? 0.12 : 0.19;
        int noteSamples = (int)(sampleRate * duration);
        int gapSamples = (int)(sampleRate * 0.055);
        int count = frequencies.Length * (noteSamples + gapSamples);
        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate); writer.Write(sampleRate * 2);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            foreach (double frequency in frequencies)
            {
                for (int i = 0; i < noteSamples; i++)
                {
                    double envelope = Math.Min(1, i / (sampleRate * 0.01)) * Math.Min(1, (noteSamples - i) / (sampleRate * 0.03));
                    writer.Write((short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * envelope * 12000));
                }
                for (int i = 0; i < gapSamples; i++) writer.Write((short)0);
            }
        }
        stream.Position = 0;
        return stream;
    }
}

public sealed class PlaybackController(SignalService signals)
{
    private readonly Stopwatch clock = new();
    private IDispatcherTimer? timer;
    public PlaybackSession? Session { get; private set; }
    public Exercise? Exercise => Session?.Exercise;
    public int SeriesIndex => Session?.SeriesIndex ?? 0;
    public PlaybackEngine? Engine => Session?.Engine;
    public string Notice { get; private set; } = "Elige un ejercicio para comenzar";
    public CueKind? LastCue { get; private set; }
    private long cueAt;
    public bool CueVisible => LastCue.HasValue && Stopwatch.GetElapsedTime(cueAt).TotalSeconds < 0.8;
    public event Action? Changed;
    public event Action<CueKind>? CueProduced;
    public void Initialize(IDispatcher dispatcher)
    {
        if (timer is not null) return;
        timer = dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(50);
        timer.Tick += (_, _) => Tick();
        timer.Start();
    }
    public void Select(Exercise exercise, int seriesIndex = 0, int plannedSeries = 1)
    {
        StopClock();
        Session = new(exercise, Math.Clamp(seriesIndex, 0, Math.Max(0, exercise.Series.Count - 1)), plannedSeries);
        Notice = Engine is null ? "Añade una serie en la biblioteca" : "Listo cuando tú lo estés";
        LastCue = null;
        Changed?.Invoke();
    }
    public void Clear()
    {
        StopClock(); Session = null; LastCue = null;
        Notice = "Crea un ejercicio en la biblioteca"; Changed?.Invoke();
    }
    public void StartOrPause()
    {
        if (Engine is null) return;
        if (Engine.State == PlaybackState.Running) { Pause(); return; }
        if (Engine.State == PlaybackState.Completed)
        {
            clock.Reset();
            LastCue = null;
        }
        if (Session?.Start() != true) return;
        clock.Start(); DeviceDisplay.Current.KeepScreenOn = true;
        Notice = "Sigue el ritmo de las señales";
        Changed?.Invoke();
    }
    public void Pause()
    {
        if (Engine?.State != PlaybackState.Running) return;
        Tick();
        if (Engine.State != PlaybackState.Running) return;
        clock.Stop(); Engine.Pause(); DeviceDisplay.Current.KeepScreenOn = false;
        Notice = "En pausa · el tiempo se conserva"; Changed?.Invoke();
    }
    public void Reset()
    {
        StopClock(); Session?.Reset(); LastCue = null;
        Notice = "Sesión reiniciada · pulsa iniciar"; Changed?.Invoke();
    }
    private void StopClock() { clock.Reset(); DeviceDisplay.Current.KeepScreenOn = false; }
    private void Tick()
    {
        bool shouldRender = Engine?.State == PlaybackState.Running || CueVisible;
        if (Engine?.State == PlaybackState.Running)
        {
            var cues = Engine.Tick(clock.Elapsed);
            // On a delayed UI frame only sound the latest boundary, avoiding a burst of stale cues.
            if (cues.Count > 0)
            {
                LastCue = cues[^1]; cueAt = Stopwatch.GetTimestamp(); signals.Play(LastCue.Value);
                CueProduced?.Invoke(LastCue.Value);
                Notice = LastCue switch { CueKind.Step => "Cambio de paso", CueKind.Repetition => "Repetición completada", _ => "Serie completada · descansa a tu ritmo" };
            }
            if (Engine.State == PlaybackState.Completed)
            {
                clock.Stop(); DeviceDisplay.Current.KeepScreenOn = false;
                Notice = Session!.Completed ? "Sesión completada · ¡listo!" : $"Serie {Session.SeriesNumber} completada · inicia la siguiente cuando quieras";
            }
        }
        if (shouldRender) Changed?.Invoke();
    }
}
