using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Domain.Common;
using Cartex.Domain.Events;
using Cartex.Infrastructure.Catalog;
using Cartex.Infrastructure.CloudBridge;
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
        services.AddSingleton<IQrLoginStore, QrLoginStore>();

        services.AddHttpClient();
        services.AddScoped<ITelegramService, TelegramService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ISmsService, SmsService>();
        services.AddScoped<ISmsProvider, EskizSmsProvider>();
        services.AddScoped<ISmsProvider, PlayMobileSmsProvider>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IProductCatalogProvider, OpenFoodFactsProvider>();
        services.AddSingleton<ISpreadsheetService, Import.ClosedXmlSpreadsheetService>();
        services.AddScoped<Storage.LocalObjectStorage>();
        services.AddScoped<Storage.MinioObjectStorage>();
        services.AddScoped<IObjectStorage, Storage.RoutedObjectStorage>();
        services.AddSingleton<IImageProcessor, Storage.SkiaImageProcessor>();
        services.AddSingleton<IPushGateway, Push.NullPushGateway>();
        services.AddHostedService<OutboxProcessor>();
        services.AddHostedService<DebtReminderScheduler>();
        services.AddHostedService<SmsStatusPoller>();
        services.AddHostedService<Catalog.PrepackExpiryService>();
        services.AddScoped<IReceiptPdfRenderer, ReceiptPdfRenderer>();
        services.AddScoped<CloudBridgeClient>();
        services.AddTransient<INotificationHandler<DomainEventNotification<ReceiptMirrorEvent>>, ReceiptMirrorHandler>();

        return services;
    }
}
