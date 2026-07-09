using Cartex.Shared.Models.Accounts;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IAccountsApi
{
    [Get("/api/accounts")]
    Task<List<AccountDto>> GetAllAsync();

    [Get("/api/accounts")]
    Task<IApiResponse<List<AccountDto>>> QueryAsync([Query] IDictionary<string, object> query);

    [Get("/api/accounts/totals")]
    Task<AccountsTotalsDto> GetTotalsAsync([Query] string? search = null);
}
