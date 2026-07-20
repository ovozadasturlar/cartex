using Cartex.Application.Common.Images;
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
public class StorageController(IObjectStorage storage, IImageProcessor processor) : ControllerBase
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

        var ct = HttpContext.RequestAborted;
        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var key = await ImageStore.SaveAsync(storage, processor, buffer, file.ContentType, Path.GetExtension(file.FileName), ct);
        return Ok(new { key });
    }

    [HttpGet("url")]
    [HasPermission(AppPermissions.Products.View)]
    public async Task<IActionResult> GetUrl([FromQuery] string key)
    {
        var url = await storage.GetUrlAsync(key, HttpContext.RequestAborted);
        return url is null ? NotFound() : Ok(new { url });
    }

    [AllowAnonymous]
    [HttpGet("content")]
    public async Task<IActionResult> GetContent([FromQuery] string key, [FromQuery] bool thumb = false)
    {
        var result = thumb ? await storage.DownloadAsync($"t_{key}", HttpContext.RequestAborted) : null;
        result ??= await storage.DownloadAsync(key, HttpContext.RequestAborted);
        if (result is null)
            return NotFound();
        Response.Headers.CacheControl = "public,max-age=86400,immutable";
        return File(result.Value.Content, result.Value.ContentType);
    }
}
