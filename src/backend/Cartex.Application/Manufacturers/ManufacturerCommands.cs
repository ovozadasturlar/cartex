using Cartex.Domain.Entities;
using Cartex.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

using Unit = Cartex.Application.Common.Messaging.Unit;

namespace Cartex.Application.Manufacturers;

public record ManufacturerDto(long Id, string Name);

public record GetManufacturersQuery : IRequest<IReadOnlyCollection<ManufacturerDto>>;

public sealed class GetManufacturersQueryHandler(IApplicationDbContext db) : IRequestHandler<GetManufacturersQuery, IReadOnlyCollection<ManufacturerDto>>
{
    public async Task<IReadOnlyCollection<ManufacturerDto>> Handle(GetManufacturersQuery request, CancellationToken cancellationToken) =>
        await db.Manufacturers
            .OrderBy(m => m.Name)
            .Select(m => new ManufacturerDto(m.Id, m.Name))
            .ToListAsync(cancellationToken);
}

public record CreateManufacturerCommand(string Name) : ICommand<long>;

public sealed class CreateManufacturerCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateManufacturerCommand, long>
{
    public async Task<long> Handle(CreateManufacturerCommand request, CancellationToken cancellationToken)
    {
        var manufacturer = new Manufacturer { Name = request.Name.Trim() };
        db.Manufacturers.Add(manufacturer);
        await db.SaveChangesAsync(cancellationToken);
        return manufacturer.Id;
    }
}

public sealed class CreateManufacturerCommandValidator : AbstractValidator<CreateManufacturerCommand>
{
    public CreateManufacturerCommandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
}

public record UpdateManufacturerCommand(long Id, string Name) : ICommand<Unit>;

public sealed class UpdateManufacturerCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateManufacturerCommand, Unit>
{
    public async Task<Unit> Handle(UpdateManufacturerCommand request, CancellationToken cancellationToken)
    {
        var manufacturer = await db.Manufacturers.FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Manufacturer not found.");
        manufacturer.Name = request.Name.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateManufacturerCommandValidator : AbstractValidator<UpdateManufacturerCommand>
{
    public UpdateManufacturerCommandValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
}

public record DeleteManufacturerCommand(long Id) : ICommand<Unit>;

public sealed class DeleteManufacturerCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteManufacturerCommand, Unit>
{
    public async Task<Unit> Handle(DeleteManufacturerCommand request, CancellationToken cancellationToken)
    {
        var manufacturer = await db.Manufacturers.FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Manufacturer not found.");
        manufacturer.IsDeleted = true;
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
