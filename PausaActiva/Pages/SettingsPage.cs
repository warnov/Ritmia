using PausaActiva.Core;
using PausaActiva.Services;

namespace PausaActiva.Pages;

public sealed class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        Title = "Señales y archivo";
        var app = AppServices.Current;
        var enabled = new Switch { IsToggled = app.Signals.Enabled, OnColor = UI.Mint };
        enabled.Toggled += (_, e) => app.Signals.Enabled = e.Value;
        var volume = new Slider { Minimum = 0, Maximum = 1, Value = app.Signals.Volume, MinimumTrackColor = UI.Mint };
        var volumeLabel = UI.Label($"Volumen: {app.Signals.Volume:P0}", 14, UI.Muted);
        volume.ValueChanged += (_, e) => { app.Signals.Volume = e.NewValue; volumeLabel.Text = $"Volumen: {e.NewValue:P0}"; };
        var tones = UI.Stack(UI.Label("Reconoce las tres señales", 25, bold: true), UI.Label("Las pruebas suenan aunque el sonido de la sesión esté desactivado.", 14, UI.Muted));
        foreach (var cue in Enum.GetValues<CueKind>())
        {
            string title = cue switch { CueKind.Step => "Paso · un tono corto", CueKind.Repetition => "Repetición · dos tonos", _ => "Serie · tres tonos ascendentes" };
            tones.Add(UI.Button($"♪  Probar {title}", async () => { app.Signals.Play(cue, preview: true); if (app.Signals.Error is { } error) await DisplayAlertAsync("Audio", error, "Entendido"); }));
        }
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 24, Spacing = 22, Children = {
            UI.Stack(UI.Label("A TU MANERA", 12, UI.Mint, true), UI.Label("Señales y configuración", 30, bold: true)),
            UI.Card(UI.Stack(UI.Row(UI.Label("Sonido de la sesión", 18, bold: true), enabled), volumeLabel, volume)),
            UI.Card(tones),
            UI.Card(UI.Stack(UI.Label("Tu biblioteca, en YAML", 23, bold: true),
                UI.Label("Todo se guarda en este dispositivo. Cada guardado conserva la versión anterior en ejercicios.yaml.bak.", 14, UI.Muted),
                UI.Label(app.Store.FilePath, 13, UI.Blue),
                UI.Button("Copiar ruta del archivo", async () => await Clipboard.Default.SetTextAsync(app.Store.FilePath)),
                UI.Button("Recargar YAML del disco", async () =>
                {
                    if (app.Playback.Engine?.State is PlaybackState.Running or PlaybackState.Paused)
                    { await DisplayAlertAsync("Sesión en curso", "Reinicia la serie antes de recargar el archivo.", "Entendido"); return; }
                    try { await app.ReloadAsync(); app.Playback.Clear(); await DisplayAlertAsync("Biblioteca recargada", "El archivo se validó y los ejercicios están disponibles.", "Entendido"); }
                    catch (Exception ex) { await UI.Error(this, ex); }
                }))),
            UI.Label("En Windows puedes trabajar en otra ventana mientras suenan las señales. En móvil, mantén la app abierta: al pasar a segundo plano, la sesión se pausa y conserva el tiempo. La pantalla permanece encendida durante el playback.", 14, UI.Muted)
        } } };
    }
}
