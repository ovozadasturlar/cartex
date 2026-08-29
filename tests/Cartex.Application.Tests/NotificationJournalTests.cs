using Cartex.Application.Common.Messaging;
using Cartex.Application.Notifications.Queries;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Authorization;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public class NotificationJournalTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task Stats_filter_by_channel_provider_and_count_billable_units()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.NotificationDeliveries.AddRange(
            Delivery(NotificationChannel.Sms, "eskiz", NotificationDeliveryStatus.Delivered, 2),
            Delivery(NotificationChannel.Sms, "eskiz", NotificationDeliveryStatus.Failed, 1),
            Delivery(NotificationChannel.Email, "smtp.gmail.com", NotificationDeliveryStatus.Accepted, 1));
        await db.SaveChangesAsync();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var stats = await sender.Send(new GetNotificationStatsQuery(Channel: "Sms", Provider: "eskiz"));

        Assert.Equal(2, stats.Deliveries);
        Assert.Equal(2, stats.Attempts);
        Assert.Equal(1, stats.Delivered);
        Assert.Equal(1, stats.Failed);
        Assert.Equal(2, stats.BillableUnits);
    }

    [Fact]
    public async Task Journal_masks_recipient_and_content_without_sensitive_permission()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FirstAsync(x => x.Username == "admin");
        var business = await db.Businesses.FirstAsync();
        var branch = await db.Branches.FirstAsync();
        Fixture.CurrentUser.AsCashier(user.Id, business.Id, branch.Id);
        Fixture.CurrentUser.Granted.Add(AppPermissions.Notifications.JournalView);
        db.NotificationDeliveries.Add(Delivery(NotificationChannel.Sms, "eskiz", NotificationDeliveryStatus.Accepted, 1));
        await db.SaveChangesAsync();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var rows = await sender.Send(new GetNotificationJournalQuery());

        var row = Assert.Single(rows);
        Assert.Equal("••••", row.Recipient);
        Assert.Null(row.Content);
    }

    [Fact]
    public async Task Journal_and_stats_accept_unspecified_date_filters()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.NotificationDeliveries.Add(Delivery(
            NotificationChannel.Sms,
            "date-filter-test",
            NotificationDeliveryStatus.Delivered,
            1));
        await db.SaveChangesAsync();

        var from = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-1), DateTimeKind.Unspecified);
        var to = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(1), DateTimeKind.Unspecified);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var rows = await sender.Send(new GetNotificationJournalQuery
        {
            From = from,
            To = to,
            Provider = "date-filter-test"
        });
        var stats = await sender.Send(new GetNotificationStatsQuery(
            From: from,
            To: to,
            Provider: "date-filter-test"));

        Assert.Single(rows);
        Assert.Equal(1, stats.Deliveries);
        Assert.Equal(1, stats.Delivered);
    }

    private static NotificationDelivery Delivery(
        NotificationChannel channel,
        string provider,
        NotificationDeliveryStatus status,
        int units)
    {
        var delivery = new NotificationDelivery
        {
            Channel = channel,
            Purpose = "test",
            Recipient = channel == NotificationChannel.Email ? "customer@example.com" : "+998901234567",
            Content = "Maxfiy xabar",
            Status = status
        };
        delivery.Attempts.Add(new NotificationDeliveryAttempt
        {
            NotificationDelivery = delivery,
            AttemptNumber = 1,
            Provider = provider,
            Status = status,
            Units = units
        });
        return delivery;
    }
}
