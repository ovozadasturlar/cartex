using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Commands;

public record SetProductStateCommand(long Id, bool IsEnabled) : ICommand<Unit>;

public sealed class SetProductStateCommandHandler(IApplicationDbContext db, IAuditService audit)
    : IRequestHandler<SetProductStateCommand, Unit>
{
    public async Task<Unit> Handle(SetProductStateCommand request, CancellationToken cancellationToken)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Product not found.");

        product.IsEnabled = request.IsEnabled;
        audit.Add(request.IsEnabled ? "product.enable" : "product.disable", "products", product.Id, new { product.Name });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
