using Cartex.Domain.Common;

namespace Cartex.Api.Services;

public sealed class CurrentCustomer(IHttpContextAccessor accessor) : ICurrentCustomer
{
    public long? CustomerId =>
        long.TryParse(accessor.HttpContext?.User?.FindFirst("customerId")?.Value, out var id) ? id : null;
}
