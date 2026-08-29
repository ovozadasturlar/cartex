using Cartex.Application.Common.Search;
using Cartex.Domain.Entities;
using Cartex.Shared.Search;
using Microsoft.EntityFrameworkCore;

namespace Cartex.Application.Products.Queries;

internal static class ProductCatalogSearch
{
    public static IQueryable<Product> Apply(IQueryable<Product> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;

        foreach (var token in CatalogSearch.Parse(search).Terms.OrderByDescending(term => term.Value.Length))
        {
            var term = $"%{token.Value}%";
            var folded = SearchFold.Fuzzy(token.Value);
            var foldedTerm = $"%{folded}%";
            query = token.Field switch
            {
                CatalogSearchField.Name => query.Where(product => EF.Functions.ILike(product.Name, term)
                    || (folded.Length > 0 && product.SearchFold != null && EF.Functions.ILike(product.SearchFold, foldedTerm))),
                CatalogSearchField.Barcode => query.Where(product => product.Variants.Any(variant => variant.Barcodes.Any(barcode => EF.Functions.ILike(barcode.Code, term)))),
                CatalogSearchField.Code => query.Where(product =>
                    (product.IkpuCode != null && EF.Functions.ILike(product.IkpuCode, term))
                    || product.Variants.Any(variant => variant.Code != null && EF.Functions.ILike(variant.Code, term))),
                CatalogSearchField.Price when token.Price is { } price => query.Where(product => product.Variants.Any(variant => variant.Prices.Any(value => value.SellingPrice == price))),
                CatalogSearchField.Price => query.Where(_ => false),
                _ => query.Where(product =>
                    EF.Functions.ILike(product.Name, term)
                    || (folded.Length > 0 && product.SearchFold != null && EF.Functions.ILike(product.SearchFold, foldedTerm))
                    || (product.IkpuCode != null && EF.Functions.ILike(product.IkpuCode, term))
                    || product.Variants.Any(variant => variant.Code != null && EF.Functions.ILike(variant.Code, term))
                    || product.Variants.Any(variant => variant.Barcodes.Any(barcode => EF.Functions.ILike(barcode.Code, term))))
            };
        }

        return query;
    }

    public static string StrictNameQuery(CatalogSearch search) =>
        SearchFold.Strict(string.Join(' ', search.Terms
            .Where(x => x.Field is CatalogSearchField.Any or CatalogSearchField.Name)
            .Select(x => x.Value)));

    public static int NameRank(string name, string strictQuery)
    {
        if (strictQuery.Length == 0)
            return 2;

        var strictName = SearchFold.Strict(name);
        if (strictName.StartsWith(strictQuery, StringComparison.Ordinal))
            return 0;
        return strictName.Contains(strictQuery, StringComparison.Ordinal) ? 1 : 2;
    }

    public static IQueryable<Product> ApplyPriceRange(IQueryable<Product> query, decimal? minPrice, decimal? maxPrice)
    {
        if (minPrice is { } min)
            query = query.Where(product => product.Variants.Any(variant => variant.IsDefault && variant.Prices.Any(price => price.WarehouseId == null && price.SellingPrice >= min)));
        if (maxPrice is { } max)
            query = query.Where(product => product.Variants.Any(variant => variant.IsDefault && variant.Prices.Any(price => price.WarehouseId == null && price.SellingPrice <= max)));
        return query;
    }
}
