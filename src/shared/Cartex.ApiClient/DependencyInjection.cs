using System.Text.Json;
using System.Text.Json.Serialization;
using Cartex.ApiClient.Api;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace Cartex.ApiClient;

public static class DependencyInjection
{
    public static IServiceCollection AddApiClients(this IServiceCollection services, string baseUrl)
    {
        var settings = new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            })
        };

        services.AddRefitClient<IAuthApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<IShopsApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<IUsersApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<IRolesApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<IPermissionsApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<IProductsApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<ICategoriesApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<IUnitsApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<IBarcodesApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<IWarehousesApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<IStocksApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<IStockTransfersApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<ISalesApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<ISuppliesApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<ICustomersApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<ISuppliersApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<IAccountsApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<ITransactionsApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
        services.AddRefitClient<IAuditLogsApi>(settings).ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));

        return services;
    }
}
