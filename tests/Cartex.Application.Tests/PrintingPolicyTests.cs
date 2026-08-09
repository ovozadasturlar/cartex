using System.Text.Json;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Printing;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Cartex.Shared.Models.Printing;
using Cartex.Shared.Models.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DomainPrintJobStatus = Cartex.Domain.Enums.PrintJobStatus;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class PrintingPolicyTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Automatic_receipt_job_is_atomic_and_keeps_branch_template_snapshot()
    {
        long branchId;
        long warehouseId;
        long businessId;
        long adminId;
        long variantId;
        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            branchId = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).SingleAsync();
            warehouseId = await db.Warehouses.Where(x => x.Name == "Asosiy ombor").Select(x => x.Id).SingleAsync();
            businessId = await db.Businesses.Select(x => x.Id).SingleAsync();
            adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).SingleAsync();
            variantId = await db.ProductVariants.Select(x => x.Id).FirstAsync();
        }

        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);
        await TestShift.OpenAsync(Fixture);

        string token;
        using (var scope = Fixture.CreateScope())
        {
            var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            await settings.SetAsync(SettingKeys.Receipt, new ReceiptSettings
            {
                HeaderText = "GLOBAL",
                ShowLogo = true,
                ShowCustomerPhone = true,
                ShowCustomerEmail = false
            });

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var branchTemplate = new ReceiptSettingsDto(
                "FILIAL",
                "Rahmat",
                42,
                "Thermal",
                ShowLogo: false,
                ShowCustomerPhone: false,
                ShowCustomerEmail: true);
            var policy = await sender.Send(new UpdateReceiptPrintPolicyCommand(branchId,
                new UpdateReceiptPrintPolicyRequest(true, 2, true, branchTemplate)));
            Assert.True(policy.AutoPrintOnSale);
            Assert.Equal(2, policy.DefaultCopies);
            Assert.Equal("FILIAL", policy.ReceiptOverride?.HeaderText);

            var price = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ProductPrices
                .Where(x => x.VariantId == variantId).Select(x => x.SellingPrice).FirstAsync();
            token = (await sender.Send(new CreateSaleCommand(
                warehouseId,
                null,
                price,
                0,
                0,
                [new CreateSaleItemDto(variantId, 1)]))).ReceiptToken;
        }

        using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var job = await db.PrintJobs.SingleAsync(x => x.IdempotencyKey == $"auto-receipt:{token}");
            Assert.Equal(DomainPrintJobStatus.Pending, job.Status);
            Assert.Equal(2, job.Copies);
            using var payload = JsonDocument.Parse(job.PayloadJson);
            Assert.Equal(token, payload.RootElement.GetProperty("receiptToken").GetString());
            var snapshot = payload.RootElement.GetProperty("receiptSettings");
            Assert.Equal("FILIAL", snapshot.GetProperty("headerText").GetString());
            Assert.False(snapshot.GetProperty("showLogo").GetBoolean());
            Assert.False(snapshot.GetProperty("showCustomerPhone").GetBoolean());
            Assert.True(snapshot.GetProperty("showCustomerEmail").GetBoolean());

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var bootstrap = await sender.Send(new GetPrintingBootstrapQuery(branchId, null));
            Assert.Equal("GLOBAL", bootstrap.BusinessReceipt.HeaderText);
            Assert.Equal("FILIAL", bootstrap.EffectiveReceipt.HeaderText);
            Assert.True(bootstrap.ReceiptPolicy.AutoPrintOnSale);
            Assert.False(string.IsNullOrWhiteSpace(bootstrap.Revision));
        }
    }
}
