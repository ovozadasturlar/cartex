using Cartex.Application.Common.Messaging;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Application.Customers.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Common.Exceptions;
using Cartex.Domain.Entities;
using Cartex.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

/// SMS-34: klient qaysi kanal yoqilganini bilmaydi, `auto` yuboradi va serverni tanlaydi.
[Collection("database")]
public sealed class CustomerMessageChannelTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task SMS_34_Auto_prefers_telegram_when_the_customer_is_connected()
    {
        using var scope = await ArrangeAsync(telegram: true, sms: true, email: true);
        var customerId = await AddCustomerAsync(scope, telegramChatId: "42", phone: "+998901234567", email: "a@b.uz");

        await SendAsync(scope, customerId, "auto");

        Assert.Equal(["telegram"], Fixture.MessageChannels.Used);
    }

    [Fact]
    public async Task SMS_34_Auto_falls_through_to_the_first_usable_channel()
    {
        using var scope = await ArrangeAsync(telegram: true, sms: false, email: true);
        var customerId = await AddCustomerAsync(scope, telegramChatId: null, phone: "+998901234567", email: "a@b.uz");

        await SendAsync(scope, customerId, "auto");

        Assert.Equal(["email"], Fixture.MessageChannels.Used);
    }

    [Fact]
    public async Task SMS_34_Auto_without_any_usable_channel_sends_nothing()
    {
        using var scope = await ArrangeAsync(telegram: true, sms: false, email: false);
        var customerId = await AddCustomerAsync(scope, telegramChatId: null, phone: "+998901234567", email: "a@b.uz");

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => SendAsync(scope, customerId, "auto"));

        Assert.Equal("no_message_channel", error.Code);
        Assert.Empty(Fixture.MessageChannels.Used);
    }

    [Fact]
    public async Task SMS_34_An_explicit_channel_separates_missing_address_from_disabled_channel()
    {
        using var scope = await ArrangeAsync(telegram: true, sms: false, email: true);
        var customerId = await AddCustomerAsync(scope, telegramChatId: null, phone: "+998901234567", email: null);

        var disabled = await Assert.ThrowsAsync<BusinessRuleException>(() => SendAsync(scope, customerId, "sms"));
        var missing = await Assert.ThrowsAsync<BusinessRuleException>(() => SendAsync(scope, customerId, "email"));

        Assert.Equal("SMS sozlanmagan.", disabled.Message);
        Assert.Equal("Mijoz emaili kiritilmagan.", missing.Message);
        Assert.Empty(Fixture.MessageChannels.Used);
    }

    private async Task<IServiceScope> ArrangeAsync(bool telegram, bool sms, bool email)
    {
        var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branchId = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).SingleAsync();
        var businessId = await db.Businesses.Select(x => x.Id).SingleAsync();
        var adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).SingleAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);

        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.Telegram, new TelegramSettings { Enabled = telegram, BotToken = "token" });
        await settings.SetAsync(SettingKeys.Sms, new SmsSettings { Enabled = sms, QuietHoursEnabled = false });
        await settings.SetAsync(SettingKeys.Email, new EmailSettings { Enabled = email });
        return scope;
    }

    private static async Task<long> AddCustomerAsync(IServiceScope scope, string? telegramChatId, string? phone, string? email)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var businessId = await db.Businesses.Select(x => x.Id).SingleAsync();
        var customer = new Customer
        {
            Party = new Party { BusinessId = businessId, FullName = "Mijoz", Phone = phone, Email = email },
            TelegramChatId = telegramChatId
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer.Id;
    }

    private static Task SendAsync(IServiceScope scope, long customerId, string channel) =>
        scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new SendCustomerMessageCommand(customerId, channel, "Salom"));
}
