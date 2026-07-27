using Cartex.Application.Common.Finance;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Application.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Entities;
using Cartex.Application.Common.Catalog;
using Cartex.Application.Barcodes;
using Cartex.Application.Barcodes.Commands;
using Microsoft.Extensions.Configuration;

namespace Cartex.Application.Products.Commands;

public record CreateProductCommand(
    string Name,
    long? CategoryId,
    long UnitId,
    decimal? MinStock,
    List<BarcodeInput>? Barcodes,
    long? ProductTypeId = null,
    bool? TracksExpiryOverride = null,
    string? Attributes = null,
    string? ImageKey = null,
    string? Code = null,
    string? IkpuCode = null,
    decimal? VatRate = null,
    decimal? SellingPrice = null,
    string? PriceCurrency = null,
    long? ManufacturerId = null) : ICommand<long>;

public sealed class CreateProductCommandHandler(IApplicationDbContext db, ICurrencyService currency, ISettingsService settingsService, IConfiguration configuration) : IRequestHandler<CreateProductCommand, long>
{
    public async Task<long> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        if (request.ProductTypeId is { } typeId)
        {
            var schema = await db.ProductTypes.Where(t => t.Id == typeId).Select(t => t.AttributeSchema).FirstOrDefaultAsync(cancellationToken);
            AttributeSchema.Validate(schema, request.Attributes);
        }

        if (!string.IsNullOrWhiteSpace(request.Code) && await db.ProductVariants.AnyAsync(v => v.Code == request.Code, cancellationToken))
            throw new BusinessRuleException($"Bu kod allaqachon mavjud: {request.Code}");

        var product = new Product
        {
            Name = request.Name,
            CategoryId = request.CategoryId,
            UnitId = request.UnitId,
            MinStock = request.MinStock ?? (await settingsService.GetAsync<SalesPolicySettings>(SettingKeys.SalesPolicy, cancellationToken))?.DefaultMinStock ?? 0,
            ProductTypeId = request.ProductTypeId,
            ManufacturerId = request.ManufacturerId,
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
        }
        else
            db.Barcodes.Add(new Barcode { VariantId = variant.Id, Code = GeneratedBarcodeCode.Build(configuration, variant.Id), PackQty = 1 });

        await db.SaveChangesAsync(cancellationToken);

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
        RuleFor(x => x.MinStock).GreaterThanOrEqualTo(0).When(x => x.MinStock.HasValue);
    }
}
