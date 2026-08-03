using System.Text.Json;
using System.Text.Json.Serialization;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Cartex.ApiClient;

public static class DependencyInjection
{
    public static IServiceCollection AddApiClients(this IServiceCollection services, Func<string> baseUrlProvider, Func<string?> tokenProvider, Func<CancellationToken, Task<string?>>? refreshAsync = null, Action? onUnauthorized = null, string clientName = "desktop", TimeSpan? timeout = null, Func<string?>? deviceIdProvider = null, Func<string?>? deviceNameProvider = null)
    {
        var settings = new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter(), new LocalDateTimeConverter() }
            })
        };

        services.AddTransient(_ => new AuthTokenHandler(tokenProvider, refreshAsync, onUnauthorized));
        services.AddTransient<NoContentHandler>();
        services.AddTransient(_ => new BaseAddressHandler(baseUrlProvider));
        services.AddTransient(_ => new DeviceMetadataHandler(deviceIdProvider, deviceNameProvider));
        services.AddSingleton<PageRequestScope>();
        services.AddTransient<PageRequestScopeHandler>();

        var baseUrl = baseUrlProvider();

        services.AddRefitClient<IAuthApi>(settings)
            .ConfigureHttpClient(c => Configure(c, baseUrl, clientName, timeout, deviceIdProvider, deviceNameProvider))
            .AddHttpMessageHandler<BaseAddressHandler>()
            .AddHttpMessageHandler<DeviceMetadataHandler>()
            .AddHttpMessageHandler<NoContentHandler>();

        services.AddRefitClient<IReceiptApi>(settings)
            .ConfigureHttpClient(c => Configure(c, baseUrl, clientName, timeout, deviceIdProvider, deviceNameProvider))
            .AddHttpMessageHandler<BaseAddressHandler>()
            .AddHttpMessageHandler<DeviceMetadataHandler>()
            .AddHttpMessageHandler<NoContentHandler>();

        RegisterAuthorized<IBranchesApi>(services, settings, baseUrl, clientName, timeout, deviceIdProvider, deviceNameProvider);
        RegisterAuthorized<IUsersApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IRolesApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IPermissionsApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IProductsApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IProductTypesApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<ICategoriesApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IUnitsApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IBarcodesApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IWarehousesApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IStocksApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IStockTransfersApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<ISalesApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<ISuppliesApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<ICustomersApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<ISuppliersApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IAccountsApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<ITransactionsApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<ILoyaltyApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IAuditLogsApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IReportsApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IExpenseCategoriesApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IOrderingApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IFeaturesApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<ILicenseApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<ISettingsApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<INotificationsApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IStorageApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IShiftsApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IHardwareKeysApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IBusinessApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IRatesApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<ISessionsApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IPrepacksApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IAgentApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IOfflineCacheApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IManufacturersApi>(services, settings, baseUrl, clientName, timeout);
        RegisterAuthorized<IPrintingApi>(services, settings, baseUrl, clientName, timeout, deviceIdProvider, deviceNameProvider);

        return services;
    }

    private static void Configure(HttpClient client, string baseUrl, string clientName, TimeSpan? timeout, Func<string?>? deviceIdProvider = null, Func<string?>? deviceNameProvider = null)
    {
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = timeout ?? TimeSpan.FromMinutes(5);
        client.DefaultRequestHeaders.Add("X-Client", clientName);
        var deviceId = deviceIdProvider?.Invoke();
        var deviceName = deviceNameProvider?.Invoke();
        if (!string.IsNullOrWhiteSpace(deviceId)) client.DefaultRequestHeaders.TryAddWithoutValidation("X-Device-Id", deviceId);
        if (!string.IsNullOrWhiteSpace(deviceName)) client.DefaultRequestHeaders.TryAddWithoutValidation("X-Device-Name", deviceName);
    }

    private static void RegisterAuthorized<T>(IServiceCollection services, RefitSettings settings, string baseUrl, string clientName, TimeSpan? timeout, Func<string?>? deviceIdProvider = null, Func<string?>? deviceNameProvider = null) where T : class =>
        services.AddRefitClient<T>(settings)
            .ConfigureHttpClient(c => Configure(c, baseUrl, clientName, timeout, deviceIdProvider, deviceNameProvider))
            .AddHttpMessageHandler<BaseAddressHandler>()
            .AddHttpMessageHandler<DeviceMetadataHandler>()
            .AddHttpMessageHandler<PageRequestScopeHandler>()
            .AddHttpMessageHandler<AuthTokenHandler>()
            .AddHttpMessageHandler<NoContentHandler>();
}
