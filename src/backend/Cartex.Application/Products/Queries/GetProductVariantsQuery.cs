using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Queries;

public record GetProductVariantsQuery(long ProductId) : IRequest<IReadOnlyCollection<VariantDto>>;

public record VariantBarcodeDto(string Code, decimal PackQty);

public record VariantDto(long Id, long ProductId, string? Name, string? Code, string? Attributes, string? ImageKey, bool IsDefault, List<VariantBarcodeDto> Barcodes);

public sealed class GetProductVariantsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetProductVariantsQuery, IReadOnlyCollection<VariantDto>>
{
    public async Task<IReadOnlyCollection<VariantDto>> Handle(GetProductVariantsQuery request, CancellationToken cancellationToken)
    {
        return await db.ProductVariants
            .Where(v => v.ProductId == request.ProductId)
            .OrderByDescending(v => v.IsDefault)
            .ThenBy(v => v.Id)
            .Select(v => new VariantDto(
                v.Id, v.ProductId, v.Name, v.Code, v.Attributes, v.ImageKey, v.IsDefault,
                v.Barcodes.Select(b => new VariantBarcodeDto(b.Code, b.PackQty)).ToList()))
            .ToListAsync(cancellationToken);
    }
}
