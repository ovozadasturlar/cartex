using Cartex.Application.Common.Interfaces;
using Cartex.Domain.Common;
using Cartex.Infrastructure.Catalog;
using Cartex.Infrastructure.Features;
using Cartex.Infrastructure.Licensing;
using Cartex.Infrastructure.Notifications;
using Cartex.Infrastructure.Notifications.Email;
using Cartex.Infrastructure.Notifications.Sms;
using Cartex.Infrastructure.Notifications.Telegram;
using Cartex.Infrastructure.Security;
using Cartex.Infrastructure.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cartex.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();

        var keysPath = configuration["DataProtection:KeysPath"];
        if (string.IsNullOrWhiteSpace(keysPath))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            keysPath = string.IsNullOrWhiteSpace(localAppData)
                ? Path.Combine(AppContext.BaseDirectory, "dp-keys")
                : Path.Combine(localAppData, "Cartex", "keys");
        }
        Directory.CreateDirectory(keysPath);

        services.AddDataProtection()
            .SetApplicationName("Cartex")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        services.AddScoped<IFeatureStateProvider, FeatureStateProvider>();
        services.AddScoped<ILicenseService, LicenseService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddSingleton<ISecretProtector, SecretProtector>();
        services.AddScoped<IHardwareKeyService, HardwareKeyService>();

        services.AddHttpClient();
        services.AddScoped<ITelegramService, TelegramService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ISmsService, SmsService>();
        services.AddScoped<ISmsProvider, EskizSmsProvider>();
        services.AddScoped<ISmsProvider, PlayMobileSmsProvider>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IProductCatalogProvider, OpenFoodFactsProvider>();
        services.AddScoped<IObjectStorage, Storage.MinioObjectStorage>();
        services.AddHostedService<OutboxProcessor>();
        services.AddHostedService<DebtReminderScheduler>();
        services.AddScoped<IReceiptPdfRenderer, ReceiptPdfRenderer>();

        return services;
    }
}
