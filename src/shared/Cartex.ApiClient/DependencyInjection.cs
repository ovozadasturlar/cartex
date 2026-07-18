using System.Text.Json;
using System.Text.Json.Serialization;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Cartex.ApiClient;

public static class DependencyInjection
{
    public static IServiceCollection AddApiClients(this IServiceCollection services, Func<string> baseUrlProvider, Func<string?> tokenProvider, Func<CancellationToken, Task<string?>>? refreshAsync = null, Action? onUnauthorized = null, string clientName = "desktop")
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
        services.AddSingleton<PageRequestScope>();
        services.AddTransient<PageRequestScopeHandler>();

        var baseUrl = baseUrlProvider();

        services.AddRefitClient<IAuthApi>(settings)
            .ConfigureHttpClient(c => Configure(c, baseUrl, clientName))
            .AddHttpMessageHandler<BaseAddressHandler>()
            .AddHttpMessageHandler<NoContentHandler>();

        services.AddRefitClient<IReceiptApi>(settings)
            .ConfigureHttpClient(c => Configure(c, baseUrl, clientName))
            .AddHttpMessageHandler<BaseAddressHandler>()
            .AddHttpMessageHandler<NoContentHandler>();

        RegisterAuthorized<IBranchesApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IUsersApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IRolesApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IPermissionsApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IProductsApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IProductTypesApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<ICategoriesApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IUnitsApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IBarcodesApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IWarehousesApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IStocksApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IStockTransfersApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<ISalesApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<ISuppliesApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<ICustomersApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<ISuppliersApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IAccountsApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<ITransactionsApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<ILoyaltyApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IAuditLogsApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IReportsApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IExpenseCategoriesApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IOrderingApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IFeaturesApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<ILicenseApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<ISettingsApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IStorageApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IShiftsApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IHardwareKeysApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IBusinessApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IRatesApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<ISessionsApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IPrepacksApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IAgentApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IOfflineCacheApi>(services, settings, baseUrl, clientName);
        RegisterAuthorized<IManufacturersApi>(services, settings, baseUrl, clientName);

        return services;
    }

    private static void Configure(HttpClient client, string baseUrl, string clientName)
    {
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("X-Client", clientName);
    }

    private static void RegisterAuthorized<T>(IServiceCollection services, RefitSettings settings, string baseUrl, string clientName) where T : class =>
        services.AddRefitClient<T>(settings)
            .ConfigureHttpClient(c => Configure(c, baseUrl, clientName))
            .AddHttpMessageHandler<BaseAddressHandler>()
            .AddHttpMessageHandler<PageRequestScopeHandler>()
            .AddHttpMessageHandler<AuthTokenHandler>()
            .AddHttpMessageHandler<NoContentHandler>();
}
