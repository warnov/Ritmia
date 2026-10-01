using System.Diagnostics;
using PausaActiva.Core;
using PausaActiva.Services;

namespace PausaActiva.Pages;

public sealed class PlaybackPage : ContentPage
{
    private readonly AppServices app = AppServices.Current;
    private readonly Picker exercises = new() { Title = "Selecciona un ejercicio" };
    private readonly Picker series = new() { Title = "Selecciona una variante de serie" };
    private readonly Entry count = new() { Text = "1", Keyboard = Keyboard.Numeric, Placeholder = "Veces", MaxLength = 5 };
    private readonly Label variant = UI.Label("", 14, UI.Blue, true);
    private readonly Label name = UI.Label("Tu próxima pausa empieza aquí", 30, bold: true);
    private readonly Label description = UI.Label("", 16, UI.Muted);
    private readonly Label seconds = UI.Label("—", 70, UI.Mint, true);
    private readonly Label movement = UI.Label("Sin ejercicio", 28, UI.Background, true);
    private readonly Label currentStep = UI.Label("PASO ACTUAL", 12, UI.Background, true);
    private readonly Border movementCard;
    private readonly BoxView flash = new() { InputTransparent = true, Opacity = 0, Color = UI.Mint };
    private readonly Border flashFrame = new() { InputTransparent = true, Opacity = 0, StrokeThickness = 8, BackgroundColor = Colors.Transparent };
    private long lastFlash;
    private bool visible;
    private readonly Label counter = UI.Label("SERIE —   /   REPETICIÓN —", 16, UI.Muted, true);
    private readonly Label notice = UI.Label("", 16, UI.Mint);
    private readonly Label total = UI.Label("", 13, UI.Muted);
    private readonly Label soundStatus = UI.Label("", 13, UI.Muted);
    private readonly ProgressBar progress = new() { ProgressColor = UI.Mint, BackgroundColor = UI.Raised, HeightRequest = 6 };
    private readonly VerticalStackLayout steps = new() { Spacing = 10 };
    private readonly Button start;
    private readonly Button reset;
    private readonly Grid columns = new() { ColumnSpacing = 22, RowSpacing = 22 };
    private readonly Grid selectors = new() { ColumnSpacing = 16, RowSpacing = 8 };
    private readonly VerticalStackLayout quantity;
    private readonly Border playerCard;
    private readonly Border sequenceCard;
    private readonly Ring ring = new();
    private readonly GraphicsView graphic;
    private bool refreshing;
    private string stepSignature = "";
    private bool wide;

    public PlaybackPage()
    {
        Title = "Reproductor";
        quantity = UI.Stack(UI.Label("Veces que harás esta serie", 12, UI.Muted), count);
        quantity.Spacing = 4;
        app.Playback.Initialize(Dispatcher);
        start = UI.Button("Iniciar serie", () => app.Playback.StartOrPause(), primary: true);
        reset = UI.Button("Reiniciar", async () =>
        {
            if (app.Playback.Engine is { State: PlaybackState.Running or PlaybackState.Paused } &&
                !await DisplayAlertAsync("Reiniciar sesión", "Se perderá el avance de la sesión y volverás a la serie 1.", "Reiniciar", "Cancelar")) return;
            app.Playback.Reset();
        });
        seconds.HorizontalTextAlignment = TextAlignment.Center;
        movement.HorizontalTextAlignment = TextAlignment.Center;
        currentStep.HorizontalTextAlignment = TextAlignment.Center;
        movementCard = UI.Card(UI.Stack(currentStep, movement), UI.Mint, 14);
        ((VerticalStackLayout)movementCard.Content!).Spacing = 3;
        movementCard.BackgroundColor = UI.Mint;
        movementCard.StrokeThickness = 3;
        counter.HorizontalTextAlignment = TextAlignment.Center;
        notice.HorizontalTextAlignment = TextAlignment.Center;
        graphic = new GraphicsView { Drawable = ring, HeightRequest = 180, WidthRequest = 180, HorizontalOptions = LayoutOptions.Center };
        var dial = new Grid { HeightRequest = 190 };
        dial.Add(graphic);
        var dialText = UI.Stack(seconds, UI.Label("SEGUNDOS RESTANTES", 11, UI.Muted));
        ((Label)dialText.Children[1]).HorizontalTextAlignment = TextAlignment.Center;
        dialText.Spacing = 0; dialText.VerticalOptions = LayoutOptions.Center; dial.Add(dialText);
        var controls = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12 };
        controls.Add(start); controls.Add(reset, 1);
        var playerContent = UI.Stack(variant, counter, movementCard, dial, notice, progress, total);
        playerContent.Spacing = 10;
        playerCard = UI.Card(playerContent, padding: 18);
        sequenceCard = UI.Card(UI.Stack(UI.Label("EL MOVIMIENTO, PASO A PASO", 12, UI.Muted, true), steps,
            new BoxView { HeightRequest = 1, Color = UI.Raised },
            UI.Label("Escucha el cambio", 19, bold: true),
            UI.Label("• Un tono: cambia de paso\n• Dos tonos: nueva repetición\n• Tres tonos: terminó la serie", 14, UI.Muted),
            UI.Label("Repetirás únicamente la variante elegida. Cada serie comienza cuando pulses iniciar.", 14, UI.Amber), soundStatus));
        var page = new Grid { Padding = 20, RowSpacing = 16, RowDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)] };
        page.Add(UI.Stack(UI.Label("TU RITMO / TU ESPACIO", 12, UI.Mint, true), name, description));
        page.Add(new ScrollView { Content = new VerticalStackLayout { Spacing = 16, Children = { UI.Card(selectors, padding: 14), columns } } }, 0, 1);
        page.Add(controls, 0, 2);
        var fullScreen = new Grid();
        fullScreen.Add(page);
        fullScreen.Add(flash);
        fullScreen.Add(flashFrame);
        Content = fullScreen;
        exercises.SelectedIndexChanged += (_, _) =>
        {
            if (refreshing || exercises.SelectedItem is not Exercise exercise) return;
            app.Playback.Select(exercise, plannedSeries: ValidCount() ?? 1); RefreshSeries(); stepSignature = ""; Render();
        };
        series.SelectedIndexChanged += (_, _) =>
        {
            if (refreshing || app.Playback.Exercise is null || series.SelectedIndex < 0) return;
            app.Playback.Select(app.Playback.Exercise, series.SelectedIndex, ValidCount() ?? 1); stepSignature = ""; Render();
        };
        count.TextChanged += (_, _) =>
        {
            if (refreshing) return;
            if (ValidCount() is { } times && app.Playback.Exercise is { } exercise)
                app.Playback.Select(exercise, app.Playback.SeriesIndex, times);
            Render();
        };
        SizeChanged += (_, _) => LayoutColumns();
        LayoutColumns();
    }
    private void LayoutColumns()
    {
        bool nextWide = Width >= 900;
        if (columns.Children.Count > 0 && nextWide == wide) return;
        wide = nextWide; columns.Children.Clear(); columns.ColumnDefinitions.Clear(); columns.RowDefinitions.Clear();
        selectors.Children.Clear(); selectors.ColumnDefinitions.Clear(); selectors.RowDefinitions.Clear();
        selectors.ColumnDefinitions.Add(new(GridLength.Star)); selectors.RowDefinitions.Add(new(GridLength.Auto));
        if (wide)
        {
            selectors.ColumnDefinitions.Add(new(GridLength.Star)); selectors.ColumnDefinitions.Add(new(new GridLength(200)));
            selectors.Add(exercises); selectors.Add(series, 1); selectors.Add(quantity, 2);
        }
        else
        {
            selectors.RowDefinitions.Add(new(GridLength.Auto)); selectors.RowDefinitions.Add(new(GridLength.Auto));
            selectors.Add(exercises); selectors.Add(series, 0, 1); selectors.Add(quantity, 0, 2);
        }
        columns.ColumnDefinitions.Add(new(GridLength.Star)); columns.RowDefinitions.Add(new(GridLength.Auto));
        if (wide) { columns.ColumnDefinitions.Add(new(new GridLength(0.75, GridUnitType.Star))); columns.Add(playerCard); columns.Add(sequenceCard, 1); }
        else { columns.RowDefinitions.Add(new(GridLength.Auto)); columns.Add(playerCard); columns.Add(sequenceCard, 0, 1); }
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        visible = true;
        app.Playback.Changed += Render;
        app.Playback.CueProduced += Blink;
        try
        {
            await app.EnsureLoadedAsync();
            var id = app.Playback.Exercise?.Id;
            var exercise = app.Library.Exercises.FirstOrDefault(e => e.Id == id) ?? app.Library.Exercises.FirstOrDefault();
            if (exercise is null) app.Playback.Clear();
            else if (exercise.Id != id)
                app.Playback.Select(exercise, exercise.Id == id ? app.Playback.SeriesIndex : 0);
            refreshing = true;
            exercises.ItemsSource = app.Library.Exercises;
            exercises.SelectedItem = exercise;
            refreshing = false;
            RefreshSeries(); stepSignature = ""; Render();
        }
        catch (Exception ex) { await UI.Error(this, ex); }
    }
    protected override void OnDisappearing()
    {
        visible = false;
        app.Playback.CueProduced -= Blink;
        this.AbortAnimation("ShiftLight");
        RestoreFlash();
        app.Playback.Pause(); app.Playback.Changed -= Render; base.OnDisappearing();
    }
    private void RefreshSeries()
    {
        refreshing = true;
        series.ItemsSource = app.Playback.Exercise?.Series;
        series.SelectedIndex = app.Playback.Engine is null ? -1 : app.Playback.SeriesIndex;
        count.Text = (app.Playback.Session?.PlannedSeries ?? 1).ToString();
        refreshing = false;
    }
    private int? ValidCount() => int.TryParse(count.Text, out int number) && number is >= 1 and <= 10000 ? number : null;
    private void Render()
    {
        var playback = app.Playback;
        var engine = playback.Engine;
        name.Text = playback.Exercise?.Name ?? "Tu próxima pausa empieza aquí";
        description.Text = playback.Exercise?.Description ?? "Crea tus ejercicios en la biblioteca y deja que las señales lleven la cuenta.";
        bool busy = engine?.State is PlaybackState.Running or PlaybackState.Paused;
        exercises.IsEnabled = series.IsEnabled = count.IsEnabled = !busy;
        start.IsEnabled = engine is not null && ValidCount().HasValue && app.Playback.Session?.Completed != true;
        reset.IsEnabled = engine is not null;
        start.Text = engine?.State switch {
            PlaybackState.Running => "Ⅱ  Pausar", PlaybackState.Paused => "▶  Continuar",
            PlaybackState.Completed when playback.Session?.Completed == true => "✓  Sesión completada",
            PlaybackState.Completed => $"▶  Iniciar serie {playback.Session!.SeriesNumber + 1}", _ => "▶  Iniciar serie"
        };
        if (engine is not null && series.SelectedIndex != playback.SeriesIndex) RefreshSeries();
        seconds.Text = engine is null ? "—" : engine.RemainingSeconds.ToString("0.0");
        movement.Text = engine is null ? "Añade una serie para empezar" : engine.State == PlaybackState.Completed ? "¡Serie completada!" : engine.Series.Steps[engine.StepIndex].Name;
        currentStep.Text = engine is null ? "PASO ACTUAL" : engine.State == PlaybackState.Completed ? "FIN DE SERIE" : $"▶  PASO ACTUAL {engine.StepIndex + 1} / {engine.Series.Steps.Count}";
        movementCard.BackgroundColor = engine?.State == PlaybackState.Completed ? UI.Amber : UI.Mint;
        variant.Text = engine is null ? "" : $"VARIANTE: {engine.Series.Name} · {engine.Series.Repetitions} repeticiones por serie";
        counter.Text = engine is null ? "SERIE —   /   REPETICIÓN —" : $"SERIE {playback.Session!.SeriesNumber} / {playback.Session.PlannedSeries}    •    REPETICIÓN {engine.RepetitionIndex + 1} / {engine.Series.Repetitions}";
        notice.Text = ValidCount().HasValue ? playback.Notice : "Escribe entre 1 y 10000 veces";
        double sessionProgress = engine is null ? 0 : playback.Session!.ElapsedSeconds / playback.Session.DurationSeconds;
        progress.Progress = sessionProgress;
        total.Text = engine is null ? "" : $"Sesión: {UI.Duration(playback.Session!.ElapsedSeconds)} de {UI.Duration(playback.Session.DurationSeconds)} · {sessionProgress:P0}";
        var color = playback.CueVisible ? playback.LastCue switch { CueKind.Repetition => UI.Blue, CueKind.Series => UI.Amber, _ => UI.Mint } : UI.Mint;
        ring.Progress = engine?.StepProgress ?? 0; ring.Color = color; graphic.Invalidate();
        seconds.TextColor = notice.TextColor = color; playerCard.Stroke = color;
        soundStatus.Text = app.Signals.Error ?? (app.Signals.Enabled ? "Sonido activo · ajusta y prueba los tonos en el menú." : "Sonido desactivado · actívalo en Señales y archivo.");
        var signature = $"{playback.Exercise?.Id}/{playback.SeriesIndex}/{engine?.StepIndex}/{engine?.State}";
        if (stepSignature == signature) return;
        stepSignature = signature; steps.Clear();
        if (engine is null) { steps.Add(UI.Label("Aquí verás los pasos de tu serie.", 14, UI.Muted)); return; }
        for (int i = 0; i < engine.Series.Steps.Count; i++)
        {
            bool active = i == engine.StepIndex && engine.State != PlaybackState.Completed;
            var step = engine.Series.Steps[i];
            var row = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12 };
            row.Add(UI.Label(active ? $"▶ {i + 1:00}" : $"{i + 1:00}", active ? 24 : 18, active ? UI.Background : UI.Muted, true));
            row.Add(active ? UI.Stack(UI.Label("AHORA", 11, UI.Background, true), UI.Label(step.Name, 23, UI.Background, true)) : UI.Label(step.Name, 17, UI.Muted), 1);
            row.Add(UI.Label($"{step.Seconds:0.#} s", active ? 20 : 14, active ? UI.Background : UI.Muted, active), 2);
            var stepCard = UI.Card(row, active ? UI.Mint : UI.Raised, active ? 18 : 14);
            stepCard.BackgroundColor = active ? UI.Mint : UI.Surface;
            stepCard.StrokeThickness = active ? 3 : 1;
            stepCard.Opacity = active || engine.State == PlaybackState.Completed ? 1 : 0.55;
            steps.Add(stepCard);
        }
    }
    private void Blink(CueKind cue)
    {
        if (!visible) return;
        // Keep pulses separated even for very short steps; never build an animation queue.
        if (lastFlash != 0 && Stopwatch.GetElapsedTime(lastFlash).TotalMilliseconds < 600) return;
        lastFlash = Stopwatch.GetTimestamp();
        this.AbortAnimation("ShiftLight");
        var light = cue switch { CueKind.Repetition => UI.Blue, CueKind.Series => UI.Amber, _ => Color.FromArgb("#D4FF70") };
        flash.Color = light;
        flashFrame.Stroke = light;
        Shell.SetBackgroundColor(Shell.Current, light);
        Shell.SetForegroundColor(Shell.Current, UI.Background);
        Shell.SetTitleColor(Shell.Current, UI.Background);
        var pulse = new Animation();
        pulse.Add(0, 0.15, new Animation(v => { flash.Opacity = v; flashFrame.Opacity = v / 0.65; }, 0, 0.65));
        pulse.Add(0.15, 0.4, new Animation(_ => { flash.Opacity = 0.65; flashFrame.Opacity = 1; }));
        pulse.Add(0.4, 1, new Animation(v => { flash.Opacity = v; flashFrame.Opacity = v / 0.65; }, 0.65, 0, Easing.CubicOut));
        pulse.Commit(this, "ShiftLight", 16, 420, finished: (_, _) => RestoreFlash());
    }
    private void RestoreFlash()
    {
        flash.Opacity = flashFrame.Opacity = 0;
        Shell.SetBackgroundColor(Shell.Current, UI.Surface);
        Shell.SetForegroundColor(Shell.Current, UI.Text);
        Shell.SetTitleColor(Shell.Current, UI.Text);
    }
    private sealed class Ring : IDrawable
    {
        public double Progress { get; set; }
        public Color Color { get; set; } = UI.Mint;
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            float size = Math.Min(dirtyRect.Width, dirtyRect.Height) - 22;
            float x = (dirtyRect.Width - size) / 2, y = (dirtyRect.Height - size) / 2;
            canvas.StrokeSize = 8; canvas.StrokeColor = UI.Raised;
            canvas.DrawEllipse(x, y, size, size);
            canvas.StrokeColor = Color; canvas.StrokeLineCap = LineCap.Round;
            if (Progress >= 0.999) canvas.DrawEllipse(x, y, size, size);
            else if (Progress > 0) canvas.DrawArc(x, y, size, size, 90, (float)(90 - 360 * Progress), true, false);
        }
    }
}
