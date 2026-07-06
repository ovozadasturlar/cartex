using Cartex.Application.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Entities;
using Cartex.Application.Common.Catalog;

namespace Cartex.Application.Products.Commands;

public record CreateVariantCommand(long ProductId, string? Name, string? Code, string? Attributes, string? ImageKey, List<string>? Barcodes) : ICommand<long>;

public sealed class CreateVariantCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateVariantCommand, long>
{
    public async Task<long> Handle(CreateVariantCommand request, CancellationToken cancellationToken)
    {
        var product = await db.Products
            .Where(p => p.Id == request.ProductId)
            .Select(p => new { p.Id, p.ProductTypeId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Product not found.");

        if (product.ProductTypeId is { } typeId)
        {
            var schema = await db.ProductTypes.Where(t => t.Id == typeId).Select(t => t.AttributeSchema).FirstOrDefaultAsync(cancellationToken);
            AttributeSchema.Validate(schema, request.Attributes);
        }

        var variant = new ProductVariant
        {
            ProductId = product.Id,
            Name = request.Name,
            Code = request.Code,
            Attributes = request.Attributes,
            ImageKey = request.ImageKey,
            IsDefault = false
        };

        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync(cancellationToken);

        var codes = request.Barcodes?.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct().ToList() ?? [];
        if (codes.Count > 0)
        {
            var existing = await db.Barcodes.Where(b => codes.Contains(b.Code)).Select(b => b.Code).FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
                throw new BusinessRuleException($"Bu barkod allaqachon mavjud: {existing}");

            foreach (var code in codes)
                db.Barcodes.Add(new Barcode { VariantId = variant.Id, Code = code, PackQty = 1 });

            await db.SaveChangesAsync(cancellationToken);
        }

        return variant.Id;
    }
}

public sealed class CreateVariantCommandValidator : AbstractValidator<CreateVariantCommand>
{
    public CreateVariantCommandValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
    }
}
