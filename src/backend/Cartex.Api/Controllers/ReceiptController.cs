using Cartex.Application.Common.Interfaces;
using Cartex.Infrastructure.Notifications;
using Cartex.Application.Common.Settings;
using Cartex.Application.Sales.Queries;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
            return Content(ReceiptHtmlRenderer.Render(receipt, new ReceiptSettings { HeaderText = receiptSettings.HeaderText, FooterText = receiptSettings.FooterText, PaperWidth = receiptSettings.PaperWidth }), "text/html; charset=utf-8");
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
        var opts = new ReceiptSettings { HeaderText = receiptSettings.HeaderText, FooterText = receiptSettings.FooterText, PaperWidth = receiptSettings.PaperWidth, PaperFormat = receiptSettings.PaperFormat };
        var pdf = (size ?? receiptSettings.PaperFormat).ToLowerInvariant() switch
        {
            "a4" => pdfRenderer.RenderDocument(receipt, opts, a4: true),
            "a5" => pdfRenderer.RenderDocument(receipt, opts),
            _ => pdfRenderer.Render(receipt, opts)
        };
        return File(pdf, "application/pdf", $"chek-{token[..Math.Min(8, token.Length)]}.pdf");
    }
}
