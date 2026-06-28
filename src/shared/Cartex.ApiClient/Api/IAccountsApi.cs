using Cartex.Shared.Models.Accounts;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IAccountsApi
{
    [Get("/api/accounts")]
    Task<List<AccountDto>> GetAllAsync();
}
