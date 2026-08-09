using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Application.Common.Measurement;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.ProductPacks.Commands;

public record CreateProductPackCommand(long ProductId, string Name, decimal Size, PackKind Kind, bool IsDefault) : ICommand<long>;

public record UpdateProductPackCommand(long Id, string Name, decimal Size, PackKind Kind, bool IsDefault) : ICommand<Unit>;

public record DeleteProductPackCommand(long Id) : ICommand<Unit>;

public sealed class CreateProductPackCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateProductPackCommand, long>
{
    public async Task<long> Handle(CreateProductPackCommand request, CancellationToken cancellationToken)
    {
        var policy = await db.Products
            .Where(p => p.Id == request.ProductId)
            .Select(p => new { Step = p.QuantityStepOverride ?? p.Unit.DefaultQuantityStep })
            .FirstOrDefaultAsync(cancellationToken);
        if (policy is null)
            throw new NotFoundException("Mahsulot topilmadi.");
        if (!QuantityPolicyService.IsValid(request.Size, policy.Step))
            throw new BusinessRuleException($"Qadoq miqdori {policy.Step:0.###} qadamiga mos emas.", "quantity_step_violation");

        var pack = new ProductPack
        {
            ProductId = request.ProductId,
            Name = request.Name.Trim(),
            Size = request.Size,
            Kind = request.Kind,
            IsDefault = request.IsDefault
        };

        db.ProductPacks.Add(pack);
        if (pack.IsDefault) await ClearOtherDefaultsAsync(db, request.ProductId, pack.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return pack.Id;
    }

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
        var pack = await db.ProductPacks.Include(p => p.Product).ThenInclude(p => p.Unit).FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Qadoq topilmadi.");

        var step = pack.Product.QuantityStepOverride ?? pack.Product.Unit.DefaultQuantityStep;
        if (!QuantityPolicyService.IsValid(request.Size, step))
            throw new BusinessRuleException($"Qadoq miqdori {step:0.###} qadamiga mos emas.", "quantity_step_violation");

        pack.Name = request.Name.Trim();
        pack.Size = request.Size;
        pack.Kind = request.Kind;
        pack.IsDefault = request.IsDefault;

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
