using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.Customers;

namespace Cartex.Application.Customers.Queries;

public record GetCustomerByIdQuery(long Id) : IRequest<CustomerDto?>;

public sealed class GetCustomerByIdQueryHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<GetCustomerByIdQuery, CustomerDto?>
{
    public async Task<CustomerDto?> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        var customers = db.Customers.Where(c => c.Id == request.Id);
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll))
            customers = customers.Where(c => c.AssignedUserId == currentUser.UserId);

        return await customers
            .Select(c => new CustomerDto(
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
                c.NotificationsOptOut,
                c.TelegramChatId != null,
                c.PreferredLanguage,
                c.Accounts.Where(a => a.Type == AccountType.CustomerAdvance).Sum(a => a.Balance * (a.Currency == baseCode ? 1m
                    : db.ExchangeRates.Where(r => r.Code == a.Currency).OrderByDescending(r => r.EffectiveAt).Select(r => r.Rate).FirstOrDefault())),
                c.Party.Note,
                c.AllowMarketingSms)
            {
                DebtBalances = c.Accounts
                    .Where(a => a.Type == AccountType.Debt && a.Balance != 0)
                    .Select(a => new CurrencyAmountDto(a.Currency, a.Balance))
                    .ToList(),
                CreditBalances = c.Accounts
                    .Where(a => a.Type == AccountType.CustomerAdvance && a.Balance != 0)
                    .Select(a => new CurrencyAmountDto(a.Currency, a.Balance))
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
