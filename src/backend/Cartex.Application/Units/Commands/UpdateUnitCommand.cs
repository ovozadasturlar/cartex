using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;
using Cartex.Domain.Enums;

namespace Cartex.Application.Units.Commands;

public record UpdateUnitCommand(
    long Id,
    string Name,
    string ShortName,
    string Dimension = "Count",
    decimal Factor = 1,
    bool? AllowFractional = null,
    bool? DefaultAllowAmountEntry = null) : ICommand<Unit>;

public sealed class UpdateUnitCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateUnitCommand, Unit>
{
    public async Task<Unit> Handle(UpdateUnitCommand request, CancellationToken cancellationToken)
    {
        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Unit not found.");

        var dimension = Enum.Parse<UnitDimension>(request.Dimension, true);
        if (unit.IsSystem &&
            (unit.Name != request.Name || unit.ShortName != request.ShortName ||
             unit.Dimension != dimension || unit.Factor != request.Factor))
            throw new BusinessRuleException("Tizim o'lchov birligining nomi va konversiyasini o'zgartirib bo'lmaydi.");

        if (!unit.IsSystem)
        {
            unit.Name = request.Name;
            unit.ShortName = request.ShortName;
            unit.Dimension = dimension;
            unit.Factor = request.Factor;
        }
        if (request.AllowFractional is { } allowFractional)
            unit.AllowFractional = allowFractional;
        if (request.DefaultAllowAmountEntry is { } allowsAmountEntry)
            unit.DefaultAllowAmountEntry = allowsAmountEntry;

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
        RuleFor(x => x.Dimension).Must(x => Enum.TryParse<UnitDimension>(x, true, out _)).WithMessage("O'lchov turi noto'g'ri.");
        RuleFor(x => x.Factor).GreaterThan(0);
    }
}
