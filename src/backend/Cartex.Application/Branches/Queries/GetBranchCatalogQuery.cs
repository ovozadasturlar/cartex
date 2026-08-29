using Cartex.Application.Common.Search;
using Cartex.Application.Products.Queries;
using Cartex.Domain.Enums;
using Cartex.Domain.Common;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Cartex.Shared.Models.Branches;
using Cartex.Shared.Search;

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
                ProductSearchFold = variant.Product.SearchFold,
                variant.Code,
                Barcode = variant.Barcodes.Select(x => x.Code).FirstOrDefault(),
                IsActive = entry != null && entry.FirstActivityAt != null,
                Visibility = entry == null ? BranchCatalogVisibilityOverride.Auto : entry.VisibilityOverride
            };

        var search = CatalogSearch.Parse(request.Search);
        foreach (var term in search.Terms)
        {
            var pattern = $"%{term.Value}%";
            var folded = SearchFold.Fuzzy(term.Value);
            var foldedPattern = $"%{folded}%";
            query = term.Field switch
            {
                CatalogSearchField.Name => query.Where(x => EF.Functions.ILike(x.ProductName, pattern)
                    || (folded.Length > 0 && x.ProductSearchFold != null && EF.Functions.ILike(x.ProductSearchFold, foldedPattern))),
                CatalogSearchField.Barcode => query.Where(x => x.Barcode != null && EF.Functions.ILike(x.Barcode, pattern)),
                CatalogSearchField.Code => query.Where(x => x.Code != null && EF.Functions.ILike(x.Code, pattern)),
                CatalogSearchField.Price => query.Where(_ => false),
                _ => query.Where(x => EF.Functions.ILike(x.ProductName, pattern)
                    || (folded.Length > 0 && x.ProductSearchFold != null && EF.Functions.ILike(x.ProductSearchFold, foldedPattern))
                    || (x.Code != null && EF.Functions.ILike(x.Code, pattern))
                    || (x.Barcode != null && EF.Functions.ILike(x.Barcode, pattern)))
            };
        }

        var count = await query.CountAsync(cancellationToken);
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var strictNameQuery = ProductCatalogSearch.StrictNameQuery(search);
        var pageQuery = query;
        Dictionary<long, int>? relevanceOrder = null;
        if (strictNameQuery.Length > 0)
        {
            var candidates = await query.Select(x => new { x.VariantId, x.ProductName }).ToListAsync(cancellationToken);
            var pageIds = candidates
                .OrderBy(x => ProductCatalogSearch.NameRank(x.ProductName, strictNameQuery))
                .ThenBy(x => x.ProductName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.VariantId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => x.VariantId)
                .ToArray();
            relevanceOrder = pageIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
            pageQuery = query.Where(x => pageIds.Contains(x.VariantId));
        }
        else
        {
            pageQuery = query
                .OrderBy(x => x.ProductName)
                .ThenBy(x => x.VariantId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);
        }

        var items = await pageQuery
            .Select(x => new BranchCatalogItemDto(x.VariantId, x.ProductName, x.Code, x.Barcode, x.IsActive, x.Visibility.ToString()))
            .ToListAsync(cancellationToken);
        if (relevanceOrder is not null)
            items = items.OrderBy(x => relevanceOrder[x.VariantId]).ToList();

        return new BranchCatalogPageDto(items, count);
    }

    private void EnsureBranchAccess(long branchId)
    {
        if (!currentUser.CanAccessAllBranches && !currentUser.BranchIds.Contains(branchId))
            throw new ForbiddenException("Branch access denied.");
    }
}
