using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Entities;
using Cartex.Application.Common.Catalog;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Products.Commands;

public record UpdateVariantCommand(long Id, string? Name, string? Code, string? Attributes, string? ImageKey, List<BarcodeInput>? Barcodes) : ICommand<Unit>;

public sealed class UpdateVariantCommandHandler(IApplicationDbContext db, IObjectStorage storage) : IRequestHandler<UpdateVariantCommand, Unit>
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

        if (!string.IsNullOrWhiteSpace(request.Code) && await db.ProductVariants.AnyAsync(v => v.Code == request.Code && v.Id != variant.Id, cancellationToken))
            throw new BusinessRuleException($"Bu kod allaqachon mavjud: {request.Code}");

        var oldImageKey = variant.ImageKey;
        variant.Name = request.Name;
        variant.Code = request.Code;
        variant.Attributes = request.Attributes;
        variant.ImageKey = request.ImageKey;

        var desired = (request.Barcodes ?? [])
            .Where(b => !string.IsNullOrWhiteSpace(b.Code))
            .Select(b => b with { Code = b.Code.Trim(), PackQty = b.PackQty > 0 ? b.PackQty : 1 })
            .DistinctBy(b => b.Code)
            .ToDictionary(b => b.Code);

        foreach (var existing in variant.Barcodes.Where(b => !desired.ContainsKey(b.Code)).ToList())
            db.Barcodes.Remove(existing);

        foreach (var existing in variant.Barcodes.Where(b => desired.ContainsKey(b.Code)))
        {
            var next = desired[existing.Code].PackQty;
            if (next != existing.PackQty && Barcodes.GeneratedPackCodes.EmbeddedQty(existing.Code) is { } embedded && embedded != next)
                throw new BusinessRuleException($"{existing.Code} kodida qadoq soni yozilgan — sonni o'zgartirish o'rniga yangi kod generatsiya qiling.");
            existing.PackQty = next;
        }

        var current = variant.Barcodes.Select(b => b.Code).ToHashSet();
        foreach (var input in desired.Values.Where(b => !current.Contains(b.Code)))
        {
            Barcodes.GeneratedPackCodes.EnsureConsistent(input.Code, input.PackQty);
            db.Barcodes.Add(new Barcode { VariantId = variant.Id, Code = input.Code, PackQty = input.PackQty });
        }

        await db.SaveChangesAsync(cancellationToken);

        if (oldImageKey is not null && oldImageKey != request.ImageKey)
        {
            try
            {
                await storage.DeleteAsync(oldImageKey, cancellationToken);
                await storage.DeleteAsync($"t_{oldImageKey}", cancellationToken);
            }
            catch { }
        }

        return Unit.Value;
    }
}
