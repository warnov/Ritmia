namespace PausaActiva.Core;

public sealed class ExerciseLibrary
{
    public int Version { get; set; } = 1;
    public List<Exercise> Exercises { get; set; } = [];
}

public sealed class Exercise
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Nuevo ejercicio";
    public string Description { get; set; } = "";
    public List<ExerciseSeries> Series { get; set; } = [];
    public Exercise Copy() => new() { Id = Id, Name = Name, Description = Description, Series = Series.Select(s => s.Copy()).ToList() };
    public override string ToString() => Name;
}

public sealed class ExerciseSeries
{
    public string Name { get; set; } = "Serie";
    public string Type { get; set; } = "repetitions";
    public int Repetitions { get; set; } = 8;
    public List<ExerciseStep> Steps { get; set; } = [];
    public ExerciseSeries Copy() => new() { Name = Name, Type = Type, Repetitions = Repetitions, Steps = Steps.Select(s => new ExerciseStep { Name = s.Name, Seconds = s.Seconds }).ToList() };
    [YamlDotNet.Serialization.YamlIgnore]
    public double DurationSeconds => Repetitions * Steps.Sum(s => s.Seconds);
    public override string ToString() => $"{Name} · {Repetitions} repeticiones";
}

public sealed class ExerciseStep
{
    public string Name { get; set; } = "Movimiento";
    public double Seconds { get; set; } = 3;
}

public static class LibraryRules
{
    public static void Validate(ExerciseLibrary library)
    {
        if (library.Version != 1) throw new InvalidDataException("Esta versión del archivo YAML no es compatible.");
        if (library.Exercises is null) throw new InvalidDataException("Falta la lista de ejercicios.");
        var ids = new HashSet<string>();
        foreach (var exercise in library.Exercises)
        {
            if (exercise is null || string.IsNullOrWhiteSpace(exercise.Id) || !ids.Add(exercise.Id))
                throw new InvalidDataException("Cada ejercicio debe tener un identificador único.");
            Validate(exercise);
        }
    }
    public static void Validate(Exercise exercise)
    {
        if (string.IsNullOrWhiteSpace(exercise.Name)) throw new InvalidDataException("Escribe el nombre del ejercicio.");
        if (exercise.Series is null) throw new InvalidDataException("Falta la lista de series.");
        foreach (var series in exercise.Series) Validate(series);
    }
    public static void Validate(ExerciseSeries series)
    {
        if (series is null || string.IsNullOrWhiteSpace(series.Name)) throw new InvalidDataException("Cada serie necesita un nombre.");
        if (series.Type != "repetitions") throw new InvalidDataException("Por ahora solo se admiten series de repeticiones.");
        if (series.Repetitions < 1 || series.Repetitions > 10000) throw new InvalidDataException("Las repeticiones deben estar entre 1 y 10000.");
        if (series.Steps is null || series.Steps.Count == 0) throw new InvalidDataException("Cada serie necesita al menos un paso.");
        foreach (var step in series.Steps)
            if (step is null || string.IsNullOrWhiteSpace(step.Name) || !double.IsFinite(step.Seconds) || step.Seconds < 0.1 || step.Seconds > 86400)
                throw new InvalidDataException("Cada paso necesita un nombre y una duración entre 0,1 y 86400 segundos.");
    }
}

public static class SampleLibrary
{
    public static ExerciseLibrary Create() => new() { Exercises = [
        new Exercise { Name = "Movilidad de hombros", Description = "Sigue los movimientos configurados a tu ritmo. Mantén una postura cómoda y evita forzar el rango de movimiento.", Series = [
            new ExerciseSeries { Name = "Serie 1", Repetitions = 6, Steps = [new() { Name = "Elevar", Seconds = 3 }, new() { Name = "Sostener", Seconds = 2 }, new() { Name = "Bajar", Seconds = 3 }] },
            new ExerciseSeries { Name = "Serie 2", Repetitions = 8, Steps = [new() { Name = "Elevar", Seconds = 3 }, new() { Name = "Sostener", Seconds = 2 }, new() { Name = "Bajar", Seconds = 3 }] }
        ] },
        new Exercise { Name = "Elevación de talones", Description = "Usa un apoyo estable si lo necesitas. El ejemplo es editable: ajusta los pasos y tiempos a tu rutina.", Series = [
            new ExerciseSeries { Name = "Serie 1", Repetitions = 10, Steps = [new() { Name = "Subir", Seconds = 2 }, new() { Name = "Bajar", Seconds = 2 }] }
        ] }
    ] };
}
