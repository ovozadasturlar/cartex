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

        ThemeManager.Instance.ThemeChanged += theme =>
            RequestedThemeVariant = theme == AppTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        var settings = SettingsService.Instance;

        services.AddSingleton(settings);
        DependencyInjection.RegisterServices(services, settings);

        var provider = services.BuildServiceProvider();
        ServiceLocator.Initialize(provider);

        RequestedThemeVariant = settings.Theme == AppTheme.Dark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;

        LocalizationManager.Instance.LoadLanguage(settings.Language);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var nav = provider.GetRequiredService<NavigationService>();
            var loginVm = provider.GetRequiredService<LoginViewModel>();

            var window = new MainWindow { DataContext = nav };
            nav.NavigateTo(loginVm);
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}