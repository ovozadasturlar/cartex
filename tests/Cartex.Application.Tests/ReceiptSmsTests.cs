using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Settings;
using Cartex.Application.Notifications;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Enums;
using Cartex.Domain.Events;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class ReceiptSmsTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task SMS_13_disabled_does_not_create_job()
    {
        var sale = await CreateSaleAsync(withCustomer: true, withPhone: true, sendOnSale: false);

        await HandleCompletedAsync(sale);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.SmsGatewayJobs.AnyAsync(x => x.IdempotencyKey == $"receipt-sms:{sale.Id}"));
    }

    [Fact]
    public async Task SMS_13_enabled_with_customer_phone_creates_one_receipt_link_job()
    {
        var sale = await CreateSaleAsync(withCustomer: true, withPhone: true, sendOnSale: true);

        await HandleCompletedAsync(sale);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var job = await db.SmsGatewayJobs.SingleAsync(x => x.IdempotencyKey == $"receipt-sms:{sale.Id}");
        Assert.Equal(SmsGatewayJobKind.ReceiptLink, job.Kind);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task SMS_13_missing_customer_or_phone_keeps_sale_and_creates_no_job(bool withCustomer, bool withPhone)
    {
        var sale = await CreateSaleAsync(withCustomer, withPhone, sendOnSale: true);

        await HandleCompletedAsync(sale);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.Sales.AnyAsync(x => x.Id == sale.Id));
        Assert.False(await db.SmsGatewayJobs.AnyAsync(x => x.IdempotencyKey == $"receipt-sms:{sale.Id}"));
    }

    [Fact]
    public async Task SMS_13_job_creation_failure_does_not_roll_back_sale()
    {
        var sale = await CreateSaleAsync(withCustomer: true, withPhone: true, sendOnSale: true,
            template: new string('x', 4001));

        await Assert.ThrowsAnyAsync<Exception>(() => HandleCompletedAsync(sale));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.Sales.AnyAsync(x => x.Id == sale.Id));
    }

    [Fact]
    public async Task SMS_13_repeated_event_creates_only_one_automatic_job()
    {
        var sale = await CreateSaleAsync(withCustomer: true, withPhone: true, sendOnSale: true);

        await HandleCompletedAsync(sale);
        await HandleCompletedAsync(sale);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.SmsGatewayJobs.CountAsync(x => x.IdempotencyKey == $"receipt-sms:{sale.Id}"));
    }

    [Fact]
    public async Task SMS_13_sale_event_is_persisted_to_outbox_and_receipt_handler_is_registered()
    {
        var sale = await CreateSaleAsync(withCustomer: true, withPhone: true, sendOnSale: true);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saleEvents = await db.NotificationOutbox
            .Where(x => x.EventType == typeof(SaleCompletedEvent).FullName)
            .Select(x => x.Payload)
            .ToListAsync();
        Assert.Contains(saleEvents, payload => payload.Contains(sale.ReceiptToken, StringComparison.Ordinal));

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ReceiptSmsSaleCompletedHandler>());
    }

    [Fact]
    public async Task SMS_14_manual_send_without_customer_phone_is_rejected()
    {
        var sale = await CreateSaleAsync(withCustomer: true, withPhone: false, sendOnSale: false);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.Send(new SendReceiptSmsCommand(sale.Id, string.Empty)));
    }

    [Fact]
    public async Task SMS_14_manual_send_can_be_repeated_without_marketing_consent()
    {
        var sale = await CreateSaleAsync(withCustomer: true, withPhone: true, sendOnSale: false);

        using (var scope = Fixture.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var preview = await sender.Send(new GetReceiptSmsPreviewQuery(sale.Id));
            await sender.Send(new SendReceiptSmsCommand(sale.Id, preview.ConfirmationToken));
            await sender.Send(new SendReceiptSmsCommand(sale.Id, preview.ConfirmationToken));
        }

        using var verify = Fixture.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var jobs = await db.SmsGatewayJobs
            .Where(x => x.CustomerId == sale.CustomerId && x.Kind == SmsGatewayJobKind.ReceiptLink)
            .ToListAsync();
        Assert.Equal(2, jobs.Count);
        Assert.Equal(2, jobs.Select(x => x.IdempotencyKey).Distinct().Count());
        Assert.Equal(2, await db.AuditLogs.CountAsync(x =>
            x.Action == "receipt.sms.manual" && x.RecordId == sale.Id));
    }

    [Fact]
    public async Task SMS_14_manual_preview_contains_recipient_store_and_public_receipt_link()
    {
        var sale = await CreateSaleAsync(withCustomer: true, withPhone: true, sendOnSale: false);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var storeName = await db.Businesses.Select(x => x.Name).SingleAsync();
        var preview = await sender.Send(new GetReceiptSmsPreviewQuery(sale.Id));

        Assert.Equal("+998901234567", preview.Recipient);
        Assert.StartsWith(storeName, preview.Text);
        Assert.Contains($"https://receipt.example/r/{sale.ReceiptToken}", preview.Text);
    }

    [Fact]
    public async Task SMS_14_manual_send_rejects_payload_changed_after_confirmation()
    {
        var sale = await CreateSaleAsync(withCustomer: true, withPhone: true, sendOnSale: false);

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var preview = await sender.Send(new GetReceiptSmsPreviewQuery(sale.Id));
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var sms = await settings.GetAsync<SmsSettings>(SettingKeys.Sms) ?? throw new InvalidOperationException();
        sms.ReceiptLinkTemplate = "{store}: yangi mazmun {link}";
        await settings.SetAsync(SettingKeys.Sms, sms);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            sender.Send(new SendReceiptSmsCommand(sale.Id, preview.ConfirmationToken)));
    }

    /// Mijoz havolani ochmasdan ham asosiy raqamlarni ko'rishi kerak.
    [Fact]
    public async Task SMS_13_receipt_text_fills_amount_and_receipt_placeholders()
    {
        var sale = await CreateSaleAsync(withCustomer: true, withPhone: true, sendOnSale: false,
            template: "{store}: chek {receipt} {date} jami {total} to'landi {paid} {link}");

        using var scope = Fixture.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var preview = await sender.Send(new GetReceiptSmsPreviewQuery(sale.Id));

        Assert.Contains($"chek {sale.Id}", preview.Text, StringComparison.Ordinal);
        Assert.Contains(DateTime.UtcNow.ToString("dd.MM.yyyy"), preview.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("{total}", preview.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("{paid}", preview.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("{receipt}", preview.Text, StringComparison.Ordinal);
    }

    private async Task<SaleSnapshot> CreateSaleAsync(
        bool withCustomer,
        bool withPhone,
        bool sendOnSale,
        string? template = null)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branchId = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).SingleAsync();
        var warehouseId = await db.Warehouses.Where(x => x.Name == "Asosiy ombor").Select(x => x.Id).SingleAsync();
        var businessId = await db.Businesses.Select(x => x.Id).SingleAsync();
        var adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).SingleAsync();
        var variantId = await db.ProductVariants
            .Where(x => x.Product.Name == "Smesitel oshxona Zegor")
            .Select(x => x.Id)
            .SingleAsync();
        long? customerId = null;
        if (withCustomer)
        {
            var customer = await db.Customers.FirstAsync();
            customer.Phone = withPhone ? "+998901234567" : null;
            customer.AllowMarketingSms = false;
            customerId = customer.Id;
            await db.SaveChangesAsync();
        }

        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);
        await TestShift.OpenAsync(Fixture);
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.Sms, new SmsSettings
        {
            Enabled = true,
            SendReceiptOnSale = sendOnSale,
            ReceiptLinkTemplate = template ?? "{store}: chekingiz {link}"
        });
        await settings.SetAsync(SettingKeys.Notification, new NotificationSettings
        {
            PublicBaseUrl = "https://receipt.example"
        });

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new CreateSaleCommand(warehouseId, customerId, 385000, 0, 0,
            [new CreateSaleItemDto(variantId, 1)]));
        return new SaleSnapshot(result.SaleId, result.ReceiptToken, customerId, 385000);
    }

    private async Task HandleCompletedAsync(SaleSnapshot sale)
    {
        using var scope = Fixture.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ReceiptSmsSaleCompletedHandler>();
        await handler.Handle(new DomainEventNotification<SaleCompletedEvent>(
            new SaleCompletedEvent(sale.ReceiptToken, sale.CustomerId, sale.TotalAmount)), CancellationToken.None);
    }

    private sealed record SaleSnapshot(long Id, string ReceiptToken, long? CustomerId, decimal TotalAmount);
}
