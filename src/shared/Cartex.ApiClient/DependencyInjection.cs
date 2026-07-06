using System.Text.Json;
using System.Text.Json.Serialization;
using Cartex.ApiClient.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Cartex.ApiClient;

public static class DependencyInjection
{
    public static IServiceCollection AddApiClients(this IServiceCollection services, Func<string> baseUrlProvider, Func<string?> tokenProvider, Action? onUnauthorized = null)
    {
        var settings = new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            })
        };

        services.AddTransient(_ => new AuthTokenHandler(tokenProvider, onUnauthorized));
        services.AddTransient<NoContentHandler>();
        services.AddTransient(_ => new BaseAddressHandler(baseUrlProvider));

        var baseUrl = baseUrlProvider();

        services.AddRefitClient<IAuthApi>(settings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler<BaseAddressHandler>()
            .AddHttpMessageHandler<NoContentHandler>();

        services.AddRefitClient<IReceiptApi>(settings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler<BaseAddressHandler>()
            .AddHttpMessageHandler<NoContentHandler>();

        RegisterAuthorized<IBranchesApi>(services, settings, baseUrl);
        RegisterAuthorized<IUsersApi>(services, settings, baseUrl);
        RegisterAuthorized<IRolesApi>(services, settings, baseUrl);
        RegisterAuthorized<IPermissionsApi>(services, settings, baseUrl);
        RegisterAuthorized<IProductsApi>(services, settings, baseUrl);
        RegisterAuthorized<IProductTypesApi>(services, settings, baseUrl);
        RegisterAuthorized<ICategoriesApi>(services, settings, baseUrl);
        RegisterAuthorized<IUnitsApi>(services, settings, baseUrl);
        RegisterAuthorized<IBarcodesApi>(services, settings, baseUrl);
        RegisterAuthorized<IWarehousesApi>(services, settings, baseUrl);
        RegisterAuthorized<IStocksApi>(services, settings, baseUrl);
        RegisterAuthorized<IStockTransfersApi>(services, settings, baseUrl);
        RegisterAuthorized<ISalesApi>(services, settings, baseUrl);
        RegisterAuthorized<ISuppliesApi>(services, settings, baseUrl);
        RegisterAuthorized<ICustomersApi>(services, settings, baseUrl);
        RegisterAuthorized<ISuppliersApi>(services, settings, baseUrl);
        RegisterAuthorized<IAccountsApi>(services, settings, baseUrl);
        RegisterAuthorized<ITransactionsApi>(services, settings, baseUrl);
        RegisterAuthorized<ILoyaltyApi>(services, settings, baseUrl);
        RegisterAuthorized<IAuditLogsApi>(services, settings, baseUrl);
        RegisterAuthorized<IReportsApi>(services, settings, baseUrl);
        RegisterAuthorized<IExpenseCategoriesApi>(services, settings, baseUrl);
        RegisterAuthorized<IOrderingApi>(services, settings, baseUrl);
        RegisterAuthorized<IFeaturesApi>(services, settings, baseUrl);
        RegisterAuthorized<ILicenseApi>(services, settings, baseUrl);
        RegisterAuthorized<ISettingsApi>(services, settings, baseUrl);
        RegisterAuthorized<IStorageApi>(services, settings, baseUrl);
        RegisterAuthorized<IShiftsApi>(services, settings, baseUrl);
        RegisterAuthorized<IHardwareKeysApi>(services, settings, baseUrl);
        RegisterAuthorized<IBusinessApi>(services, settings, baseUrl);
        RegisterAuthorized<IRatesApi>(services, settings, baseUrl);

        return services;
    }

    private static void RegisterAuthorized<T>(IServiceCollection services, RefitSettings settings, string baseUrl) where T : class =>
        services.AddRefitClient<T>(settings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
            .AddHttpMessageHandler<BaseAddressHandler>()
            .AddHttpMessageHandler<AuthTokenHandler>()
            .AddHttpMessageHandler<NoContentHandler>();
}
