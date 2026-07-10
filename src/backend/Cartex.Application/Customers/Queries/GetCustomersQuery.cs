using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Cartex.Domain.Common;
using Cartex.Domain.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers.Queries;

public record GetCustomersQuery : FilteringRequest, IRequest<IReadOnlyCollection<CustomerDto>>;

public record CustomerDto(long Id, string FullName, string? LastName, string? Address, string? Phone, string? Email, string? CardBarcode, decimal DiscountPct, decimal CashbackBalance, decimal DebtBalance, decimal CreditLimit, bool NotificationsOptOut = false, bool HasTelegram = false, string? PreferredLanguage = null)
{
    public IReadOnlyList<CurrencyAmountDto> DebtBalances { get; init; } = [];
}

public sealed class GetCustomersQueryHandler(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IPagingMetadataWriter writer) : IRequestHandler<GetCustomersQuery, IReadOnlyCollection<CustomerDto>>
{
    public async Task<IReadOnlyCollection<CustomerDto>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        var customers = db.Customers.AsQueryable();
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll))
            customers = customers.Where(c => c.AgentId == currentUser.UserId);

        var items = await customers
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
                    0m,
                    0m,
                    c.CreditLimit,
                    c.NotificationsOptOut,
                    c.TelegramChatId != null,
                    c.PreferredLanguage),
                writer, cancellationToken);

        var ids = items.Select(i => i.Id).ToList();
        var balances = await db.Accounts
            .Where(a => a.CustomerId != null && ids.Contains(a.CustomerId.Value)
                && (a.Type == AccountType.Debt || a.Type == AccountType.Bonus) && a.Balance != 0)
            .Select(a => new { a.CustomerId, a.Type, a.Currency, a.Balance })
            .ToListAsync(cancellationToken);

        var rates = (await db.ExchangeRates
            .GroupBy(r => r.Code)
            .Select(g => g.OrderByDescending(r => r.EffectiveAt).First())
            .ToListAsync(cancellationToken))
            .ToDictionary(r => r.Code, r => r.Rate);

        return items.Select(i =>
        {
            var mine = balances.Where(b => b.CustomerId == i.Id).ToList();
            return i with
            {
                CashbackBalance = mine.Where(b => b.Type == AccountType.Bonus).Sum(b => b.Balance),
                DebtBalance = mine.Where(b => b.Type == AccountType.Debt)
                    .Sum(b => b.Balance * (b.Currency == baseCode ? 1m : rates.GetValueOrDefault(b.Currency))),
                DebtBalances = mine.Where(b => b.Type == AccountType.Debt)
                    .Select(b => new CurrencyAmountDto(b.Currency, b.Balance)).ToList()
            };
        }).ToList();
    }
}
