using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Cartex.UI;
using Cartex.UI.Models;
using Cartex.UI.Services;
using Cartex.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Cartex.Desktop;

public partial class App : Application
{
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
		Cartex.UI.Controls.WheelGuard.Install();

		ThemeManager.Instance.ThemeChanged += theme =>
			RequestedThemeVariant = theme == AppTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
	}

	public override void OnFrameworkInitializationCompleted()
	{
		// 1. AppData papkasini aniqlaymiz va mavjud bo'lmasa yaratamiz
		string appDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cartex");
		if (!Directory.Exists(appDataRoot))
		{
			Directory.CreateDirectory(appDataRoot);
		}

		// 2. Dasturning joriy ishchi papkasini AppData'ga o'zgartiramiz.
		// Shunda nisbiy yo'l bilan yoziladigan barcha loglar va bazalar .exe yonida emas, shu yerda yaratiladi.
		Environment.CurrentDirectory = appDataRoot;

		// 3. Rasm keshini AppData ichiga yo'naltiramiz
		AsyncImageLoader.ImageLoader.AsyncImageLoader = new CachedImageLoader(Path.Combine(appDataRoot, "imagecache"));

		var services = new ServiceCollection();
		var settings = SettingsService.Instance;

		services.AddSingleton(settings);

		// Loyihangizdagi asl 2 ta argumentli metod (xatolik bermaydi)
		DependencyInjection.RegisterServices(services, settings);

		var provider = services.BuildServiceProvider();
		ServiceLocator.Initialize(provider);

		RequestedThemeVariant = settings.Theme == AppTheme.Dark
			? ThemeVariant.Dark
			: ThemeVariant.Light;
		ThemeManager.Instance.Theme = settings.Theme;

		LocalizationManager.Instance.LoadLanguage(settings.Language);

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			var nav = provider.GetRequiredService<NavigationService>();
			var window = new MainWindow { DataContext = nav };

			if (settings.RememberMe && provider.GetRequiredService<AuthService>().TryRestore())
			{
				var mainVm = provider.GetRequiredService<MainViewModel>();
				mainVm.Initialize();
				nav.NavigateTo(mainVm);
				provider.GetRequiredService<ReferenceCache>();
				_ = provider.GetRequiredService<AuthService>().ValidateSessionAsync();
			}
			else
			{
				nav.NavigateTo(provider.GetRequiredService<LoginViewModel>());
			}

			desktop.MainWindow = window;
		}

		base.OnFrameworkInitializationCompleted();
	}
}