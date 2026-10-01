using Microsoft.Extensions.DependencyInjection;

namespace PausaActiva;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		UserAppTheme = AppTheme.Dark;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell()) { Title = "Ritmia", Width = 1180, Height = 850, MinimumWidth = 390, MinimumHeight = 640 };
		window.Stopped += (_, _) => Services.AppServices.Current.Playback.Pause();
		window.Destroying += (_, _) => Services.AppServices.Current.Playback.Pause();
		return window;
	}
}
