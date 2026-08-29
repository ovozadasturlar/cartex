using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Loyalty.Commands;

public record DeleteDiscountRuleCommand(long Id) : ICommand<Unit>;

public sealed class DeleteDiscountRuleCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteDiscountRuleCommand, Unit>
{
    public async Task<Unit> Handle(DeleteDiscountRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await db.DiscountRules.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Discount rule not found.");
        rule.IsDeleted = true;
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
