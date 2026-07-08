using System.Globalization;
using Cartex.Application.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Cartex.Persistence;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;

namespace Cartex.Application.Barcodes.Commands;

public record CreateBarcodeCommand(long VariantId, string Code, decimal PackQty) : ICommand<long>;

public record GenerateBarcodeCommand(long VariantId, decimal PackQty = 1) : ICommand<string>;

public sealed class CreateBarcodeCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateBarcodeCommand, long>
{
    public async Task<long> Handle(CreateBarcodeCommand request, CancellationToken cancellationToken)
    {
        GeneratedPackCodes.EnsureConsistent(request.Code, request.PackQty);
        if (await db.Barcodes.AnyAsync(b => b.Code == request.Code, cancellationToken))
            throw new BusinessRuleException("Bu barkod allaqachon mavjud.");

        var barcode = new Barcode
        {
            VariantId = request.VariantId,
            Code = request.Code,
            PackQty = request.PackQty
        };

        db.Barcodes.Add(barcode);
        await db.SaveChangesAsync(cancellationToken);

        return barcode.Id;
    }
}

public sealed class GenerateBarcodeCommandHandler(IApplicationDbContext db, IConfiguration configuration) : IRequestHandler<GenerateBarcodeCommand, string>
{
    public async Task<string> Handle(GenerateBarcodeCommand request, CancellationToken cancellationToken)
    {
        if (!await db.ProductVariants.AnyAsync(v => v.Id == request.VariantId, cancellationToken))
            throw new NotFoundException("Variant not found.");

        var prefix = configuration["Barcode:Prefix"]?.Trim().TrimEnd('-').ToUpperInvariant();
        if (string.IsNullOrEmpty(prefix)) prefix = "CTX";
        if (GeneratedPackCodes.EmbeddedQty($"{prefix}-") is not null)
            throw new BusinessRuleException("Barcode:Prefix ichida -P<son>- bo'lagi bo'lishi mumkin emas.");

        var qty = request.PackQty > 1 ? request.PackQty : 1m;
        var serial = request.VariantId.ToString("D6");
        var code = qty > 1
            ? $"{prefix}-P{qty.ToString("0.###", CultureInfo.InvariantCulture)}-{serial}"
            : $"{prefix}-{serial}";

        var existing = await db.Barcodes.FirstOrDefaultAsync(b => b.Code == code, cancellationToken);
        if (existing is not null)
        {
            if (existing.VariantId != request.VariantId)
                throw new BusinessRuleException($"Bu kod boshqa mahsulotga tegishli: {code}");
            return code;
        }

        db.Barcodes.Add(new Barcode { VariantId = request.VariantId, Code = code, PackQty = qty });
        await db.SaveChangesAsync(cancellationToken);
        return code;
    }
}

public sealed class CreateBarcodeCommandValidator : AbstractValidator<CreateBarcodeCommand>
{
    public CreateBarcodeCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(60);
    }
}
