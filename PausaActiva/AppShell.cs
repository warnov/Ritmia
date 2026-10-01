using PausaActiva.Pages;

namespace PausaActiva;

public sealed class AppShell : Shell
{
    public AppShell()
    {
        Title = "Ritmia";
        FlyoutBehavior = FlyoutBehavior.Flyout;
        BackgroundColor = UI.Background;
        FlyoutBackgroundColor = UI.Surface;
        Shell.SetBackgroundColor(this, UI.Surface);
        Shell.SetForegroundColor(this, UI.Text);
        Shell.SetTitleColor(this, UI.Text);
        Shell.SetUnselectedColor(this, UI.Muted);
        FlyoutHeader = new VerticalStackLayout { Padding = new Thickness(24, 36, 24, 24), Spacing = 8, Children = {
            UI.Label("RITMIA", 23, UI.Mint, true), UI.Label("Move with the rhythm.\nLeave the counting to us.", 13, UI.Muted)
        } };
        Items.Add(new FlyoutItem { Title = "Reproductor", Route = "play", Items = { new ShellContent { Title = "Reproductor", ContentTemplate = new DataTemplate(() => new PlaybackPage()) } } });
        Items.Add(new FlyoutItem { Title = "Biblioteca", Route = "library", Items = { new ShellContent { Title = "Biblioteca", ContentTemplate = new DataTemplate(() => new LibraryPage()) } } });
        Items.Add(new FlyoutItem { Title = "Señales y archivo", Route = "settings", Items = { new ShellContent { Title = "Señales y archivo", ContentTemplate = new DataTemplate(() => new SettingsPage()) } } });
    }
}
