using Cartex.Application.Common.Messaging;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.ProductPacks.Commands;

public record CreateProductPackCommand(long ProductId, string Name, decimal Size, string Kind, bool IsDefault) : ICommand<long>;

public record UpdateProductPackCommand(long Id, string Name, decimal Size, string Kind, bool IsDefault) : ICommand<Unit>;

public record DeleteProductPackCommand(long Id) : ICommand<Unit>;

public sealed class CreateProductPackCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateProductPackCommand, long>
{
    public async Task<long> Handle(CreateProductPackCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Products.AnyAsync(p => p.Id == request.ProductId, cancellationToken))
            throw new NotFoundException("Mahsulot topilmadi.");

        var pack = new ProductPack
        {
            ProductId = request.ProductId,
            Name = request.Name.Trim(),
            Size = request.Size,
            Kind = ParsePackKind(request.Kind),
            IsDefault = request.IsDefault
        };

        db.ProductPacks.Add(pack);
        if (pack.IsDefault) await ClearOtherDefaultsAsync(db, request.ProductId, pack.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return pack.Id;
    }

    internal static PackKind ParsePackKind(string kind) =>
        Enum.TryParse<PackKind>(kind, true, out var parsed) ? parsed : PackKind.Purchase;

    internal static async Task ClearOtherDefaultsAsync(IApplicationDbContext db, long productId, long packId, CancellationToken token)
    {
        var others = await db.ProductPacks
            .Where(p => p.ProductId == productId && p.Id != packId && p.IsDefault)
            .ToListAsync(token);
        foreach (var other in others) other.IsDefault = false;
    }
}

public sealed class UpdateProductPackCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateProductPackCommand, Unit>
{
    public async Task<Unit> Handle(UpdateProductPackCommand request, CancellationToken cancellationToken)
    {
        var pack = await db.ProductPacks.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Qadoq topilmadi.");

        pack.Name = request.Name.Trim();
        pack.Size = request.Size;
        pack.Kind = CreateProductPackCommandHandler.ParsePackKind(request.Kind);
        pack.IsDefault = request.IsDefault;

        // Qadoqqa bog'langan barkodlar bitta skanerlashda shu hajmni bildiradi — hajm o'zgarsa ular ham yangilanadi.
        var linked = await db.Barcodes.Where(b => b.PackId == pack.Id).ToListAsync(cancellationToken);
        foreach (var barcode in linked) barcode.PackQty = pack.Size;

        if (pack.IsDefault)
            await CreateProductPackCommandHandler.ClearOtherDefaultsAsync(db, pack.ProductId, pack.Id, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class DeleteProductPackCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteProductPackCommand, Unit>
{
    public async Task<Unit> Handle(DeleteProductPackCommand request, CancellationToken cancellationToken)
    {
        var pack = await db.ProductPacks.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Qadoq topilmadi.");

        db.ProductPacks.Remove(pack);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class CreateProductPackCommandValidator : AbstractValidator<CreateProductPackCommand>
{
    public CreateProductPackCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Size).GreaterThan(0);
    }
}

public sealed class UpdateProductPackCommandValidator : AbstractValidator<UpdateProductPackCommand>
{
    public UpdateProductPackCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Size).GreaterThan(0);
    }
}
