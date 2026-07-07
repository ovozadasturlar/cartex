using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Entities;
using Cartex.Application.Common.Catalog;

namespace Cartex.Application.Products.Commands;

public record CreateProductCommand(
    string Name,
    long? CategoryId,
    long UnitId,
    decimal MinStock,
    List<string>? Barcodes,
    long? ProductTypeId = null,
    bool? TracksExpiryOverride = null,
    string? Attributes = null,
    string? ImageKey = null,
    string? Code = null,
    string? IkpuCode = null,
    decimal? VatRate = null,
    decimal? SellingPrice = null,
    string? PriceCurrency = null) : ICommand<long>;

public sealed class CreateProductCommandHandler(IApplicationDbContext db, ICurrencyService currency) : IRequestHandler<CreateProductCommand, long>
{
    public async Task<long> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        if (request.ProductTypeId is { } typeId)
        {
            var schema = await db.ProductTypes.Where(t => t.Id == typeId).Select(t => t.AttributeSchema).FirstOrDefaultAsync(cancellationToken);
            AttributeSchema.Validate(schema, request.Attributes);
        }

        var product = new Product
        {
            Name = request.Name,
            CategoryId = request.CategoryId,
            UnitId = request.UnitId,
            MinStock = request.MinStock,
            ProductTypeId = request.ProductTypeId,
            TracksExpiryOverride = request.TracksExpiryOverride,
            Attributes = request.Attributes,
            ImageKey = request.ImageKey,
            IkpuCode = request.IkpuCode,
            VatRate = request.VatRate
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        var variant = new ProductVariant { ProductId = product.Id, IsDefault = true, Code = request.Code };
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

        if (request.SellingPrice is { } sellingPrice)
        {
            await currency.EnsureAllowedAsync(request.PriceCurrency, cancellationToken);
            await ProductPriceWriter.UpsertAsync(db, variant.Id, null, sellingPrice, cancellationToken, request.PriceCurrency);
            await db.SaveChangesAsync(cancellationToken);
        }

        return product.Id;
    }
}

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}
