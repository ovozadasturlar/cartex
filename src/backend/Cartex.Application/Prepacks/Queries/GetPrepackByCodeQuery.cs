using Cartex.Application.Common.Messaging;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Prepacks.Queries;

public record PrepackLookupDto(long PrepackId, long VariantId, string ProductName, string UnitName, string Dimension, decimal Quantity, decimal UnitPrice, decimal Price);

public record GetPrepackByCodeQuery(string Code, long WarehouseId) : IRequest<PrepackLookupDto?>;

public sealed class GetPrepackByCodeQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPrepackByCodeQuery, PrepackLookupDto?>
{
    public async Task<PrepackLookupDto?> Handle(GetPrepackByCodeQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var prepack = await db.Prepacks
            .Where(p => p.LabelCode == request.Code && p.WarehouseId == request.WarehouseId)
            .Select(p => new { p.Id, p.VariantId, p.Variant.Product.Name, Unit = p.Variant.Product.Unit, p.Quantity, p.UnitPrice, p.Status, p.ExpiresAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (prepack is null)
            return null;

        if (prepack.Status != PrepackStatus.Active)
            throw new BusinessRuleException("Bu qadoq allaqachon sotilgan.");
        if (prepack.ExpiresAt is { } expires && expires <= now)
            throw new BusinessRuleException("Qadoq muddati o'tgan. Qaytadan qadoqlang.");

        return new PrepackLookupDto(prepack.Id, prepack.VariantId, prepack.Name, prepack.Unit.Name, prepack.Unit.Dimension.ToString(),
            prepack.Quantity, prepack.UnitPrice, Math.Round(prepack.UnitPrice * prepack.Quantity, 2));
    }
}
