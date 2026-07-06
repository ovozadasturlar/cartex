using Cartex.Application.Common.Messaging;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Cartex.Domain.Enums;
using Cartex.Persistence;

namespace Cartex.Application.ProductTypes.Commands;

public record UpdateProductTypeCommand(long Id, string Name, bool TracksExpiry, MeasureMode MeasureMode, string? AttributeSchema = null) : ICommand<Unit>;

public sealed class UpdateProductTypeCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateProductTypeCommand, Unit>
{
    public async Task<Unit> Handle(UpdateProductTypeCommand request, CancellationToken cancellationToken)
    {
        var type = await db.ProductTypes.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Product type not found.");

        type.Name = request.Name;
        type.TracksExpiry = request.TracksExpiry;
        type.MeasureMode = request.MeasureMode;
        type.AttributeSchema = request.AttributeSchema;

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

public sealed class UpdateProductTypeCommandValidator : AbstractValidator<UpdateProductTypeCommand>
{
    public UpdateProductTypeCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
    }
}
