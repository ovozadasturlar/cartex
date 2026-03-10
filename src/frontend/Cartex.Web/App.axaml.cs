using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Cartex.UI;
using Cartex.UI.Models;
using Cartex.UI.Services;
using Cartex.UI.ViewModels;
using Cartex.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Cartex.Web;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        ThemeManager.ApplyTheme = theme =>
            RequestedThemeVariant = theme == AppTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
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

            if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                var nav = provider.GetRequiredService<NavigationService>();
                var loginVm = provider.GetRequiredService<LoginViewModel>();
                nav.NavigateTo(loginVm);

                singleView.MainView = new AppShell { DataContext = nav };
            }
        }
        catch (Exception ex)
        {
            if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                singleView.MainView = new TextBlock
                {
                    Text = $"Init error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Thickness(20),
                    FontSize = 14
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
