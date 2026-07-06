using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Cartex.Persistence;

namespace Cartex.Application.Barcodes.Commands;

public record DeleteBarcodeCommand(long Id) : ICommand<Unit>;

public sealed class DeleteBarcodeCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteBarcodeCommand, Unit>
{
    public async Task<Unit> Handle(DeleteBarcodeCommand request, CancellationToken cancellationToken)
    {
        var barcode = await db.Barcodes.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Barcode not found.");

        db.Barcodes.Remove(barcode);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
