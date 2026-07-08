using Cartex.Application.Barcodes.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class BarcodeGenerationTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Generate_WithoutPrefixConfig_FallsBackToCtx()
    {
        using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var variantId = (await db.ProductVariants.OrderBy(v => v.Id).FirstAsync()).Id;

        var unit = await sender.Send(new GenerateBarcodeCommand(variantId));
        var pack = await sender.Send(new GenerateBarcodeCommand(variantId, 6));

        Assert.Equal($"CTX-{variantId:D6}", unit);
        Assert.Equal($"CTX-P6-{variantId:D6}", pack);
    }
}
