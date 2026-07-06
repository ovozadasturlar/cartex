using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Products.Commands;

public record DeleteVariantCommand(long Id) : ICommand<Unit>;

public sealed class DeleteVariantCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteVariantCommand, Unit>
{
    public async Task<Unit> Handle(DeleteVariantCommand request, CancellationToken cancellationToken)
    {
        var variant = await db.ProductVariants.FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Variant not found.");

        if (variant.IsDefault)
            throw new BusinessRuleException("Asosiy variantni o'chirib bo'lmaydi.");

        db.ProductVariants.Remove(variant);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
