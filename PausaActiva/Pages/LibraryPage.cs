using PausaActiva.Core;
using PausaActiva.Services;

namespace PausaActiva.Pages;

public sealed class LibraryPage : ContentPage
{
    private readonly AppServices app = AppServices.Current;
    private readonly VerticalStackLayout list = new() { Spacing = 16 };
    private readonly SearchBar search = new() { Placeholder = "Buscar ejercicio…", TextColor = UI.Text, PlaceholderColor = UI.Muted, BackgroundColor = UI.Surface };
    public LibraryPage()
    {
        Title = "Biblioteca";
        search.TextChanged += (_, _) => Render();
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 24, Spacing = 22, Children = {
            UI.Stack(UI.Label("TU BIBLIOTECA", 12, UI.Mint, true), UI.Label("Movimientos a tu medida", 30, bold: true),
                UI.Label("Cada serie es una variante. Elige una y cuántas veces hacerla en el reproductor.", 15, UI.Muted)),
            UI.Button("+  Crear ejercicio", async () => await Navigation.PushAsync(new ExerciseEditorPage(new Exercise())), primary: true), search, list
        } } };
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await app.EnsureLoadedAsync(); Render(); } catch (Exception ex) { await UI.Error(this, ex); }
    }
    private void Render()
    {
        list.Clear();
        var matches = app.Library.Exercises.Where(e => e.Name.Contains(search.Text ?? "", StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0) { list.Add(UI.Label("No hay ejercicios. Crea uno para configurar tu primera serie.", 16, UI.Muted)); return; }
        foreach (var exercise in matches)
        {
            var content = UI.Stack(UI.Label(exercise.Name, 23, bold: true), UI.Label(exercise.Description, 14, UI.Muted),
                UI.Label($"{exercise.Series.Count} variantes de serie disponibles", 13, UI.Mint));
            foreach (var series in exercise.Series) content.Add(UI.Label($"{series.Name}   /   {series.Repetitions} rep.   /   {series.Steps.Count} pasos   /   {UI.Duration(series.DurationSeconds)} por serie", 14, UI.Muted));
            var actions = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Direction = Microsoft.Maui.Layouts.FlexDirection.Row };
            var play = UI.Button("▶  Practicar", async () =>
            {
                app.Playback.Select(exercise); await Shell.Current.GoToAsync("//play");
            }, primary: true);
            play.IsEnabled = exercise.Series.Count > 0;
            actions.Add(play);
            actions.Add(UI.Button("Editar", async () => await Navigation.PushAsync(new ExerciseEditorPage(exercise))));
            actions.Add(UI.Button("Eliminar", async () =>
            {
                if (!await DisplayAlertAsync("Eliminar ejercicio", $"¿Eliminar «{exercise.Name}» y todas sus series?", "Eliminar", "Cancelar")) return;
                try
                {
                    await app.DeleteExerciseAsync(exercise.Id);
                    if (app.Playback.Exercise?.Id == exercise.Id) app.Playback.Clear();
                    Render();
                }
                catch (Exception ex) { await UI.Error(this, ex); }
            }, danger: true));
            foreach (View button in actions.Children) button.Margin = new Thickness(0, 0, 8, 8);
            content.Add(actions); list.Add(UI.Card(content));
        }
    }
}
