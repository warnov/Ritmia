namespace PausaActiva.Core;

public enum PlaybackState { Ready, Running, Paused, Completed }
public enum CueKind { Step, Repetition, Series }

// elapsed is cumulative ACTIVE time. The UI clock stops during pauses.
public sealed class PlaybackEngine
{
    public ExerciseSeries Series { get; }
    public PlaybackState State { get; private set; } = PlaybackState.Ready;
    public int RepetitionIndex { get; private set; }
    public int StepIndex { get; private set; }
    public double ElapsedSeconds { get; private set; }
    private double stepStart;
    private double boundary;
    public double RemainingSeconds => State == PlaybackState.Completed ? 0 : Math.Max(0, boundary - ElapsedSeconds);
    public double StepProgress => State == PlaybackState.Completed ? 1 : Math.Clamp((ElapsedSeconds - stepStart) / Series.Steps[StepIndex].Seconds, 0, 1);
    public double TotalProgress => Math.Clamp(ElapsedSeconds / Series.DurationSeconds, 0, 1);

    public PlaybackEngine(ExerciseSeries series)
    {
        LibraryRules.Validate(series);
        Series = series.Copy();
        Reset();
    }
    public void Start() { if (State is PlaybackState.Ready or PlaybackState.Paused) State = PlaybackState.Running; }
    public void Pause() { if (State == PlaybackState.Running) State = PlaybackState.Paused; }
    public void Reset()
    {
        State = PlaybackState.Ready;
        RepetitionIndex = StepIndex = 0;
        ElapsedSeconds = stepStart = 0;
        boundary = Series.Steps[0].Seconds;
    }
    public List<CueKind> Tick(TimeSpan activeElapsed)
    {
        var cues = new List<CueKind>();
        if (State != PlaybackState.Running) return cues;
        ElapsedSeconds = Math.Min(Series.DurationSeconds, Math.Max(ElapsedSeconds, activeElapsed.TotalSeconds));
        while (ElapsedSeconds + 1e-9 >= boundary && State == PlaybackState.Running)
        {
            stepStart = boundary;
            if (StepIndex + 1 < Series.Steps.Count)
            {
                StepIndex++;
                cues.Add(CueKind.Step);
            }
            else if (RepetitionIndex + 1 < Series.Repetitions)
            {
                RepetitionIndex++;
                StepIndex = 0;
                cues.Add(CueKind.Repetition);
            }
            else
            {
                State = PlaybackState.Completed;
                cues.Add(CueKind.Series);
                break;
            }
            boundary += Series.Steps[StepIndex].Seconds;
        }
        return cues;
    }
}
