using MediatR;
using FluentValidation;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Unit = MediatR.Unit;

namespace Cartex.Application.Loyalty.Commands;

public record UpdateLoyaltyProgramCommand(bool IsEnabled, CashbackBase Base, decimal TotalPercent) : ICommand<Unit>;

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
        program.Base = request.Base;
        program.TotalPercent = request.TotalPercent;

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateLoyaltyProgramCommandValidator : AbstractValidator<UpdateLoyaltyProgramCommand>
{
    public UpdateLoyaltyProgramCommandValidator()
    {
        RuleFor(x => x.TotalPercent).InclusiveBetween(0, 100);
    }
}
