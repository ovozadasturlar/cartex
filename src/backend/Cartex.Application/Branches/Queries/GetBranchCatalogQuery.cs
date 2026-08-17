using Cartex.Application.Common.Search;
using Cartex.Domain.Enums;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Branches;

namespace Cartex.Application.Branches.Queries;

public record GetBranchCatalogQuery(long BranchId, string? Search = null, int Page = 1, int PageSize = 80) : IRequest<BranchCatalogPageDto>;

public sealed class GetBranchCatalogQueryHandler(IApplicationDbContext db, ICurrentUser currentUser) : IRequestHandler<GetBranchCatalogQuery, BranchCatalogPageDto>
{
    public async Task<BranchCatalogPageDto> Handle(GetBranchCatalogQuery request, CancellationToken cancellationToken)
    {
        EnsureBranchAccess(request.BranchId);
        var query =
            from variant in db.ProductVariants
            join entry in db.BranchCatalogEntries.Where(x => x.BranchId == request.BranchId) on variant.Id equals entry.VariantId into entries
            from entry in entries.DefaultIfEmpty()
            select new
            {
                VariantId = variant.Id,
                ProductName = variant.Product.Name,
                variant.Code,
                Barcode = variant.Barcodes.Select(x => x.Code).FirstOrDefault(),
                IsActive = entry != null && entry.FirstActivityAt != null,
                Visibility = entry == null ? BranchCatalogVisibilityOverride.Auto : entry.VisibilityOverride
            };

        foreach (var term in CatalogSearch.Parse(request.Search).Terms)
        {
            var pattern = $"%{term.Value}%";
            query = term.Field switch
            {
                CatalogSearchField.Name => query.Where(x => EF.Functions.ILike(x.ProductName, pattern)),
                CatalogSearchField.Barcode => query.Where(x => x.Barcode != null && EF.Functions.ILike(x.Barcode, pattern)),
                CatalogSearchField.Code => query.Where(x => x.Code != null && EF.Functions.ILike(x.Code, pattern)),
                CatalogSearchField.Price => query.Where(_ => false),
                _ => query.Where(x => EF.Functions.ILike(x.ProductName, pattern)
                    || (x.Code != null && EF.Functions.ILike(x.Code, pattern))
                    || (x.Barcode != null && EF.Functions.ILike(x.Barcode, pattern)))
            };
        }

        var count = await query.CountAsync(cancellationToken);
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var items = await query
            .OrderBy(x => x.ProductName)
            .ThenBy(x => x.VariantId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new BranchCatalogItemDto(x.VariantId, x.ProductName, x.Code, x.Barcode, x.IsActive, x.Visibility.ToString()))
            .ToListAsync(cancellationToken);

        return new BranchCatalogPageDto(items, count);
    }

    private void EnsureBranchAccess(long branchId)
    {
        if (!currentUser.CanAccessAllBranches && !currentUser.BranchIds.Contains(branchId))
            throw new ForbiddenException("Branch access denied.");
    }
}
