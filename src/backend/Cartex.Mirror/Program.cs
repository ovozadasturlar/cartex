using Cartex.Mirror;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("public", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();
app.UseRateLimiter();

var dataDir = app.Configuration["Mirror:DataPath"] ?? Path.Combine(AppContext.BaseDirectory, "data");
Directory.CreateDirectory(dataDir);
var keys = (app.Configuration["Mirror:Keys"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToHashSet();

static bool ValidToken(string token) =>
    token.Length is >= 8 and <= 64 && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/receipts", async (HttpRequest request) =>
{
    if (keys.Count == 0 || !request.Headers.TryGetValue("X-License-Key", out var key) || !keys.Contains(key.ToString()))
        return Results.Unauthorized();
    var body = await request.ReadFromJsonAsync<ReceiptPush>();
    if (body is null || !ValidToken(body.Token) || string.IsNullOrEmpty(body.Html))
        return Results.BadRequest();
    await File.WriteAllTextAsync(Path.Combine(dataDir, body.Token + ".html"), body.Html);
    if (!string.IsNullOrEmpty(body.PdfBase64))
        await File.WriteAllBytesAsync(Path.Combine(dataDir, body.Token + ".pdf"), Convert.FromBase64String(body.PdfBase64));
    return Results.Ok();
});

app.MapGet("/r/{token}", async (string token) =>
{
    if (!ValidToken(token)) return Results.NotFound();
    var path = Path.Combine(dataDir, token + ".html");
    return File.Exists(path)
        ? Results.Content(await File.ReadAllTextAsync(path), "text/html; charset=utf-8")
        : Results.NotFound();
}).RequireRateLimiting("public");

app.MapGet("/r/{token}/pdf", async (string token) =>
{
    if (!ValidToken(token)) return Results.NotFound();
    var path = Path.Combine(dataDir, token + ".pdf");
    return File.Exists(path)
        ? Results.File(await File.ReadAllBytesAsync(path), "application/pdf", $"chek-{token[..8]}.pdf")
        : Results.NotFound();
}).RequireRateLimiting("public");

app.Run();

namespace Cartex.Mirror
{
    public sealed record ReceiptPush(string Token, string? Html, string? PdfBase64);
}

public partial class Program;
