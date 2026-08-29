using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Commands;

public record DeleteProductCommand(long Id) : ICommand<Unit>;

public sealed class DeleteProductCommandHandler(IApplicationDbContext db, IAuditService audit) : IRequestHandler<DeleteProductCommand, Unit>
{
    public async Task<Unit> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Product not found.");

        var variantIds = await db.ProductVariants.IgnoreQueryFilters()
            .Where(v => v.ProductId == product.Id)
            .Select(v => v.Id)
            .ToArrayAsync(cancellationToken);

        if (variantIds.Length == 0)
            throw new BusinessRuleException("Mahsulot varianti topilmadi.");

        var hasOperations =
            await db.SaleItems.IgnoreQueryFilters().AnyAsync(x => variantIds.Contains(x.VariantId), cancellationToken)
            || await db.SupplyItems.IgnoreQueryFilters().AnyAsync(x => variantIds.Contains(x.VariantId), cancellationToken)
            || await db.Stocks.IgnoreQueryFilters().AnyAsync(x => variantIds.Contains(x.VariantId), cancellationToken)
            || await db.StockTransfers.IgnoreQueryFilters().AnyAsync(x => variantIds.Contains(x.VariantId), cancellationToken)
            || await db.StockAdjustments.IgnoreQueryFilters().AnyAsync(x => variantIds.Contains(x.VariantId), cancellationToken)
            || await db.Prepacks.IgnoreQueryFilters().AnyAsync(x => variantIds.Contains(x.VariantId), cancellationToken)
            || await db.CartItems.IgnoreQueryFilters().AnyAsync(x => variantIds.Contains(x.VariantId), cancellationToken);

        if (hasOperations)
            throw new BusinessRuleException("Operatsiyada qatnashgan mahsulotni o'chirib bo'lmaydi.");

        var now = DateTime.UtcNow;
        var barcodes = await db.Barcodes.IgnoreQueryFilters().Where(x => variantIds.Contains(x.VariantId)).ToListAsync(cancellationToken);
        var prices = await db.ProductPrices.IgnoreQueryFilters().Where(x => variantIds.Contains(x.VariantId)).ToListAsync(cancellationToken);
        var catalogEntries = await db.BranchCatalogEntries.IgnoreQueryFilters().Where(x => variantIds.Contains(x.VariantId)).ToListAsync(cancellationToken);
        var packs = await db.ProductPacks.IgnoreQueryFilters().Where(x => x.ProductId == product.Id).ToListAsync(cancellationToken);
        var variants = await db.ProductVariants.IgnoreQueryFilters().Where(x => x.ProductId == product.Id).ToListAsync(cancellationToken);

        foreach (var entity in barcodes.Cast<ISoftDeletable>()
                     .Concat(prices)
                     .Concat(catalogEntries)
                     .Concat(packs)
                     .Concat(variants))
        {
            entity.IsDeleted = true;
            entity.DeletedAt = now;
        }

        product.IsDeleted = true;
        product.DeletedAt = now;
        audit.Add("product.delete", "products", product.Id, new { product.Name });
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
