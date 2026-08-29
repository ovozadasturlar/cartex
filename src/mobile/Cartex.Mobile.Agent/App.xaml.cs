using Cartex.Mobile.Agent.Services;
using Cartex.Mobile.Core;

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
			_ = services.GetRequiredService<AccessState>().RefreshAsync();
			if (slept.TotalMinutes >= 5)
				_ = services.GetRequiredService<SyncService>().SyncAsync();
			if (AppLock.PinEnabled && slept.TotalSeconds >= AppLock.LockAfterSeconds
				&& !shell.Navigation.ModalStack.OfType<Views.PinPage>().Any())
				await shell.GoToAsync("pin");
		};
		return window;
	}

	private static void OnSessionInvalidated() => MainThread.BeginInvokeOnMainThread(() => _ = HandleSessionInvalidatedAsync());

	private static async Task HandleSessionInvalidatedAsync()
	{
		try
		{
			if (Shell.Current is not { } shell || shell.CurrentState.Location.OriginalString.Contains("login")) return;
			AppLock.Disable();
			IPlatformApplication.Current!.Services.GetRequiredService<AccessState>().Clear();
			await shell.GoToAsync("//login");
			if (shell.CurrentPage is { } page)
				await page.DisplayAlertAsync(Loc.Instance["session_ended_title"], Loc.Instance["session_ended_msg"], Loc.Instance["ok"]);
		}
		catch (Exception exception)
		{
			System.Diagnostics.Debug.WriteLine(exception);
		}
	}
}
