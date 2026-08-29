using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Prepacks.Commands;

public record CancelPrepackCommand(long Id) : ICommand<Unit>;

public sealed class CancelPrepackCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelPrepackCommand, Unit>
{
    public async Task<Unit> Handle(CancelPrepackCommand request, CancellationToken cancellationToken)
    {
        var prepack = await db.Prepacks.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Qadoq topilmadi.");
        if (prepack.Status == PrepackStatus.Sold)
            throw new BusinessRuleException("Sotilgan qadoqni bekor qilib bo'lmaydi.");

        prepack.IsDeleted = true;
        prepack.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
