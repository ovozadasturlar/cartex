using Cartex.Mobile.Agent.Services;

namespace Cartex.Mobile.Agent;

public partial class App : Application
{
	public App()
	{
		Loc.Instance.InitAsync().GetAwaiter().GetResult();
		InitializeComponent();
		ViewModels.ProfileViewModel.ApplyTheme();
	}

	private DateTime? _sleptAt;

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());
		window.Deactivated += (_, _) => _sleptAt = DateTime.UtcNow;
		window.Resumed += async (_, _) =>
		{
			if (!AppLock.PinEnabled || _sleptAt is null) return;
			if ((DateTime.UtcNow - _sleptAt.Value).TotalSeconds < AppLock.LockAfterSeconds) return;
			_sleptAt = null;
			if (Shell.Current is { } shell && !shell.CurrentState.Location.OriginalString.Contains("login"))
				await shell.GoToAsync("pin");
		};
		return window;
	}
}