using Cartex.Application.Common.Models;
using Cartex.Application.Stocks.Queries;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Agents.Queries;

public record AgentCustomerDto(long Id, string FullName, string? Phone, string? Address, decimal DebtBalance, decimal CreditLimit, List<CurrencyAmountDto> DebtBalances, double? Latitude, double? Longitude);

public record AgentBootstrapDto(long? WarehouseId, string? WarehouseName, string BaseCurrency, DateTime ServerTime, List<AgentCustomerDto> Customers, IReadOnlyCollection<StockOnHandDto> VanStock);

public record GetAgentBootstrapQuery : IRequest<AgentBootstrapDto>;

public sealed class GetAgentBootstrapQueryHandler(IApplicationDbContext db, ICurrentUser currentUser, ISender sender)
    : IRequestHandler<GetAgentBootstrapQuery, AgentBootstrapDto>
{
    public async Task<AgentBootstrapDto> Handle(GetAgentBootstrapQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Not authenticated.");
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);

        var warehouse = await db.Warehouses
            .Where(w => w.AssignedUserId == userId)
            .Select(w => new { w.Id, w.Name })
            .FirstOrDefaultAsync(cancellationToken);

        var customers = await db.Customers
            .Where(c => c.AssignedUserId == userId)
            .OrderBy(c => c.FullName)
            .Select(c => new AgentCustomerDto(
                c.Id,
                c.FullName,
                c.Phone,
                c.Address,
                c.Accounts.Where(a => a.Type == AccountType.Debt).Sum(a => a.Balance * (a.Currency == baseCode ? 1m
                    : db.ExchangeRates.Where(r => r.Code == a.Currency).OrderByDescending(r => r.EffectiveAt).Select(r => r.Rate).FirstOrDefault())),
                c.CreditLimit,
                c.Accounts.Where(a => a.Type == AccountType.Debt && a.Balance != 0)
                    .Select(a => new CurrencyAmountDto(a.Currency, a.Balance)).ToList(),
                c.Latitude,
                c.Longitude))
            .ToListAsync(cancellationToken);

        var vanStock = warehouse is null
            ? []
            : (await sender.Send(new GetStockOnHandQuery(warehouse.Id, PageSize: 1000), cancellationToken)).Items;

        return new AgentBootstrapDto(warehouse?.Id, warehouse?.Name, baseCode, DateTime.UtcNow, customers, vanStock);
    }
}
