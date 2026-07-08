using Cartex.Application.Common.Messaging;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Store.Queries;

public record StoreReceiptDto(string ReceiptToken, DateTime CreatedAt, decimal TotalAmount);

public record GetStoreReceiptsQuery : IRequest<IReadOnlyList<StoreReceiptDto>>;

public sealed class GetStoreReceiptsQueryHandler(IApplicationDbContext db, ICurrentCustomer currentCustomer)
    : IRequestHandler<GetStoreReceiptsQuery, IReadOnlyList<StoreReceiptDto>>
{
    public async Task<IReadOnlyList<StoreReceiptDto>> Handle(GetStoreReceiptsQuery request, CancellationToken cancellationToken)
    {
        var customerId = currentCustomer.CustomerId ?? throw new UnauthorizedAccessException("Not authenticated.");
        return await db.Sales
            .Where(s => s.CustomerId == customerId)
            .OrderByDescending(s => s.Id)
            .Take(100)
            .Select(s => new StoreReceiptDto(s.ReceiptToken, s.CreatedAt, s.TotalAmount))
            .ToListAsync(cancellationToken);
    }
}
