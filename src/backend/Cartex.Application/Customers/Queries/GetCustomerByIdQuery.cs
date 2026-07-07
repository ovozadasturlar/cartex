using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Customers.Queries;

public record GetCustomerByIdQuery(long Id) : IRequest<CustomerDto?>;

public sealed class GetCustomerByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCustomerByIdQuery, CustomerDto?>
{
    public async Task<CustomerDto?> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var baseCode = await db.Businesses.Select(b => b.Currency).FirstAsync(cancellationToken);
        return await db.Customers
            .Where(c => c.Id == request.Id)
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
                c.NotificationsOptOut))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
