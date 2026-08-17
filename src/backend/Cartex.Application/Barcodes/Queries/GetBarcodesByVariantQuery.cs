using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Barcodes;

namespace Cartex.Application.Barcodes.Queries;

public record GetBarcodesByVariantQuery(long VariantId) : IRequest<IReadOnlyCollection<BarcodeDto>>;

public sealed class GetBarcodesByVariantQueryHandler(IApplicationDbContext db) : IRequestHandler<GetBarcodesByVariantQuery, IReadOnlyCollection<BarcodeDto>>
{
    public async Task<IReadOnlyCollection<BarcodeDto>> Handle(GetBarcodesByVariantQuery request, CancellationToken cancellationToken) =>
        await db.Barcodes
            .Where(b => b.VariantId == request.VariantId)
            .Select(b => new BarcodeDto(b.Id, b.Code, b.PackQty))
            .ToListAsync(cancellationToken);
}
