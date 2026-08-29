using System.Text.Json;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.CustomerPayments.Commands;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Printing;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.Printing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using WirePrintJobKind = Cartex.Shared.Models.Printing.PrintJobKind;
using WirePrintJobStatus = Cartex.Shared.Models.Printing.PrintJobStatus;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class LocalPrintCompletionTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task CHOP_03_CompletedLocally_does_not_require_remote_use_permission()
    {
        var context = await SetupCashierAsync(AppPermissions.Printing.ZReportPrint);
        var request = LocalZReport(context.BranchId, context.ShiftId, "local-no-remote");

        using var scope = Fixture.CreateScope();
        var job = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new CreatePrintJobCommand(request));

        Assert.Equal(WirePrintJobStatus.Completed, job.Status);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public async Task CHOP_03_Remote_job_still_requires_remote_use_permission()
    {
        var context = await SetupCashierAsync(AppPermissions.Printing.ZReportPrint);
        var request = LocalZReport(context.BranchId, context.ShiftId, "remote-needs-permission") with
        {
            CompletedLocally = false
        };

        using var scope = Fixture.CreateScope();
        await Assert.ThrowsAsync<ForbiddenException>(() => scope.ServiceProvider
            .GetRequiredService<ISender>().Send(new CreatePrintJobCommand(request)));
    }

    [Fact]
    public async Task CHOP_04_Disabled_money_documents_reject_CompletedLocally_history()
    {
        var context = await SetupAdminAsync();
        var customerId = await CreateCustomerAsync();
        long documentId;
        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await scope.ServiceProvider.GetRequiredService<ISettingsService>()
                .SetAsync(SettingKeys.SalesPolicy, new SalesPolicySettings
                {
                    AllowCustomerCredit = true,
                    PrintMoneyDocuments = false
                });
            documentId = (await sender.Send(new CreateCustomerPaymentCommand(
                customerId,
                context.BranchId,
                [new CustomerPaymentTenderInput(PaymentMethod.Card, "UZS", 1_000m)],
                IdempotencyKey: "print-policy-payment"))).Id;
        }

        Fixture.CurrentUser.AsCashier(context.UserId, context.BusinessId, context.BranchId);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Printing.DocumentPrint);
        var request = new CreatePrintJobRequest(
            context.BranchId,
            WirePrintJobKind.Document,
            "customer_payment",
            documentId.ToString(),
            JsonSerializer.SerializeToElement(new { documentId }),
            IdempotencyKey: "local-money-policy",
            DeviceId: "cashier-device",
            CompletedLocally: true);

        using var check = Fixture.CreateScope();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => check.ServiceProvider
            .GetRequiredService<ISender>().Send(new CreatePrintJobCommand(request)));

        Assert.Equal("money_document_print_disabled", error.Code);
    }

    [Fact]
    public async Task CHOP_05_CompletedLocally_idempotency_key_creates_one_job()
    {
        var context = await SetupCashierAsync(AppPermissions.Printing.ZReportPrint);
        var request = LocalZReport(context.BranchId, context.ShiftId, "local-idempotent");

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var first = await sender.Send(new CreatePrintJobCommand(request));
        var second = await sender.Send(new CreatePrintJobCommand(request));
        var count = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .PrintJobs.CountAsync(x => x.IdempotencyKey == request.IdempotencyKey);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, count);
    }

    private async Task<(long BranchId, long BusinessId, long UserId, long ShiftId)> SetupCashierAsync(
        string permission)
    {
        var context = await SetupAdminAsync();
        var shiftId = await TestShift.OpenAsync(Fixture);
        Fixture.CurrentUser.AsCashier(context.UserId, context.BusinessId, context.BranchId);
        Fixture.CurrentUser.Granted.Add(permission);
        return (context.BranchId, context.BusinessId, context.UserId, shiftId);
    }

    private async Task<(long BranchId, long BusinessId, long UserId)> SetupAdminAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branchId = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).SingleAsync();
        var businessId = await db.Businesses.Select(x => x.Id).SingleAsync();
        var userId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).SingleAsync();
        Fixture.CurrentUser.AsAdmin(userId, businessId, branchId);
        return (branchId, businessId, userId);
    }

    private async Task<long> CreateCustomerAsync()
    {
        using var scope = Fixture.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateCustomerCommand(
                "Lokal chop mijoz",
                "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999),
                null,
                0m,
                CreditLimit: 1_000_000m));
    }

    private static CreatePrintJobRequest LocalZReport(long branchId, long shiftId, string key) =>
        new(
            branchId,
            WirePrintJobKind.ZReport,
            "shift",
            shiftId.ToString(),
            JsonSerializer.SerializeToElement(new { shiftId }),
            IdempotencyKey: key,
            DeviceId: "cashier-device",
            CompletedLocally: true);
}
