using Cartex.Application.Common.Messaging;
using FluentValidation;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;

namespace Cartex.Application.ProductTypes.Commands;

public record CreateProductTypeCommand(string Name, bool TracksExpiry, MeasureMode MeasureMode, string? AttributeSchema = null) : ICommand<long>;

public sealed class CreateProductTypeCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateProductTypeCommand, long>
{
    public async Task<long> Handle(CreateProductTypeCommand request, CancellationToken cancellationToken)
    {
        var type = new ProductType
        {
            Name = request.Name,
            TracksExpiry = request.TracksExpiry,
            MeasureMode = request.MeasureMode,
            AttributeSchema = request.AttributeSchema
        };

        db.ProductTypes.Add(type);
        await db.SaveChangesAsync(cancellationToken);

        return type.Id;
    }
}

public sealed class CreateProductTypeCommandValidator : AbstractValidator<CreateProductTypeCommand>
{
    public CreateProductTypeCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
    }
}
