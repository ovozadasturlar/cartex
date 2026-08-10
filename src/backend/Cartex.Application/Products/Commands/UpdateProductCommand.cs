using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Cartex.Persistence;
using Cartex.Application.Common.Catalog;
using Cartex.Application.Common.Measurement;
using FluentValidation;

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
    long? ManufacturerId = null,
    bool? AmountEntryEnabled = null,
    bool ConfirmUnitDimensionChange = false,
    bool? FractionalOverride = null) : ICommand<Unit>;

public sealed class UpdateProductCommandHandler(
    IApplicationDbContext db,
    ICurrencyService currency,
    IObjectStorage storage,
    ILogger<UpdateProductCommandHandler> logger) : IRequestHandler<UpdateProductCommand, Unit>
{
    public async Task<Unit> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await db.Products.Include(p => p.Unit).FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Product not found.");

        if (request.UnitId != product.UnitId)
        {
            var newUnit = await db.Units.FirstOrDefaultAsync(u => u.Id == request.UnitId, cancellationToken)
                ?? throw new NotFoundException("Unit not found.");
            if (newUnit.Dimension != product.Unit.Dimension && !request.ConfirmUnitDimensionChange)
                throw new BusinessRuleException("O'lchov birligi guruhini o'zgartirish alohida tasdiqlanishi kerak.");
            if (newUnit.Dimension == product.Unit.Dimension && newUnit.Factor != product.Unit.Factor
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
        product.AmountEntryEnabled = request.AmountEntryEnabled;
        product.FractionalOverride = request.FractionalOverride;

        var defaultVariant = await db.ProductVariants.FirstOrDefaultAsync(v => v.ProductId == product.Id && v.IsDefault, cancellationToken);
        if (defaultVariant is not null)
        {
            if (!string.IsNullOrWhiteSpace(request.Code) && await db.ProductVariants.AnyAsync(v => v.Code == request.Code && v.Id != defaultVariant.Id, cancellationToken))
                throw new BusinessRuleException($"Bu kod allaqachon mavjud: {request.Code}");
            defaultVariant.Code = request.Code;
            if (request.SellingPrice is { } sellingPrice)
            {
                await currency.EnsurePricingAllowedAsync(request.PriceCurrency, cancellationToken);
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
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Could not delete replaced product image {ImageKey}", oldImageKey);
            }
        }

        return Unit.Value;
    }
}

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.MinStock).GreaterThanOrEqualTo(0);
    }
}
