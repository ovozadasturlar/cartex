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

	protected override async void OnStart()
	{
		base.OnStart();
		await IPlatformApplication.Current!.Services.GetRequiredService<Services.StartupService>().RunAsync();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var shell = new AppShell();
		var window = new Window(shell);
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
			if (AppLock.PinEnabled && slept.TotalSeconds >= AppLock.LockAfterSeconds
				&& !shell.Navigation.ModalStack.OfType<Views.PinPage>().Any())
				await shell.GoToAsync("pin");
		};
		return window;
	}

	private static void OnSessionInvalidated() => MainThread.BeginInvokeOnMainThread(() => _ = HandleSessionEndAsync());

	private static async Task HandleSessionEndAsync()
	{
		try
		{
			if (Shell.Current is not { } shell || shell.CurrentState.Location.OriginalString.Contains("login")) return;
			AppLock.Disable();
			var services = IPlatformApplication.Current!.Services;
			services.GetRequiredService<AccessState>().Clear();
			await services.GetRequiredService<Services.OrderingHubService>().StopAsync();
			await services.GetRequiredService<Services.SmsGatewayHostService>().StopAsync();
			await services.GetRequiredService<Services.MobileHubHostService>().ApplyAsync();
			await shell.GoToAsync("//login");
			if (shell.CurrentPage is { } page)
				await page.DisplayAlertAsync(Loc.Instance["session_ended_title"], Loc.Instance["session_ended_msg"], Loc.Instance["ok"]);
		}
		catch
		{
			// Best-effort: login ekraniga o'tishning o'zi asosiy natija; dialog yoki
			// navigatsiya yiqilsa foydalanuvchi baribir login sahifasida qoladi.
		}
	}
}
