using Cartex.Api.Services;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Sales.Queries;
using Cartex.Application.Common.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cartex.Api.Controllers;

[ApiController]
[Route("r")]
[AllowAnonymous]
public class ReceiptController(ISender sender) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<IActionResult> GetReceipt(string token)
    {
        var receipt = await sender.Send(new GetReceiptByTokenQuery(token));
        if (receipt is null)
            return NotFound();

        if (Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
            return Content(ReceiptHtmlRenderer.Render(receipt), "text/html; charset=utf-8");

        return Ok(receipt);
    }

    [HttpGet("{token}/pdf")]
    public async Task<IActionResult> GetReceiptPdf(string token, [FromServices] IReceiptPdfRenderer pdfRenderer)
    {
        var receipt = await sender.Send(new GetReceiptByTokenQuery(token));
        if (receipt is null)
            return NotFound();

        return File(pdfRenderer.Render(receipt), "application/pdf", $"chek-{token[..Math.Min(8, token.Length)]}.pdf");
    }
}
