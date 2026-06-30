using Cartex.Application.Common.Interfaces;
using Cartex.Auth.Authorization;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StorageController(IObjectStorage storage) : ControllerBase
{
    private const long MaxFileSize = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/png", "image/jpeg", "image/webp", "image/gif" };

    [HttpPost("upload")]
    [HasPermission(AppPermissions.Products.Manage)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest();

        if (file.Length > MaxFileSize)
            throw new BusinessRuleException("Fayl hajmi 5 MB dan oshmasligi kerak.");

        if (!AllowedContentTypes.Contains(file.ContentType))
            throw new BusinessRuleException("Faqat rasm fayllariga ruxsat (png, jpeg, webp, gif).");

        await using var stream = file.OpenReadStream();
        var key = await storage.UploadAsync(stream, file.Length, file.ContentType, Path.GetExtension(file.FileName), HttpContext.RequestAborted);
        return Ok(new { key });
    }

    [HttpGet("url")]
    [HasPermission(AppPermissions.Products.View)]
    public async Task<IActionResult> GetUrl([FromQuery] string key)
    {
        var url = await storage.GetUrlAsync(key, HttpContext.RequestAborted);
        return url is null ? NotFound() : Ok(new { url });
    }
}
