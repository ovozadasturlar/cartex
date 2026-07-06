using Cartex.Application.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Common;
using Cartex.Domain.Entities;

namespace Cartex.Application.Barcodes.Commands;

public record CreateBarcodeCommand(long VariantId, string Code, decimal PackQty) : ICommand<long>;

public record GenerateBarcodeCommand(long VariantId) : ICommand<string>;

public sealed class CreateBarcodeCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateBarcodeCommand, long>
{
    public async Task<long> Handle(CreateBarcodeCommand request, CancellationToken cancellationToken)
    {
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

public sealed class GenerateBarcodeCommandHandler(IApplicationDbContext db) : IRequestHandler<GenerateBarcodeCommand, string>
{
    public async Task<string> Handle(GenerateBarcodeCommand request, CancellationToken cancellationToken)
    {
        var existing = await db.Barcodes
            .Where(b => b.VariantId == request.VariantId)
            .Select(b => b.Code)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
            return existing;

        var ctxCodes = await db.Barcodes.IgnoreQueryFilters()
            .Where(b => b.Code.StartsWith("CTX"))
            .Select(b => b.Code)
            .ToListAsync(cancellationToken);
        var next = ctxCodes
            .Select(c => int.TryParse(c.AsSpan(3), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        var code = $"CTX{next:D8}";
        db.Barcodes.Add(new Barcode { VariantId = request.VariantId, Code = code, PackQty = 1 });
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
