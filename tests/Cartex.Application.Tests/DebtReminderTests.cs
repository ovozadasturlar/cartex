using Cartex.Application.Common.Settings;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Sales.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Infrastructure.Notifications;
using Cartex.Persistence;
using Cartex.Application.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class DebtReminderTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    private async Task<long> SeedDebtorAsync(bool optOut = false, DateOnly? dueDate = null)
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branch1 = (await db.Branches.FirstAsync(b => b.Name == "Asosiy filial")).Id;
        var warehouse1 = (await db.Warehouses.FirstAsync(w => w.Name == "Asosiy ombor")).Id;
        var businessId = (await db.Businesses.FirstAsync()).Id;
        var adminId = (await db.Users.FirstAsync(u => u.Username == "admin")).Id;
        var productId = (await db.Products.FirstAsync(p => p.Name == "Smesitel oshxona Zegor")).Id;
        var variantId = (await db.ProductVariants.FirstAsync(v => v.ProductId == productId)).Id;
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branch1);

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var customerId = await sender.Send(new CreateCustomerCommand("Eslatma Mijoz", "+998" + Random.Shared.NextInt64(100_000_000, 999_999_999), null, 0m,
            CreditLimit: 10_000_000m, NotificationsOptOut: optOut));
        await sender.Send(new CreateSaleCommand(warehouse1, customerId, 0, 0, 0, [new CreateSaleItemDto(variantId, 2)])
        {
            DebtDueDate = dueDate
        });
        return customerId;
    }

    private static ReminderSettings Config(int minDays = 0, decimal minBalance = 0, int repeatDays = 7, int daysBeforeDue = 1) =>
        new()
        {
            Enabled = true,
            MinDaysOverdue = minDays,
            MinBalance = minBalance,
            RepeatEveryDays = repeatDays,
            NotifyBeforeDue = true,
            DaysBeforeDue = daysBeforeDue,
            NotifyOnDueDate = true
        };

    [Fact]
    public async Task Enqueue_writes_outbox_and_log_then_dedups_within_window()
    {
        var customerId = await SeedDebtorAsync();

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var outboxBefore = await db.NotificationOutbox.CountAsync();
        var first = await DebtReminderScheduler.EnqueueDueRemindersAsync(db, Config(), DateTime.UtcNow, CancellationToken.None);
        var second = await DebtReminderScheduler.EnqueueDueRemindersAsync(db, Config(), DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(outboxBefore + 1, await db.NotificationOutbox.CountAsync());
        Assert.Equal(1, await db.DebtReminderLogs.CountAsync(l => l.CustomerId == customerId));
    }

    [Fact]
    public async Task Enqueue_fires_again_after_repeat_window()
    {
        var customerId = await SeedDebtorAsync();

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await DebtReminderScheduler.EnqueueDueRemindersAsync(db, Config(repeatDays: 7), DateTime.UtcNow, CancellationToken.None);
        var later = await DebtReminderScheduler.EnqueueDueRemindersAsync(db, Config(repeatDays: 7), DateTime.UtcNow.AddDays(8), CancellationToken.None);

        Assert.Equal(1, later);
        Assert.Equal(2, await db.DebtReminderLogs.CountAsync(l => l.CustomerId == customerId));
    }

    [Fact]
    public async Task Filters_respect_min_days_min_balance_and_opt_out()
    {
        await SeedDebtorAsync(optOut: true);

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(0, await DebtReminderScheduler.EnqueueDueRemindersAsync(db, Config(), DateTime.UtcNow, CancellationToken.None));

        await SeedDebtorAsync();
        Assert.Equal(0, await DebtReminderScheduler.EnqueueDueRemindersAsync(db, Config(minDays: 30), DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(0, await DebtReminderScheduler.EnqueueDueRemindersAsync(db, Config(minBalance: 1_000_000_000m), DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(1, await DebtReminderScheduler.EnqueueDueRemindersAsync(db, Config(), DateTime.UtcNow, CancellationToken.None));
    }

    [Fact]
    public void Send_window_is_two_hours()
    {
        Assert.True(DebtReminderScheduler.IsInWindow(10, 10));
        Assert.True(DebtReminderScheduler.IsInWindow(11, 10));
        Assert.False(DebtReminderScheduler.IsInWindow(12, 10));
        Assert.False(DebtReminderScheduler.IsInWindow(9, 10));
    }

    [Fact]
    public async Task Due_date_debt_reminds_near_due_even_before_min_days()
    {
        await SeedDebtorAsync(dueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(1, await DebtReminderScheduler.EnqueueDueRemindersAsync(db, Config(minDays: 30), DateTime.UtcNow, CancellationToken.None));
        var outbox = await db.NotificationOutbox.OrderByDescending(o => o.Id).FirstAsync();
        Assert.Contains("debt_due_soon", outbox.Payload);
    }

    [Fact]
    public async Task Future_due_date_does_not_remind_yet()
    {
        await SeedDebtorAsync(dueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(0, await DebtReminderScheduler.EnqueueDueRemindersAsync(db, Config(), DateTime.UtcNow, CancellationToken.None));
    }

    [Fact]
    public async Task Configured_advance_days_controls_pre_due_reminder()
    {
        await SeedDebtorAsync(dueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(0, await DebtReminderScheduler.EnqueueDueRemindersAsync(db, Config(daysBeforeDue: 1), DateTime.UtcNow, CancellationToken.None));
        Assert.Equal(1, await DebtReminderScheduler.EnqueueDueRemindersAsync(db, Config(daysBeforeDue: 2), DateTime.UtcNow, CancellationToken.None));
        var outbox = await db.NotificationOutbox.OrderByDescending(o => o.Id).FirstAsync();
        Assert.Contains("debt_due_soon", outbox.Payload);
    }

    [Fact]
    public async Task Due_date_day_uses_dedicated_template()
    {
        await SeedDebtorAsync(dueDate: DateOnly.FromDateTime(DateTime.UtcNow));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(1, await DebtReminderScheduler.EnqueueDueRemindersAsync(db, Config(), DateTime.UtcNow, CancellationToken.None));
        var outbox = await db.NotificationOutbox.OrderByDescending(o => o.Id).FirstAsync();
        Assert.Contains("debt_due_today", outbox.Payload);
    }

    [Fact]
    public async Task Overdue_due_date_uses_overdue_template()
    {
        await SeedDebtorAsync(dueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)));

        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(1, await DebtReminderScheduler.EnqueueDueRemindersAsync(db, Config(minDays: 30), DateTime.UtcNow, CancellationToken.None));
        var outbox = await db.NotificationOutbox.OrderByDescending(o => o.Id).FirstAsync();
        Assert.Contains("debt_reminder", outbox.Payload);
        Assert.DoesNotContain("debt_due_soon", outbox.Payload);
    }
}
