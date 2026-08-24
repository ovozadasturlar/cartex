using Cartex.Application.Common.Extensions;
using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Models;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.Customers;

namespace Cartex.Application.Customers.Queries;

public record GetCustomersQuery : FilteringRequest, IRequest<IReadOnlyCollection<CustomerDto>>;

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
            customers = customers.Where(c => c.AssignedUserId == currentUser.UserId);

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
                        c.PreferredLanguage,
                        0m,
                        c.Party.Note,
                        c.AllowMarketingSms),
                    Debts = db.Accounts
                        .Where(a => a.CustomerId == c.Id && a.Type == AccountType.Debt && a.Balance != 0)
                        .Select(a => new CurrencyAmountDto(a.Currency, a.Balance))
                        .ToList(),
                    Credits = db.Accounts
                        .Where(a => a.CustomerId == c.Id && a.Type == AccountType.CustomerAdvance && a.Balance != 0)
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
            DebtBalances = x.Debts,
            CreditBalance = x.Credits.Sum(d => d.Amount * (d.Currency == baseCode ? 1m : rates.GetValueOrDefault(d.Currency))),
            CreditBalances = x.Credits
        }).ToList();
    }
}
