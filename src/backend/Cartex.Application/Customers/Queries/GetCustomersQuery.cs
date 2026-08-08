using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
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
    ICurrencyService currency,
    IPagingMetadataWriter writer) : IRequestHandler<GetCustomersQuery, IReadOnlyCollection<CustomerDto>>
{
    public async Task<IReadOnlyCollection<CustomerDto>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        var baseCode = await currency.BaseAsync(cancellationToken);
        var customers = db.Customers.AsSingleQuery();
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll))
            customers = customers.Where(c => c.AgentId == currentUser.UserId);

        var items = await customers
            .ToPagedListAsync(request,
                c => new
                {
                    Dto = new CustomerDto(
                        c.Id,
                        c.FullName,
                        c.LastName,
                        c.Address,
                        c.Phone,
                        c.Email,
                        c.CardBarcode,
                        c.DiscountPct,
                        db.Accounts
                            .Where(a => a.CustomerId == c.Id && a.Type == AccountType.Bonus && a.Balance != 0)
                            .Sum(a => a.Balance),
                        0m,
                        c.CreditLimit,
                        c.NotificationsOptOut,
                        c.TelegramChatId != null,
                        c.PreferredLanguage),
                    Debts = db.Accounts
                        .Where(a => a.CustomerId == c.Id && a.Type == AccountType.Debt && a.Balance != 0)
                        .Select(a => new CurrencyAmountDto(a.Currency, a.Balance))
                        .ToList()
                },
                writer, cancellationToken);

        var rates = (await db.ExchangeRates
            .GroupBy(r => r.Code)
            .Select(g => g.OrderByDescending(r => r.EffectiveAt).First())
            .ToListAsync(cancellationToken))
            .ToDictionary(r => r.Code, r => r.Rate);

        return items.Select(x => x.Dto with
        {
            DebtBalance = x.Debts.Sum(d => d.Amount * (d.Currency == baseCode ? 1m : rates.GetValueOrDefault(d.Currency))),
            DebtBalances = x.Debts
        }).ToList();
    }
}
