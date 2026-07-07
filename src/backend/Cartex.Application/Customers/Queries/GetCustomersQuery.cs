using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers.Queries;

public record GetCustomersQuery : FilteringRequest, IRequest<IReadOnlyCollection<CustomerDto>>;

public record CustomerDto(long Id, string FullName, string? LastName, string? Address, string? Phone, string? Email, string? CardBarcode, decimal DiscountPct, decimal CashbackBalance, decimal DebtBalance, decimal CreditLimit, bool NotificationsOptOut = false)
{
    public IReadOnlyList<CurrencyAmountDto> DebtBalances { get; init; } = [];
}

public sealed class GetCustomersQueryHandler(
    IApplicationDbContext db,
    IPagingMetadataWriter writer) : IRequestHandler<GetCustomersQuery, IReadOnlyCollection<CustomerDto>>
{
    public async Task<IReadOnlyCollection<CustomerDto>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        var items = await db.Customers
            .ToPagedListAsync(request,
                c => new CustomerDto(
                    c.Id,
                    c.FullName,
                    c.LastName,
                    c.Address,
                    c.Phone,
                    c.Email,
                    c.CardBarcode,
                    c.DiscountPct,
                    c.Accounts.Where(a => a.Type == AccountType.Bonus).Sum(a => a.Balance),
                    c.Accounts.Where(a => a.Type == AccountType.Debt).Sum(a => a.Balance * (a.Currency == baseCode ? 1m
                        : db.ExchangeRates.Where(r => r.Code == a.Currency).OrderByDescending(r => r.EffectiveAt).Select(r => r.Rate).FirstOrDefault())),
                    c.CreditLimit,
                    c.NotificationsOptOut),
                writer, cancellationToken);

        var ids = items.Select(i => i.Id).ToList();
        var balances = await db.Accounts
            .Where(a => a.CustomerId != null && ids.Contains(a.CustomerId.Value) && a.Type == AccountType.Debt && a.Balance != 0)
            .Select(a => new { a.CustomerId, a.Currency, a.Balance })
            .ToListAsync(cancellationToken);

        return items.Select(i => i with
        {
            DebtBalances = balances.Where(b => b.CustomerId == i.Id)
                .Select(b => new CurrencyAmountDto(b.Currency, b.Balance)).ToList()
        }).ToList();
    }
}
