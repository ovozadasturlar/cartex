using FluentValidation;
using Cartex.Persistence;
using Cartex.Domain.Enums;

namespace Cartex.Application.Units.Commands;

public record CreateUnitCommand(string Name, string ShortName, string Dimension = "Count", decimal Factor = 1) : ICommand<long>;

public sealed class CreateUnitCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateUnitCommand, long>
{
    public async Task<long> Handle(CreateUnitCommand request, CancellationToken cancellationToken)
    {
        var unit = new Cartex.Domain.Entities.Unit
        {
            Name = request.Name,
            ShortName = request.ShortName,
            Dimension = Enum.TryParse<UnitDimension>(request.Dimension, true, out var d) ? d : UnitDimension.Count,
            Factor = request.Factor
        };

        db.Units.Add(unit);
        await db.SaveChangesAsync(cancellationToken);

        return unit.Id;
    }
}

public sealed class CreateUnitCommandValidator : AbstractValidator<CreateUnitCommand>
{
    public CreateUnitCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(20);
        RuleFor(x => x.ShortName).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Factor).GreaterThan(0);
    }
}
