using Cartex.Application.Common.Search;
using Cartex.Domain.Entities;
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
            query = token.Field switch
            {
                CatalogSearchField.Name => query.Where(product => EF.Functions.ILike(product.Name, term)),
                CatalogSearchField.Barcode => query.Where(product => product.Variants.Any(variant => variant.Barcodes.Any(barcode => EF.Functions.ILike(barcode.Code, term)))),
                CatalogSearchField.Code => query.Where(product =>
                    (product.IkpuCode != null && EF.Functions.ILike(product.IkpuCode, term))
                    || product.Variants.Any(variant => variant.Code != null && EF.Functions.ILike(variant.Code, term))),
                CatalogSearchField.Price when token.Price is { } price => query.Where(product => product.Variants.Any(variant => variant.Prices.Any(value => value.SellingPrice == price))),
                CatalogSearchField.Price => query.Where(_ => false),
                _ => query.Where(product =>
                    EF.Functions.ILike(product.Name, term)
                    || (product.IkpuCode != null && EF.Functions.ILike(product.IkpuCode, term))
                    || product.Variants.Any(variant => variant.Code != null && EF.Functions.ILike(variant.Code, term))
                    || product.Variants.Any(variant => variant.Barcodes.Any(barcode => EF.Functions.ILike(barcode.Code, term))))
            };
        }

        return query;
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
