using Cartex.Application.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Enums;

namespace Cartex.Application.Units.Commands;

public record UpdateUnitCommand(long Id, string Name, string ShortName, string Dimension = "Count", decimal Factor = 1) : ICommand<Unit>;

public sealed class UpdateUnitCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateUnitCommand, Unit>
{
    public async Task<Unit> Handle(UpdateUnitCommand request, CancellationToken cancellationToken)
    {
        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Unit not found.");

        if (unit.IsSystem)
            throw new BusinessRuleException("Tizim o'lchov birligini o'zgartirib bo'lmaydi.");

        unit.Name = request.Name;
        unit.ShortName = request.ShortName;
        unit.Dimension = Enum.TryParse<UnitDimension>(request.Dimension, true, out var d) ? d : UnitDimension.Count;
        unit.Factor = request.Factor;

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateUnitCommandValidator : AbstractValidator<UpdateUnitCommand>
{
    public UpdateUnitCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(20);
        RuleFor(x => x.ShortName).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Factor).GreaterThan(0);
    }
}
