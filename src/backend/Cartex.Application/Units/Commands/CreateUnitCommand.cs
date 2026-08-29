using FluentValidation;
using Cartex.Persistence;
using Cartex.Domain.Enums;

namespace Cartex.Application.Units.Commands;

public record CreateUnitCommand(
    string Name,
    string ShortName,
    string Dimension = "Count",
    decimal Factor = 1,
    bool? AllowFractional = null,
    bool? DefaultAllowAmountEntry = null) : ICommand<long>;

public sealed class CreateUnitCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateUnitCommand, long>
{
    public async Task<long> Handle(CreateUnitCommand request, CancellationToken cancellationToken)
    {
        var dimension = Enum.Parse<UnitDimension>(request.Dimension, true);
        var unit = new Cartex.Domain.Entities.Unit
        {
            Name = request.Name,
            ShortName = request.ShortName,
            Dimension = dimension,
            Factor = request.Factor,
            AllowFractional = request.AllowFractional ?? dimension != UnitDimension.Count,
            DefaultAllowAmountEntry = request.DefaultAllowAmountEntry ?? dimension != UnitDimension.Count
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
        RuleFor(x => x.Dimension).Must(x => Enum.TryParse<UnitDimension>(x, true, out _)).WithMessage("O'lchov turi noto'g'ri.");
        RuleFor(x => x.Factor).GreaterThan(0);
    }
}
