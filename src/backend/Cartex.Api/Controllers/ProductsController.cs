using Cartex.Application.Common.Interfaces;
using Cartex.Application.ProductPacks.Commands;
using Cartex.Application.ProductPacks.Queries;
using Cartex.Application.Products.Commands;
using Cartex.Application.Products.Import;
using Cartex.Application.Products.Queries;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [HasPermission(AppPermissions.Products.View)]
    public async Task<ActionResult<IReadOnlyCollection<ProductDto>>> GetProducts([FromQuery] GetProductsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("totals")]
    [HasPermission(AppPermissions.Products.View)]
    public async Task<ActionResult<ProductsTotalsDto>> GetTotals([FromQuery] GetProductsTotalsQuery query)
    {
        var result = await sender.Send(query);
        return Ok(result);
    }

    [HttpGet("lookup")]
    [HasPermission(AppPermissions.Products.View)]
    public async Task<ActionResult<IReadOnlyCollection<ProductOptionDto>>> GetLookup()
    {
        var result = await sender.Send(new GetProductLookupQuery());
        return Ok(result);
    }

    [HttpGet("category-counts")]
    [HasPermission(AppPermissions.Products.View)]
    public async Task<ActionResult<IReadOnlyCollection<CategoryCountDto>>> GetCategoryCounts()
    {
        var result = await sender.Send(new GetProductCategoryCountsQuery());
        return Ok(result);
    }

    [HttpGet("by-barcode")]
    [HasPermission(AppPermissions.Sales.Pick, AppPermissions.Sales.Create)]
    public async Task<ActionResult<ProductLookupDto>> GetByBarcode([FromQuery] string code, [FromQuery] long warehouseId, [FromQuery] bool forSale = false)
    {
        var result = await sender.Send(new GetProductByBarcodeQuery(code, warehouseId, forSale));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("catalog-lookup")]
    [HasPermission(AppPermissions.Products.View)]
    public async Task<ActionResult<ProductCatalogInfo>> CatalogLookup([FromQuery] string barcode)
    {
        var result = await sender.Send(new GetCatalogInfoByBarcodeQuery(barcode));
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("variants/{id:long}/price-info")]
    [HasPermission(AppPermissions.Supplies.Create, AppPermissions.Supplies.Edit)]
    public async Task<ActionResult<VariantPriceInfoDto>> GetVariantPriceInfo(long id, [FromQuery] long warehouseId)
    {
        var result = await sender.Send(new GetVariantPriceInfoQuery(id, warehouseId));
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(AppPermissions.Products.Create)]
    public async Task<ActionResult<long>> CreateProduct(CreateProductCommand command)
    {
        var id = await sender.Send(command);
        return Ok(id);
    }

    [HttpPut("{id:long}")]
    [HasPermission(AppPermissions.Products.Edit)]
    public async Task<IActionResult> UpdateProduct(long id, UpdateProductCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }

    [HttpPut("{id:long}/state")]
    [HasPermission(AppPermissions.Products.Toggle)]
    public async Task<IActionResult> SetProductState(long id, SetProductStateCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [HasPermission(AppPermissions.Products.Delete)]
    public async Task<IActionResult> DeleteProduct(long id)
    {
        await sender.Send(new DeleteProductCommand(id));
        return NoContent();
    }

    [HttpPost("price")]
    [HasPermission(AppPermissions.Products.Edit)]
    public async Task<IActionResult> SetPrice(SetProductPriceCommand command)
    {
        await sender.Send(command);
        return NoContent();
    }

    [HttpGet("import/template")]
    [HasPermission(AppPermissions.Products.Import)]
    public async Task<IActionResult> ImportTemplate()
    {
        var content = await sender.Send(new GetImportTemplateQuery());
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "cartex-import.xlsx");
    }

    [HttpPost("import/preview")]
    [HasPermission(AppPermissions.Products.Import)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<ProductImportPreviewDto>> ImportPreview(IFormFile? file, [FromQuery] string? mapping = null)
    {
        if (file is null || file.Length == 0)
            throw new BusinessRuleException("Fayl tanlanmagan.");
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Faqat .xlsx fayl qabul qilinadi.");

        await using var content = file.OpenReadStream();
        var result = await sender.Send(new PreviewProductImportQuery(content, mapping));
        return Ok(result);
    }

    [HttpPost("import")]
    [HasPermission(AppPermissions.Products.Import)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ImportResultDto>> Import(ImportProductsCommand command)
    {
        var result = await sender.Send(command);
        return Ok(result);
    }

    [HttpGet("{productId:long}/variants")]
    [HasPermission(AppPermissions.Products.View)]
    public async Task<ActionResult<IReadOnlyCollection<VariantDto>>> GetVariants(long productId)
    {
        var result = await sender.Send(new GetProductVariantsQuery(productId));
        return Ok(result);
    }

    [HttpPost("{productId:long}/variants")]
    [HasPermission(AppPermissions.Products.Create, AppPermissions.Products.Edit)]
    public async Task<ActionResult<long>> CreateVariant(long productId, CreateVariantCommand command)
    {
        var id = await sender.Send(command with { ProductId = productId });
        return Ok(id);
    }

    [HttpPut("variants/{id:long}")]
    [HasPermission(AppPermissions.Products.Edit)]
    public async Task<IActionResult> UpdateVariant(long id, UpdateVariantCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }

    [HttpDelete("variants/{id:long}")]
    [HasPermission(AppPermissions.Products.Delete)]
    public async Task<IActionResult> DeleteVariant(long id)
    {
        await sender.Send(new DeleteVariantCommand(id));
        return NoContent();
    }

    [HttpGet("{productId:long}/packs")]
    [HasPermission(AppPermissions.Products.View)]
    public async Task<ActionResult<IReadOnlyCollection<ProductPackDto>>> GetPacks(long productId)
    {
        var result = await sender.Send(new GetProductPacksQuery(productId));
        return Ok(result);
    }

    [HttpPost("{productId:long}/packs")]
    [HasPermission(AppPermissions.Products.Create, AppPermissions.Products.Edit)]
    public async Task<ActionResult<long>> CreatePack(long productId, CreateProductPackCommand command)
    {
        var id = await sender.Send(command with { ProductId = productId });
        return Ok(id);
    }

    [HttpPut("packs/{id:long}")]
    [HasPermission(AppPermissions.Products.Edit)]
    public async Task<IActionResult> UpdatePack(long id, UpdateProductPackCommand command)
    {
        await sender.Send(command with { Id = id });
        return NoContent();
    }

    [HttpDelete("packs/{id:long}")]
    [HasPermission(AppPermissions.Products.Delete)]
    public async Task<IActionResult> DeletePack(long id)
    {
        await sender.Send(new DeleteProductPackCommand(id));
        return NoContent();
    }
}
