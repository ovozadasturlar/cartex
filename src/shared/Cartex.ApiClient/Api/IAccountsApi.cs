using Cartex.Shared.Models.Accounts;
using Refit;

namespace Cartex.ApiClient.Api;

public interface IAccountsApi
{
    [Get("/api/accounts")]
    Task<List<AccountDto>> GetAllAsync([Query] string? ownerType = null, [Query] long? ownerId = null);
}
