using Cartex.Mobile.Core;

namespace Cartex.Mobile.Store;

public partial class App : Application
{
	public App()
	{
		MauiProgram.LocInit.GetAwaiter().GetResult();
		InitializeComponent();
		ViewModels.ProfileViewModel.ApplyTheme();
	}

	private DateTime? _sleptAt;

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());
		var services = IPlatformApplication.Current!.Services;
		services.GetRequiredService<MobileAuthService>().SessionInvalidated += OnSessionInvalidated;
		window.Deactivated += (_, _) => _sleptAt = DateTime.UtcNow;
		window.Resumed += async (_, _) =>
		{
			if (_sleptAt is null) return;
			var slept = DateTime.UtcNow - _sleptAt.Value;
			_sleptAt = null;
			if (Shell.Current is not { } shell || shell.CurrentState.Location.OriginalString.Contains("login")) return;
			_ = services.GetRequiredService<MobileAuthService>().ValidateSessionAsync();
			if (AppLock.PinEnabled && slept.TotalSeconds >= AppLock.LockAfterSeconds
				&& !shell.Navigation.ModalStack.OfType<Views.PinPage>().Any())
				await shell.GoToAsync("pin");
		};
		return window;
	}

	private static void OnSessionInvalidated() => MainThread.BeginInvokeOnMainThread(async () =>
	{
		if (Shell.Current is not { } shell || shell.CurrentState.Location.OriginalString.Contains("login")) return;
		AppLock.Disable();
		await shell.GoToAsync("//login");
		try
		{
			if (shell.CurrentPage is { } page)
				await page.DisplayAlertAsync(Loc.Instance["session_ended_title"], Loc.Instance["session_ended_msg"], Loc.Instance["ok"]);
		}
		catch { }
	});
}
