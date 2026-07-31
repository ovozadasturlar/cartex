using Cartex.Application.Common.Interfaces;
using Cartex.Infrastructure.Notifications;
using Cartex.Application.Common.Settings;
using Cartex.Application.Sales.Queries;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.IO.Compression;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("r")]
[AllowAnonymous]
[EnableRateLimiting("public")]
public class ReceiptController(ISender sender) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<ActionResult<ReceiptDto>> GetReceipt(string token)
    {
        var receipt = await sender.Send(new GetReceiptByTokenQuery(token));
        if (receipt is null)
            return NotFound();

        if (Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            var receiptSettings = await sender.Send(new Cartex.Application.Settings.Queries.GetReceiptSettingsQuery());
            return Content(ReceiptHtmlRenderer.Render(receipt, ToRenderSettings(receiptSettings)), "text/html; charset=utf-8");
        }

        return Ok(receipt);
    }

    [HttpGet("{token}/pdf")]
    public async Task<IActionResult> GetReceiptPdf(string token, [FromServices] IReceiptPdfRenderer pdfRenderer, [FromQuery] string? size = null)
    {
        var receipt = await sender.Send(new GetReceiptByTokenQuery(token));
        if (receipt is null)
            return NotFound();

        var receiptSettings = await sender.Send(new Cartex.Application.Settings.Queries.GetReceiptSettingsQuery());
        var opts = ToRenderSettings(receiptSettings);
        var pdf = (size ?? receiptSettings.PaperFormat).ToLowerInvariant() switch
        {
            "a4" => pdfRenderer.RenderDocument(receipt, opts, a4: true),
            "a5" => pdfRenderer.RenderDocument(receipt, opts),
            _ => pdfRenderer.Render(receipt, opts)
        };
        return File(pdf, "application/pdf", $"chek-{token[..Math.Min(8, token.Length)]}.pdf");
    }

    [HttpGet("{token}/print-pages")]
    public async Task<IActionResult> GetReceiptPrintPages(
        string token,
        [FromServices] IReceiptPdfRenderer pdfRenderer,
        [FromQuery] string size = "a4",
        [FromQuery] string orientation = "portrait")
    {
        var receipt = await sender.Send(new GetReceiptByTokenQuery(token));
        if (receipt is null)
            return NotFound();

        var receiptSettings = await sender.Send(new Cartex.Application.Settings.Queries.GetReceiptSettingsQuery());
        var images = pdfRenderer.RenderDocumentImages(
            receipt,
            ToRenderSettings(receiptSettings),
            a4: !string.Equals(size, "a5", StringComparison.OrdinalIgnoreCase),
            landscape: string.Equals(orientation, "landscape", StringComparison.OrdinalIgnoreCase));
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var i = 0; i < images.Count; i++)
            {
                var entry = archive.CreateEntry($"page-{i + 1:D3}.png", CompressionLevel.Fastest);
                using var stream = entry.Open();
                await stream.WriteAsync(images[i]);
            }
        }
        return File(output.ToArray(), "application/zip");
    }

    private static ReceiptSettings ToRenderSettings(Cartex.Application.Settings.Queries.ReceiptSettingsDto settings) =>
        new()
        {
            HeaderText = settings.HeaderText,
            FooterText = settings.FooterText,
            PaperWidth = settings.PaperWidth,
            PaperFormat = settings.PaperFormat,
            ShowBusinessName = settings.ShowBusinessName,
            ShowBranchName = settings.ShowBranchName,
            ShowAddress = settings.ShowAddress,
            ShowPhone = settings.ShowPhone,
            ShowCashier = settings.ShowCashier,
            ShowCustomer = settings.ShowCustomer,
            ShowReceiptNumber = settings.ShowReceiptNumber,
            ShowPaymentDetails = settings.ShowPaymentDetails,
            ShowQrCode = settings.ShowQrCode,
            ShowElectronicLink = settings.ShowElectronicLink,
            PublicReceiptBaseUrl = settings.PublicReceiptBaseUrl
        };
}
