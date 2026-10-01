namespace PausaActiva.Core;

// A session repeats one chosen variant; other variants are never part of its sequence.
public sealed class PlaybackSession
{
    public Exercise Exercise { get; }
    public int SeriesIndex { get; }
    public int PlannedSeries { get; }
    public int SeriesNumber { get; private set; } = 1;
    public PlaybackEngine? Engine { get; }
    public bool Completed => Engine?.State == PlaybackState.Completed && SeriesNumber == PlannedSeries;
    public double DurationSeconds => (Engine?.Series.DurationSeconds ?? 0) * PlannedSeries;
    public double ElapsedSeconds => (Engine?.Series.DurationSeconds ?? 0) * (SeriesNumber - 1) + (Engine?.ElapsedSeconds ?? 0);

    public PlaybackSession(Exercise exercise, int seriesIndex, int plannedSeries)
    {
        LibraryRules.Validate(exercise);
        if (plannedSeries is < 1 or > 10000) throw new ArgumentOutOfRangeException(nameof(plannedSeries), "Elige entre 1 y 10000 ejecuciones de la serie.");
        if (exercise.Series.Count > 0 && (seriesIndex < 0 || seriesIndex >= exercise.Series.Count)) throw new ArgumentOutOfRangeException(nameof(seriesIndex));
        Exercise = exercise.Copy();
        SeriesIndex = seriesIndex;
        PlannedSeries = plannedSeries;
        Engine = Exercise.Series.Count > 0 ? new(Exercise.Series[seriesIndex]) : null;
    }
    public bool Start()
    {
        if (Engine is null || Completed) return false;
        if (Engine.State == PlaybackState.Completed)
        {
            SeriesNumber++;
            Engine.Reset();
        }
        Engine.Start();
        return true;
    }
    public void Reset() { SeriesNumber = 1; Engine?.Reset(); }
}
