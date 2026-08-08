using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Barcodes.Queries;

public record GetBarcodesByVariantQuery(long VariantId) : IRequest<IReadOnlyCollection<BarcodeDto>>;

public record BarcodeDto(long Id, string Code, decimal PackQty);

public sealed class GetBarcodesByVariantQueryHandler(IApplicationDbContext db) : IRequestHandler<GetBarcodesByVariantQuery, IReadOnlyCollection<BarcodeDto>>
{
    public async Task<IReadOnlyCollection<BarcodeDto>> Handle(GetBarcodesByVariantQuery request, CancellationToken cancellationToken) =>
        await db.Barcodes
            .Where(b => b.VariantId == request.VariantId)
            .Select(b => new BarcodeDto(b.Id, b.Code, b.PackQty))
            .ToListAsync(cancellationToken);
}
