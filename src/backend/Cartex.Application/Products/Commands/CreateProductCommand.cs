using MediatR;
using FluentValidation;
using Cartex.Persistence;
using Cartex.Domain.Entities;

namespace Cartex.Application.Products.Commands;

public record CreateProductCommand(string Name, long? CategoryId, long UnitId, decimal MinStock, List<string>? Barcodes) : IRequest<long>;

public sealed class CreateProductCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateProductCommand, long>
{
    public async Task<long> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Name = request.Name,
            CategoryId = request.CategoryId,
            UnitId = request.UnitId,
            MinStock = request.MinStock
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        if (request.Barcodes is not null)
        {
            foreach (var code in request.Barcodes)
            {
                db.Barcodes.Add(new Barcode
                {
                    ProductId = product.Id,
                    Code = code,
                    PackQty = 1
                });
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return product.Id;
    }
}

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
    }
}
