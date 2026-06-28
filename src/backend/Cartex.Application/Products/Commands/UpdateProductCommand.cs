using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Products.Commands;

public record UpdateProductCommand(long Id, string Name, long? CategoryId, long UnitId, decimal MinStock) : ICommand<Unit>;

public sealed class UpdateProductCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateProductCommand, Unit>
{
    public async Task<Unit> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Product not found.");

        product.Name = request.Name;
        product.CategoryId = request.CategoryId;
        product.UnitId = request.UnitId;
        product.MinStock = request.MinStock;

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
