using Cartex.Domain.Authorization;
using Cartex.Domain.Common;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Customers;

namespace Cartex.Application.Customers.Queries;

public record GetCustomerByCardQuery(string Code) : IRequest<CustomerDto?>;

public sealed class GetCustomerByCardQueryHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<GetCustomerByCardQuery, CustomerDto?>
{
    public async Task<CustomerDto?> Handle(GetCustomerByCardQuery request, CancellationToken cancellationToken)
    {
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        var customers = db.Customers.Where(c => c.CardBarcode == request.Code);
        if (!currentUser.HasPermission(AppPermissions.Customers.ViewAll))
            customers = customers.Where(c => c.AssignedUserId == currentUser.UserId);

        return await customers
            .Select(c => new CustomerDto(
                c.Id,
                c.Party.FullName,
                c.LastName,
                c.Party.Address,
                c.Party.Phone,
                c.Party.Email,
                c.CardBarcode,
                c.DiscountPct,
                c.Accounts.Where(a => a.Type == AccountType.Bonus).Sum(a => a.Balance),
                c.Accounts.Where(a => a.Type == AccountType.Debt).Sum(a => a.Balance * (a.Currency == baseCode ? 1m
                    : db.ExchangeRates.Where(r => r.Code == a.Currency).OrderByDescending(r => r.EffectiveAt).Select(r => r.Rate).FirstOrDefault())),
                c.CreditLimit))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
