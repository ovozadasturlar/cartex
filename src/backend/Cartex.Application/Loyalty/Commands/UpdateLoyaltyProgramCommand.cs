using Cartex.Application.Common.Messaging;
using FluentValidation;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Loyalty.Commands;

public record UpdateLoyaltyProgramCommand(bool IsEnabled, decimal TotalPercent, decimal CashbackRounding = 0) : ICommand<Unit>;

public sealed class UpdateLoyaltyProgramCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateLoyaltyProgramCommand, Unit>
{
    public async Task<Unit> Handle(UpdateLoyaltyProgramCommand request, CancellationToken cancellationToken)
    {
        var program = await db.LoyaltyPrograms.FirstOrDefaultAsync(p => p.BranchId == null, cancellationToken);

        if (program is null)
        {
            program = new LoyaltyProgram();
            db.LoyaltyPrograms.Add(program);
        }

        program.IsEnabled = request.IsEnabled;
        program.TotalPercent = request.TotalPercent;
        program.CashbackRounding = request.CashbackRounding;

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateLoyaltyProgramCommandValidator : AbstractValidator<UpdateLoyaltyProgramCommand>
{
    public UpdateLoyaltyProgramCommandValidator()
    {
        RuleFor(x => x.TotalPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.CashbackRounding).Must(v => v is 0 or 1 or 100 or 1000);
    }
}
