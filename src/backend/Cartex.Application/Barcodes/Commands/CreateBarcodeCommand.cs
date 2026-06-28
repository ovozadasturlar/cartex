using MediatR;
using FluentValidation;
using Cartex.Persistence;
using Cartex.Domain.Entities;

namespace Cartex.Application.Barcodes.Commands;

public record CreateBarcodeCommand(long ProductId, string Code, decimal PackQty) : ICommand<long>;

public sealed class CreateBarcodeCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateBarcodeCommand, long>
{
    public async Task<long> Handle(CreateBarcodeCommand request, CancellationToken cancellationToken)
    {
        var barcode = new Barcode
        {
            ProductId = request.ProductId,
            Code = request.Code,
            PackQty = request.PackQty
        };

        db.Barcodes.Add(barcode);
        await db.SaveChangesAsync(cancellationToken);

        return barcode.Id;
    }
}

public sealed class CreateBarcodeCommandValidator : AbstractValidator<CreateBarcodeCommand>
{
    public CreateBarcodeCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(60);
    }
}
