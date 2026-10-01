using System.Globalization;
using PausaActiva.Core;
using PausaActiva.Services;

namespace PausaActiva.Pages;

public sealed class ExerciseEditorPage : ContentPage
{
    private readonly Exercise draft;
    private readonly Entry name;
    private readonly Editor description;
    private readonly VerticalStackLayout seriesList = new() { Spacing = 20 };
    private bool dirty;
    private bool saving;
    public ExerciseEditorPage(Exercise exercise)
    {
        draft = exercise.Copy();
        Title = "Editar ejercicio";
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior { Command = new Command(async () => await CancelAsync()) });
        name = new Entry { Text = draft.Name, Placeholder = "Nombre del ejercicio", MaxLength = 120 };
        description = new Editor { Text = draft.Description, Placeholder = "Guía de postura y movimientos…", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 100 };
        name.TextChanged += (_, e) => { draft.Name = e.NewTextValue ?? ""; dirty = true; };
        description.TextChanged += (_, e) => { draft.Description = e.NewTextValue ?? ""; dirty = true; };
        var cancel = new ToolbarItem { Text = "Cancelar" }; cancel.Clicked += async (_, _) => await CancelAsync(); ToolbarItems.Add(cancel);
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 24, Spacing = 22, Children = {
            UI.Label("Diseña el ritmo de tu ejercicio", 27, bold: true),
            UI.Card(UI.Stack(UI.Label("Nombre", 13, UI.Muted), name, UI.Label("Descripción / guía de referencia", 13, UI.Muted), description)),
            UI.Stack(UI.Label("Variantes de serie", 23, bold: true), UI.Label("Cada variante define repeticiones y pasos. En el reproductor elegirás una variante y cuántas veces hacerla.", 14, UI.Muted)),
            seriesList,
            UI.Button("+  Añadir variante", () => { draft.Series.Add(new ExerciseSeries { Name = draft.Series.Count == 0 ? "Main" : $"Variante {draft.Series.Count + 1}", Steps = [new ExerciseStep()] }); dirty = true; RenderSeries(); }),
            UI.Button("Guardar ejercicio", async () => await SaveAsync(), primary: true)
        } } };
        RenderSeries();
    }
    private void RenderSeries()
    {
        seriesList.Clear();
        if (draft.Series.Count == 0) seriesList.Add(UI.Label("Añade una serie para poder reproducir este ejercicio.", 15, UI.Amber));
        foreach (var series in draft.Series)
        {
            var seriesName = new Entry { Text = series.Name, Placeholder = "Nombre de la serie" };
            seriesName.TextChanged += (_, e) => { series.Name = e.NewTextValue ?? ""; dirty = true; };
            var repeats = new Entry { Text = series.Repetitions.ToString(), Keyboard = Keyboard.Numeric, Placeholder = "Número de repeticiones" };
            repeats.TextChanged += (_, e) => { series.Repetitions = int.TryParse(e.NewTextValue, out int n) ? n : 0; dirty = true; };
            var stepList = new VerticalStackLayout { Spacing = 12 };
            var content = UI.Stack(UI.Label($"VARIANTE {draft.Series.IndexOf(series) + 1:00}", 12, UI.Mint, true),
                UI.Label("Nombre de la serie", 13, UI.Muted), seriesName, UI.Label("Repeticiones", 13, UI.Muted), repeats,
                UI.Label("Pasos · duración en segundos", 15, bold: true), stepList);
            for (int i = 0; i < series.Steps.Count; i++)
            {
                var step = series.Steps[i];
                int index = i;
                var stepName = new Entry { Text = step.Name, Placeholder = "Nombre / movimiento" };
                stepName.TextChanged += (_, e) => { step.Name = e.NewTextValue ?? ""; dirty = true; };
                var seconds = new Entry { Text = step.Seconds.ToString(CultureInfo.CurrentCulture), Keyboard = Keyboard.Numeric, Placeholder = "Segundos" };
                seconds.TextChanged += (_, e) =>
                {
                    // Accept either decimal comma or decimal point without interpreting thousands.
                    step.Seconds = double.TryParse((e.NewTextValue ?? "").Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double n) ? n : 0;
                    dirty = true;
                };
                var fields = new Grid { ColumnDefinitions = [new(GridLength.Star), new(new GridLength(100))], ColumnSpacing = 12 };
                fields.Add(UI.Stack(UI.Label($"PASO {i + 1}", 11, UI.Muted), stepName));
                fields.Add(UI.Stack(UI.Label("SEGUNDOS", 11, UI.Muted), seconds), 1);
                var actions = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
                var up = UI.Button("↑", () => { (series.Steps[index - 1], series.Steps[index]) = (series.Steps[index], series.Steps[index - 1]); dirty = true; RenderSeries(); });
                up.IsEnabled = index > 0; SemanticProperties.SetDescription(up, "Mover paso hacia arriba");
                var down = UI.Button("↓", () => { (series.Steps[index + 1], series.Steps[index]) = (series.Steps[index], series.Steps[index + 1]); dirty = true; RenderSeries(); });
                down.IsEnabled = index < series.Steps.Count - 1; SemanticProperties.SetDescription(down, "Mover paso hacia abajo");
                actions.Add(up); actions.Add(down);
                actions.Add(UI.Button("Quitar paso", () => { series.Steps.Remove(step); dirty = true; RenderSeries(); }, danger: true));
                foreach (View action in actions.Children) action.Margin = new Thickness(0, 0, 8, 0);
                stepList.Add(UI.Card(UI.Stack(fields, actions), padding: 14));
            }
            content.Add(UI.Button("+  Añadir paso", () => { series.Steps.Add(new ExerciseStep { Name = $"Paso {series.Steps.Count + 1}" }); dirty = true; RenderSeries(); }));
            var seriesActions = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
            seriesActions.Add(UI.Button("Duplicar variante", () => { var copy = series.Copy(); copy.Name += " (copia)"; draft.Series.Insert(draft.Series.IndexOf(series) + 1, copy); dirty = true; RenderSeries(); }));
            seriesActions.Add(UI.Button("Eliminar variante", async () =>
            {
                if (!await DisplayAlertAsync("Eliminar variante", $"¿Eliminar «{series.Name}»?", "Eliminar", "Cancelar")) return;
                draft.Series.Remove(series); dirty = true; RenderSeries();
            }, danger: true));
            foreach (View action in seriesActions.Children) action.Margin = new Thickness(0, 0, 8, 8);
            content.Add(seriesActions); seriesList.Add(UI.Card(content));
        }
    }
    private async Task SaveAsync()
    {
        if (saving) return;
        saving = true;
        try
        {
            draft.Name = draft.Name.Trim();
            LibraryRules.Validate(draft);
            await AppServices.Current.SaveExerciseAsync(draft);
            if (AppServices.Current.Playback.Exercise?.Id == draft.Id) AppServices.Current.Playback.Select(draft);
            dirty = false;
            await Navigation.PopAsync();
        }
        catch (Exception ex) { await UI.Error(this, ex); }
        finally { saving = false; }
    }
    private async Task CancelAsync()
    {
        if (dirty && !await DisplayAlertAsync("Descartar cambios", "Los cambios de este ejercicio aún no están guardados.", "Descartar", "Seguir editando")) return;
        await Navigation.PopAsync();
    }
    protected override bool OnBackButtonPressed() { _ = CancelAsync(); return true; }
}
