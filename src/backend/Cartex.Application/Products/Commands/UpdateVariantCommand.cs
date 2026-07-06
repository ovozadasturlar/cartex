using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Entities;
using Cartex.Application.Common.Catalog;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Products.Commands;

public record UpdateVariantCommand(long Id, string? Name, string? Code, string? Attributes, string? ImageKey, List<string>? Barcodes) : ICommand<Unit>;

public sealed class UpdateVariantCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateVariantCommand, Unit>
{
    public async Task<Unit> Handle(UpdateVariantCommand request, CancellationToken cancellationToken)
    {
        var variant = await db.ProductVariants
            .Include(v => v.Barcodes)
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Variant not found.");

        if (variant.Product.ProductTypeId is { } typeId)
        {
            var schema = await db.ProductTypes.Where(t => t.Id == typeId).Select(t => t.AttributeSchema).FirstOrDefaultAsync(cancellationToken);
            AttributeSchema.Validate(schema, request.Attributes);
        }

        variant.Name = request.Name;
        variant.Code = request.Code;
        variant.Attributes = request.Attributes;
        variant.ImageKey = request.ImageKey;

        var desired = request.Barcodes ?? [];
        foreach (var existing in variant.Barcodes.Where(b => !desired.Contains(b.Code)).ToList())
            db.Barcodes.Remove(existing);

        var current = variant.Barcodes.Select(b => b.Code).ToHashSet();
        foreach (var code in desired.Where(c => !current.Contains(c)))
            db.Barcodes.Add(new Barcode { VariantId = variant.Id, Code = code, PackQty = 1 });

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
