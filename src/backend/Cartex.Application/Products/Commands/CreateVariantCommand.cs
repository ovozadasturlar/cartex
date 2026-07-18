using Cartex.Application.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Entities;
using Cartex.Application.Common.Catalog;

namespace Cartex.Application.Products.Commands;

public record CreateVariantCommand(long ProductId, string? Name, string? Code, string? Attributes, string? ImageKey, List<BarcodeInput>? Barcodes) : ICommand<long>;

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

        if (!string.IsNullOrWhiteSpace(request.Code) && await db.ProductVariants.AnyAsync(v => v.Code == request.Code, cancellationToken))
            throw new BusinessRuleException($"Bu kod allaqachon mavjud: {request.Code}");

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

        var inputs = request.Barcodes?.Where(b => !string.IsNullOrWhiteSpace(b.Code))
            .Select(b => b with { Code = b.Code.Trim() }).DistinctBy(b => b.Code).ToList() ?? [];
        if (inputs.Count > 0)
        {
            var codes = inputs.Select(b => b.Code).ToList();
            var existing = await db.Barcodes.Where(b => codes.Contains(b.Code)).Select(b => b.Code).FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
                throw new BusinessRuleException($"Bu barkod allaqachon mavjud: {existing}");

            foreach (var input in inputs)
            {
                var packQty = input.PackQty > 0 ? input.PackQty : 1;
                Barcodes.GeneratedPackCodes.EnsureConsistent(input.Code, packQty);
                db.Barcodes.Add(new Barcode { VariantId = variant.Id, Code = input.Code, PackQty = packQty });
            }

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
