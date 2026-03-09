using MediatR;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Shops.Commands;

public record UpdateShopCommand(long Id, string Name, string? Address, string? Phone, decimal CashbackRate, bool IsActive) : IRequest<Unit>;

public sealed class UpdateShopCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateShopCommand, Unit>
{
    public async Task<Unit> Handle(UpdateShopCommand request, CancellationToken cancellationToken)
    {
        var shop = await db.Shops.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new Exception("Shop not found.");

        shop.Name = request.Name;
        shop.Address = request.Address;
        shop.Phone = request.Phone;
        shop.CashbackRate = request.CashbackRate;
        shop.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
