using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Application.Common.Catalog;

namespace Cartex.Application.Products.Commands;

public record UpdateProductCommand(
    long Id,
    string Name,
    long? CategoryId,
    long UnitId,
    decimal MinStock,
    long? ProductTypeId = null,
    bool? TracksExpiryOverride = null,
    string? Attributes = null,
    string? ImageKey = null,
    string? Code = null,
    string? IkpuCode = null,
    decimal? VatRate = null,
    decimal? SellingPrice = null,
    string? PriceCurrency = null,
    long? ManufacturerId = null) : ICommand<Unit>;

public sealed class UpdateProductCommandHandler(IApplicationDbContext db, ICurrencyService currency, IObjectStorage storage) : IRequestHandler<UpdateProductCommand, Unit>
{
    public async Task<Unit> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await db.Products.Include(p => p.Unit).FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Product not found.");

        if (request.UnitId != product.UnitId)
        {
            var newUnit = await db.Units.FirstOrDefaultAsync(u => u.Id == request.UnitId, cancellationToken)
                ?? throw new NotFoundException("Unit not found.");
            if (newUnit.Dimension != product.Unit.Dimension)
                throw new BusinessRuleException("O'lchov birligini boshqa guruhga o'zgartirib bo'lmaydi.");
            if (newUnit.Factor != product.Unit.Factor
                && await db.Stocks.IgnoreQueryFilters().AnyAsync(s => s.Variant.ProductId == product.Id, cancellationToken))
                throw new BusinessRuleException("Mahsulotning ombor harakatlari bor — o'lchov birligini o'zgartirib bo'lmaydi.");
        }

        if (request.ProductTypeId is { } typeId)
        {
            var schema = await db.ProductTypes.Where(t => t.Id == typeId).Select(t => t.AttributeSchema).FirstOrDefaultAsync(cancellationToken);
            AttributeSchema.Validate(schema, request.Attributes);
        }

        var oldImageKey = product.ImageKey;
        product.Name = request.Name;
        product.CategoryId = request.CategoryId;
        product.UnitId = request.UnitId;
        product.MinStock = request.MinStock;
        product.ProductTypeId = request.ProductTypeId;
        product.ManufacturerId = request.ManufacturerId;
        product.TracksExpiryOverride = request.TracksExpiryOverride;
        product.Attributes = request.Attributes;
        product.ImageKey = request.ImageKey;
        product.IkpuCode = request.IkpuCode;
        product.VatRate = request.VatRate;

        var defaultVariant = await db.ProductVariants.FirstOrDefaultAsync(v => v.ProductId == product.Id && v.IsDefault, cancellationToken);
        if (defaultVariant is not null)
        {
            if (!string.IsNullOrWhiteSpace(request.Code) && await db.ProductVariants.AnyAsync(v => v.Code == request.Code && v.Id != defaultVariant.Id, cancellationToken))
                throw new BusinessRuleException($"Bu kod allaqachon mavjud: {request.Code}");
            defaultVariant.Code = request.Code;
            if (request.SellingPrice is { } sellingPrice)
            {
                await currency.EnsureAllowedAsync(request.PriceCurrency, cancellationToken);
                await ProductPriceWriter.UpsertAsync(db, defaultVariant.Id, null, sellingPrice, cancellationToken, request.PriceCurrency);
            }
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
