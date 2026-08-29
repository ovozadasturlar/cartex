using System.Text.Json;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Ordering.Commands;
using Cartex.Application.Printing;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Printing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using WirePrintJobKind = Cartex.Shared.Models.Printing.PrintJobKind;

namespace Cartex.Application.Tests;

/// Written from docs/domain-rules.md SOZ-14: whether the shop hands out a proforma is the
/// owner's decision and lives in the sales policy, so the server refuses the job — hiding the
/// button is not where a policy is enforced (SOZ-10).
[Collection("database")]
public class ProformaPrintPolicyTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<(long BranchId, string CartCode)> SetupAsync(bool allowProforma)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branchId = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouseId = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;

        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);
        await TestShift.OpenAsync(Fixture);

        await scope.ServiceProvider.GetRequiredService<ISettingsService>()
            .SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings { PrintCartProforma = allowProforma });

        var variantId = await db.ProductVariants.Select(x => x.Id).FirstAsync();
        var cartCode = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new SubmitCartCommand(warehouseId, null, [new SubmitCartItemDto(variantId, 1)])
            {
                Kind = CartKind.Proforma
            });

        return (branchId, cartCode);
    }

    private static CreatePrintJobCommand Job(long branchId, string cartCode) =>
        new(new CreatePrintJobRequest(
            branchId,
            WirePrintJobKind.CartProforma,
            "cart",
            cartCode,
            JsonSerializer.SerializeToElement(new { cartCode }),
            IdempotencyKey: Guid.NewGuid().ToString("N")));

    [Fact]
    public async Task SOZ_14_A_shop_that_does_not_hand_out_proformas_has_the_job_refused()
    {
        var (branchId, cartCode) = await SetupAsync(allowProforma: false);

        using var scope = Fixture.CreateScope();
        var error = await Assert.ThrowsAnyAsync<Exception>(
            () => scope.ServiceProvider.GetRequiredService<ISender>().Send(Job(branchId, cartCode)));

        Assert.True(error is BusinessRuleException { Code: "proforma_print_disabled" },
            $"expected proforma_print_disabled, got {error.GetType().Name}: {error.Message}");
    }

    [Fact]
    public async Task SOZ_14_The_proforma_job_is_accepted_by_default()
    {
        var (branchId, cartCode) = await SetupAsync(allowProforma: true);

        using var scope = Fixture.CreateScope();
        var job = await scope.ServiceProvider.GetRequiredService<ISender>().Send(Job(branchId, cartCode));

        Assert.Equal(WirePrintJobKind.CartProforma, job.Kind);
    }
}
