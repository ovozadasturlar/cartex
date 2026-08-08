using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Units.Commands;

public record SetUnitStateCommand(long Id, bool IsEnabled, bool IsDefault) : ICommand<Unit>;

public sealed class SetUnitStateCommandHandler(IApplicationDbContext db) : IRequestHandler<SetUnitStateCommand, Unit>
{
    public async Task<Unit> Handle(SetUnitStateCommand request, CancellationToken cancellationToken)
    {
        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Unit not found.");

        if (!request.IsEnabled && (request.IsDefault || unit.IsDefault))
            throw new BusinessRuleException("Standart birlikni o'chirib bo'lmaydi — avval boshqasini standart qiling.");

        if (request.IsDefault && !unit.IsDefault)
            await db.Units
                .Where(u => u.Dimension == unit.Dimension && u.IsDefault && u.Id != unit.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false), cancellationToken);

        unit.IsEnabled = request.IsEnabled;
        unit.IsDefault = request.IsDefault;
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
