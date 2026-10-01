using PausaActiva.Core;

int assertions = 0;
void Check(bool condition, string message)
{
    assertions++;
    if (!condition) throw new Exception(message);
}
var series = new ExerciseSeries { Repetitions = 2, Steps = [new() { Name = "Subir", Seconds = 2 }, new() { Name = "Bajar", Seconds = 3 }] };
var engine = new PlaybackEngine(series);
Check(engine.State == PlaybackState.Ready && engine.RemainingSeconds == 2, "Initial state");
engine.Start();
Check(engine.Tick(TimeSpan.FromSeconds(1)).Count == 0 && engine.RemainingSeconds == 1, "Before boundary");
Check(engine.Tick(TimeSpan.FromSeconds(2)).SequenceEqual([CueKind.Step]) && engine.StepIndex == 1, "Step boundary");
engine.Tick(TimeSpan.FromSeconds(3)); engine.Pause();
Check(engine.Tick(TimeSpan.FromSeconds(100)).Count == 0 && engine.RemainingSeconds == 2, "Pause freezes time");
engine.Start();
Check(engine.Tick(TimeSpan.FromSeconds(5)).SequenceEqual([CueKind.Repetition]) && engine.RepetitionIndex == 1 && engine.StepIndex == 0, "Repetition boundary");
Check(engine.Tick(TimeSpan.FromSeconds(7)).SequenceEqual([CueKind.Step]), "Second repetition step");
Check(engine.Tick(TimeSpan.FromSeconds(10)).SequenceEqual([CueKind.Series]) && engine.State == PlaybackState.Completed, "Series cue takes priority at final repetition");
Check(engine.RemainingSeconds == 0 && engine.TotalProgress == 1 && engine.RepetitionIndex == 1, "Final counters");
Check(engine.Tick(TimeSpan.FromSeconds(100)).Count == 0, "No duplicate completion");
engine.Start(); Check(engine.State == PlaybackState.Completed, "Completion requires explicit reset or next series");
engine.Reset(); engine.Start();
Check(engine.Tick(TimeSpan.FromSeconds(100)).SequenceEqual([CueKind.Step, CueKind.Repetition, CueKind.Step, CueKind.Series]), "Delayed tick catches up accurately");
var fractional = new PlaybackEngine(new() { Repetitions = 3, Steps = [new() { Seconds = 0.1 }] });
fractional.Start();
Check(fractional.Tick(TimeSpan.FromSeconds(0.3)).SequenceEqual([CueKind.Repetition, CueKind.Repetition, CueKind.Series]), "Single fractional step and exact final boundary");
engine.Reset(); engine.Start(); engine.Tick(TimeSpan.FromSeconds(1)); engine.Tick(TimeSpan.Zero);
Check(engine.ElapsedSeconds == 1, "Clock cannot go backwards");
series.Steps[0].Seconds = 900; Check(engine.Series.Steps[0].Seconds == 2, "Playback isolates config snapshot");
foreach (double bad in new[] { 0, -1, double.NaN, double.PositiveInfinity })
{
    bool rejected = false;
    try { LibraryRules.Validate(new ExerciseSeries { Steps = [new() { Seconds = bad }] }); }
    catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Invalid duration rejected");
}
var exercise = new Exercise { Series = [
    new() { Name = "Alternativa", Repetitions = 99, Steps = [new() { Name = "Otro movimiento", Seconds = 50 }] },
    new() { Name = "Main", Repetitions = 2, Steps = [new() { Name = "Elevar", Seconds = 1 }, new() { Name = "Bajar", Seconds = 2 }] }
] };
var session = new PlaybackSession(exercise, 1, 3);
Check(session.SeriesIndex == 1 && session.SeriesNumber == 1 && session.PlannedSeries == 3 && session.DurationSeconds == 18, "Session plans selected variant only");
for (int run = 1; run <= 3; run++)
{
    Check(session.Start() && session.SeriesNumber == run, "Manual start advances execution counter");
    Check(session.SeriesIndex == 1 && session.Engine!.Series.Name == "Main" && session.Engine.Series.Repetitions == 2, "Every execution keeps selected variant");
    session.Engine!.Tick(TimeSpan.FromSeconds(1)); session.Engine.Pause();
    Check(session.Start() && session.SeriesNumber == run && session.Engine.StepIndex == 1, "Resume doesn't advance execution");
    var cues = session.Engine.Tick(TimeSpan.FromSeconds(6));
    Check(cues[^1] == CueKind.Series && session.Engine.State == PlaybackState.Completed, "Each execution finishes with series cue and waits");
    Check(session.ElapsedSeconds == run * 6 && session.Completed == (run == 3), "Session elapsed and completion respect execution count");
}
Check(!session.Start() && session.SeriesNumber == 3, "Finished session cannot start extra execution");
session.Reset();
Check(session.SeriesNumber == 1 && session.SeriesIndex == 1 && session.ElapsedSeconds == 0 && session.Engine!.State == PlaybackState.Ready, "Reset keeps variant and resets session");
var single = new PlaybackSession(exercise, 0, 1);
single.Start(); single.Engine!.Tick(TimeSpan.FromSeconds(4950));
Check(single.Completed && !single.Start() && single.SeriesIndex == 0, "One execution doesn't switch to another variant");
foreach (int badCount in new[] { 0, -1, 10001 })
{
    bool rejected = false;
    try { _ = new PlaybackSession(exercise, 1, badCount); } catch (ArgumentOutOfRangeException) { rejected = true; }
    Check(rejected, "Invalid session count rejected");
}
Check(new PlaybackSession(new Exercise(), 0, 1).Engine is null, "Exercise without variants has no playback");
var folder = Path.Combine(Path.GetTempPath(), "PausaActiva-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
try
{
    var path = Path.Combine(folder, "ejercicios.yaml"); var store = new YamlLibraryStore(path);
    var library = await store.LoadAsync();
    Check(library.Exercises.Count == 2 && File.Exists(path), "First run seeds local YAML");
    library.Exercises[0].Name = "Prueba: ñ y símbolos #";
    library.Exercises[0].Description = "Primera línea\nSegunda línea";
    library.Exercises[0].Series[0].Steps[0].Seconds = 1.25;
    await store.SaveAsync(library); var loaded = await store.LoadAsync();
    Check(loaded.Exercises[0].Name == library.Exercises[0].Name && loaded.Exercises[0].Description == library.Exercises[0].Description, "YAML accents, punctuation and multiline roundtrip");
    Check(loaded.Exercises[0].Series[0].Steps[0].Seconds == 1.25 && loaded.Exercises[0].Series.Count == 2, "YAML independent series and fractional seconds");
    Check(File.Exists(path + ".bak") && !File.Exists(path + ".tmp"), "Atomic save and backup");
    string before = await File.ReadAllTextAsync(path); loaded.Exercises[0].Series[0].Repetitions = 0;
    bool failed = false;
    try { await store.SaveAsync(loaded); } catch (InvalidDataException) { failed = true; }
    Check(failed && await File.ReadAllTextAsync(path) == before, "Failed validation preserves file");
    await File.WriteAllTextAsync(path, "version: 99\nexercises: []"); failed = false;
    try { await store.LoadAsync(); } catch (InvalidDataException) { failed = true; }
    Check(failed && (await File.ReadAllTextAsync(path)).Contains("99"), "Unknown schema isn't overwritten");
    await store.SaveAsync(new ExerciseLibrary());
    Check((await store.LoadAsync()).Exercises.Count == 0, "Empty library persists after delete");
}
finally { Directory.Delete(folder, recursive: true); }
Console.WriteLine($"PASS: {assertions} checks (playback, validation, YAML persistence)");
