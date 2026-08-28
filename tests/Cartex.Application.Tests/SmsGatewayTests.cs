using Cartex.Application.Common.Messaging;
using Cartex.Application.Sms;
using Cartex.Application.Tests.Common;
using Cartex.Domain.Entities;
using Cartex.Domain.Enums;
using Cartex.Persistence;
using Cartex.Shared.Models.SmsGateway;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Application.Notifications.Queries;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using SmsGatewayJobKind = Cartex.Domain.Enums.SmsGatewayJobKind;
using SmsGatewayJobStatus = Cartex.Domain.Enums.SmsGatewayJobStatus;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class SmsGatewayTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task SMS_02_Unconsented_device_is_not_assigned()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        db.SmsGatewayDevices.Add(Device(context.BranchId, "no-consent", 1, consented: false));
        await db.SaveChangesAsync();

        var job = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.Manual, "+998901234567",
            "Do'kon: xabar", 1, "sms-02", null, default);

        Assert.NotNull(job);
        Assert.Equal(SmsGatewayJobStatus.Pending, job.Status);
        Assert.Null(job.AssignedDeviceId);
    }

    [Fact]
    public async Task SMS_05_SMS_07_Quota_exhaustion_selects_next_priority_device()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.Sms, new SmsSettings { QuietHoursEnabled = false });
        var exhausted = Device(context.BranchId, "quota-full", 1);
        exhausted.MonthlyQuota = 5;
        exhausted.SentThisPeriod = 5;
        var available = Device(context.BranchId, "quota-open", 2);
        db.SmsGatewayDevices.AddRange(exhausted, available);
        await db.SaveChangesAsync();

        var job = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.DebtReminder, "+998901234567",
            "Do'kon: qarz", 1, "sms-07", null, default);

        Assert.Equal(available.Id, job!.AssignedDeviceId);
    }

    [Fact]
    public async Task SMS_08_No_matching_device_keeps_job_pending()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();

        var job = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.Manual, "+998901234567",
            "Do'kon: xabar", 1, "sms-08", null, default);

        Assert.Equal(SmsGatewayJobStatus.Pending, job!.Status);
    }

    [Fact]
    public async Task SMS_09_Promotion_without_customer_consent_is_not_created()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        var customer = new Customer { Party = new Party { BusinessId = context.BusinessId, FullName = "Mijoz" }, AllowMarketingSms = false };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var job = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.Promotion, "+998901234567",
            "Do'kon: aksiya", 1, "sms-09", customer.Id, default);

        Assert.Null(job);
        Assert.False(await db.SmsGatewayJobs.AnyAsync(x => x.IdempotencyKey == "sms-09"));
    }

    [Fact]
    public async Task SMS_10_Idempotency_key_creates_one_job()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();

        var first = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.Manual, "+998901234567",
            "Do'kon: bir", 1, "sms-10", null, default);
        var second = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.Manual, "+998901234567",
            "Do'kon: ikki", 1, "sms-10", null, default);

        Assert.Equal(first!.Id, second!.Id);
        Assert.Equal(1, await db.SmsGatewayJobs.CountAsync(x => x.IdempotencyKey == "sms-10"));
    }

    [Theory]
    [InlineData(60, 0)]
    [InlineData(0, 4)]
    [InlineData(301, 4)]
    public async Task SMS_06_Out_of_range_rate_limits_fail_validation(int maxPerHour, int minIntervalSeconds)
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var device = Device(context.BranchId, "invalid-rate", 1);
        const string token = "invalid-rate-token";
        device.CredentialHash = SmsGatewayCredential.Hash(token);
        db.SmsGatewayDevices.Add(device);
        await db.SaveChangesAsync();
        Fixture.CurrentUser.DeviceId = device.DeviceId;

        await Assert.ThrowsAsync<ValidationException>(() => sender.Send(new UpdateSmsGatewayConsentCommand(device.Id,
            new UpdateSmsGatewayConsentRequest(device.DeviceId, device.SimSlot, token,
                new SmsGatewayConsentScope(null, true, 1, maxPerHour, minIntervalSeconds, 10), false))));
    }

    [Fact]
    public async Task SMS_05_SMS_12_Multipart_sent_callback_counts_segments_once()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.Sms, new SmsSettings { TestMode = false });
        const string token = "host-token";
        var device = Device(context.BranchId, "multipart", 1);
        device.CredentialHash = SmsGatewayCredential.Hash(token);
        device.MonthlyQuota = 20;
        device.SentThisPeriod = 3;
        db.SmsGatewayDevices.Add(device);
        await db.SaveChangesAsync();
        Fixture.CurrentUser.DeviceId = device.DeviceId;
        var job = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.Manual, "+998901234567",
            "Do'kon: uzun", 2, "sms-12", null, default);
        var request = new SmsGatewayLeaseRequest(device.DeviceId, device.SimSlot, token, job!.LeaseToken!);

        await sender.Send(new MarkSmsGatewayJobSentCommand(job.Id, request));
        await sender.Send(new MarkSmsGatewayJobSentCommand(job.Id, request));

        Assert.Equal(5, await db.SmsGatewayDevices.Where(x => x.Id == device.Id).Select(x => x.SentThisPeriod).SingleAsync());
    }

    [Fact]
    public async Task SMS_03_Revoking_consent_requeues_assigned_jobs()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        const string token = "consent-token";
        var device = Device(context.BranchId, "consent", 1);
        device.CredentialHash = SmsGatewayCredential.Hash(token);
        db.SmsGatewayDevices.Add(device);
        await db.SaveChangesAsync();
        Fixture.CurrentUser.DeviceId = device.DeviceId;
        var job = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.Manual, "+998901234567",
            "Do'kon: xabar", 1, "sms-03", null, default);

        await sender.Send(new SetSmsGatewayConsentCommand(device.Id,
            new SetSmsGatewayConsentRequest(device.DeviceId, device.SimSlot, token, false)));

        Assert.Equal(SmsGatewayJobStatus.Pending, job!.Status);
        Assert.Null(job.AssignedDeviceId);
    }

    [Fact]
    public async Task SMS_15_Registration_requires_a_limit_or_explicit_unlimited_choice()
    {
        var validator = new RegisterSmsGatewayCommandValidator();
        var request = new RegisterSmsGatewayRequest(1, "device", "Telefon", "store", 0, "Operator", "sim", null, null, null);

        var result = await validator.ValidateAsync(new RegisterSmsGatewayCommand(request));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task SMS_16_Decrease_applies_without_consent_but_increase_waits_for_consent()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        const string token = "scope-token";
        var device = Device(context.BranchId, "scope", 1);
        device.CredentialHash = SmsGatewayCredential.Hash(token);
        device.MonthlyQuota = 2_000;
        device.ConsentedMonthlyQuota = 2_000;
        device.ConsentedMinIntervalSeconds = 4;
        device.ConsentedMaxPerHour = 60;
        db.SmsGatewayDevices.Add(device);
        await db.SaveChangesAsync();
        Fixture.CurrentUser.DeviceId = device.DeviceId;

        var decrease = await sender.Send(new UpdateSmsGatewayConsentCommand(device.Id,
            new UpdateSmsGatewayConsentRequest(device.DeviceId, device.SimSlot, token,
                new SmsGatewayConsentScope(1_500, false, 1, 60, 4, 10), false)));
        var increase = await sender.Send(new UpdateSmsGatewayConsentCommand(device.Id,
            new UpdateSmsGatewayConsentRequest(device.DeviceId, device.SimSlot, token,
                new SmsGatewayConsentScope(2_500, false, 1, 60, 4, 10), false)));

        Assert.False(decrease.RequiresConsent);
        Assert.True(increase.RequiresConsent);
        Assert.Equal(1_500, await db.SmsGatewayDevices.Where(x => x.Id == device.Id).Select(x => x.MonthlyQuota).SingleAsync());
    }

    [Fact]
    public async Task SMS_18_Lowering_limit_below_usage_blocks_new_job_without_changing_sent_jobs()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        const string token = "lower-token";
        var device = Device(context.BranchId, "lower", 1);
        device.CredentialHash = SmsGatewayCredential.Hash(token);
        device.MonthlyQuota = 2_000;
        device.ConsentedMonthlyQuota = 2_000;
        device.SentThisPeriod = 1_600;
        var sent = new SmsGatewayJob
        {
            BranchId = context.BranchId,
            Kind = SmsGatewayJobKind.Manual,
            Phone = "+998901234567",
            Text = "Do'kon: oldingi",
            Status = SmsGatewayJobStatus.Sent,
            AssignedDevice = device,
            SegmentCount = 1,
            IdempotencyKey = "sms-18-old",
            SentAt = DateTime.UtcNow
        };
        db.AddRange(device, sent);
        await db.SaveChangesAsync();
        Fixture.CurrentUser.DeviceId = device.DeviceId;

        await sender.Send(new UpdateSmsGatewayConsentCommand(device.Id,
            new UpdateSmsGatewayConsentRequest(device.DeviceId, device.SimSlot, token,
                new SmsGatewayConsentScope(1_500, false, 1, 60, 4, 10), false)));
        var created = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.Manual, "+998909999999",
            "Do'kon: yangi", 1, "sms-18-new", null, default);

        Assert.NotNull(created);
        Assert.Equal(SmsGatewayJobStatus.Pending, created.Status);
        Assert.Equal("quota_exhausted", created.WaitingReason);
        Assert.Equal(SmsGatewayJobStatus.Sent, await db.SmsGatewayJobs.Where(x => x.Id == sent.Id).Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task SMS_19_Paused_device_is_skipped_and_consent_is_preserved()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        const string token = "pause-token";
        var device = Device(context.BranchId, "paused", 1);
        device.CredentialHash = SmsGatewayCredential.Hash(token);
        db.SmsGatewayDevices.Add(device);
        await db.SaveChangesAsync();
        Fixture.CurrentUser.DeviceId = device.DeviceId;

        await sender.Send(new SetSmsGatewayPauseCommand(device.Id,
            new SetSmsGatewayPauseRequest(device.DeviceId, device.SimSlot, token, true)));
        var job = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.Manual, "+998901234567",
            "Do'kon: pauza", 1, "sms-19", null, default);

        Assert.Null(job!.AssignedDeviceId);
        Assert.True(await db.SmsGatewayDevices.Where(x => x.Id == device.Id).Select(x => x.IsConsented).SingleAsync());
    }

    [Fact]
    public async Task SMS_18_Low_quota_warning_is_sent_once_per_period()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        const string token = "warning-token";
        await settings.SetAsync(SettingKeys.Telegram, new TelegramSettings { Enabled = true, ChatId = "owner" });
        await settings.SetAsync(SettingKeys.Sms, new SmsSettings { TestMode = false });
        var device = Device(context.BranchId, "warning", 1);
        device.CredentialHash = SmsGatewayCredential.Hash(token);
        device.MonthlyQuota = 10;
        device.ConsentedMonthlyQuota = 10;
        device.SentThisPeriod = 8;
        device.LowQuotaWarnPercent = 20;
        db.SmsGatewayDevices.Add(device);
        await db.SaveChangesAsync();
        Fixture.CurrentUser.DeviceId = device.DeviceId;
        var job = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.Manual, "+998901234567",
            "Do'kon: ogohlantirish", 1, "sms-warning", null, default);
        var lease = new SmsGatewayLeaseRequest(device.DeviceId, device.SimSlot, token, job!.LeaseToken!);

        await sender.Send(new MarkSmsGatewayJobSentCommand(job.Id, lease));
        await sender.Send(new MarkSmsGatewayJobSentCommand(job.Id, lease));

        Assert.Single(Fixture.Notifications.Messages, x => x.Template == "sms_quota_low");
    }

    [Fact]
    public async Task SMS_20_Test_mode_simulates_unlisted_number_without_using_quota()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        const string token = "simulation-token";
        await settings.SetAsync(SettingKeys.Sms, new SmsSettings { TestMode = true });
        var device = Device(context.BranchId, "simulation", 1);
        device.CredentialHash = SmsGatewayCredential.Hash(token);
        device.MonthlyQuota = 10;
        db.SmsGatewayDevices.Add(device);
        await db.SaveChangesAsync();
        Fixture.CurrentUser.DeviceId = device.DeviceId;
        var job = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.Manual, "+998901234567",
            "Do'kon: sinov", 1, "sms-20-simulated", null, default);

        var assigned = await sender.Send(new GetAssignedSmsGatewayJobsQuery(device.DeviceId, device.SimSlot, token));
        Assert.True(Assert.Single(assigned).Simulate);
        var lease = new SmsGatewayLeaseRequest(device.DeviceId, device.SimSlot, token, job!.LeaseToken!);
        await sender.Send(new MarkSmsGatewayJobSimulatedCommand(job.Id, lease));
        await sender.Send(new MarkSmsGatewayJobSimulatedCommand(job.Id, lease));

        Assert.Equal(SmsGatewayJobStatus.Simulated, await db.SmsGatewayJobs.Where(x => x.Id == job.Id).Select(x => x.Status).SingleAsync());
        Assert.Equal(0, await db.SmsGatewayDevices.Where(x => x.Id == device.Id).Select(x => x.SentThisPeriod).SingleAsync());
    }

    [Fact]
    public async Task SMS_20_Allowed_test_number_is_sent_and_uses_quota()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        const string token = "allowed-token";
        const string phone = "+998901234567";
        await settings.SetAsync(SettingKeys.Sms, new SmsSettings { TestMode = true, TestAllowedNumbers = [phone] });
        var device = Device(context.BranchId, "allowed", 1);
        device.CredentialHash = SmsGatewayCredential.Hash(token);
        device.MonthlyQuota = 10;
        db.SmsGatewayDevices.Add(device);
        await db.SaveChangesAsync();
        Fixture.CurrentUser.DeviceId = device.DeviceId;
        var job = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.Manual, phone,
            "Do'kon: haqiqiy sinov", 1, "sms-20-allowed", null, default);

        var assigned = await sender.Send(new GetAssignedSmsGatewayJobsQuery(device.DeviceId, device.SimSlot, token));
        Assert.False(Assert.Single(assigned).Simulate);
        await sender.Send(new MarkSmsGatewayJobSentCommand(job!.Id,
            new SmsGatewayLeaseRequest(device.DeviceId, device.SimSlot, token, job.LeaseToken!)));

        Assert.Equal(1, await db.SmsGatewayDevices.Where(x => x.Id == device.Id).Select(x => x.SentThisPeriod).SingleAsync());
    }

    [Fact]
    public async Task SMS_21_Two_slots_are_independent_and_revoking_one_keeps_the_other_active()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        const string deviceId = "dual-sim";
        const string firstToken = "dual-first";
        var first = Device(context.BranchId, deviceId, 1, simSlot: 0);
        first.CredentialHash = SmsGatewayCredential.Hash(firstToken);
        var second = Device(context.BranchId, deviceId, 2, simSlot: 1);
        second.CredentialHash = SmsGatewayCredential.Hash("dual-second");
        db.SmsGatewayDevices.AddRange(first, second);
        await db.SaveChangesAsync();
        Fixture.CurrentUser.DeviceId = deviceId;

        var job = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.Manual, "+998901234567",
            "Do'kon: dual", 1, "sms-21", null, default);
        await sender.Send(new SetSmsGatewayConsentCommand(first.Id,
            new SetSmsGatewayConsentRequest(deviceId, first.SimSlot, firstToken, false)));

        Assert.False(first.IsConsented);
        Assert.True(second.IsConsented);
        Assert.Equal(second.Id, job!.AssignedDeviceId);
        Assert.Equal(2, await db.SmsGatewayDevices.CountAsync(x => x.DeviceId == deviceId));
    }

    [Fact]
    public async Task SMS_22_Sticky_device_is_preferred_when_it_can_send()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        await settings.SetAsync(SettingKeys.Sms, new SmsSettings { QuietHoursEnabled = false });
        var customer = Customer(context.BusinessId);
        var sticky = Device(context.BranchId, "sticky", 100);
        var faster = Device(context.BranchId, "faster", 1);
        db.AddRange(customer, sticky, faster);
        await db.SaveChangesAsync();
        db.CustomerSmsRoutes.Add(new CustomerSmsRoute
        {
            BranchId = context.BranchId,
            CustomerId = customer.Id,
            LastDeviceId = sticky.Id,
            LastSentAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var job = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.DebtReminder, "+998901234567",
            "Do'kon: qarz", 1, "sms-22-sticky", customer.Id, default);

        Assert.Equal(sticky.Id, job!.AssignedDeviceId);
    }

    [Fact]
    public async Task SMS_22_Sticky_wait_expires_then_falls_back_and_zero_wait_falls_back_immediately()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        var routing = scope.ServiceProvider.GetRequiredService<SmsGatewayRoutingService>();
        await settings.SetAsync(SettingKeys.Sms, new SmsSettings
        {
            DebtReminderStickyWaitMinutes = 15,
            QuietHoursEnabled = false
        });
        var customer = Customer(context.BusinessId);
        var sticky = Device(context.BranchId, "sticky-full", 1);
        sticky.MonthlyQuota = 10;
        sticky.SentThisPeriod = 10;
        var fallback = Device(context.BranchId, "sticky-fallback", 2);
        db.AddRange(customer, sticky, fallback);
        await db.SaveChangesAsync();
        db.CustomerSmsRoutes.Add(new CustomerSmsRoute
        {
            BranchId = context.BranchId,
            CustomerId = customer.Id,
            LastDeviceId = sticky.Id,
            LastSentAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var waiting = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.DebtReminder, "+998901234567",
            "Do'kon: kutish", 1, "sms-22-wait", customer.Id, default);
        Assert.Equal(SmsGatewayJobStatus.Pending, waiting!.Status);
        Assert.Equal("sticky_device", waiting.WaitingReason);
        Assert.True(waiting.AvailableAt > waiting.CreatedAt.AddMinutes(14));

        waiting.AvailableAt = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        await routing.AssignAsync(waiting, default);
        Assert.Equal(fallback.Id, waiting.AssignedDeviceId);

        await settings.SetAsync(SettingKeys.Sms, new SmsSettings
        {
            DebtReminderStickyWaitMinutes = 0,
            QuietHoursEnabled = false
        });
        var immediate = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.DebtReminder, "+998901234568",
            "Do'kon: darhol", 1, "sms-22-zero", customer.Id, default);
        Assert.Equal(fallback.Id, immediate!.AssignedDeviceId);
    }

    [Fact]
    public async Task SMS_23_Remaining_quota_ratio_distributes_three_thousand_to_five_hundred_as_six_to_one()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        var large = Device(context.BranchId, "quota-3000", 1);
        large.MonthlyQuota = 3_000;
        var small = Device(context.BranchId, "quota-500", 2);
        small.MonthlyQuota = 500;
        db.AddRange(large, small);
        await db.SaveChangesAsync();
        var counts = new Dictionary<long, int>();

        for (var index = 0; index < 7; index++)
        {
            var job = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.Manual, $"+99890123456{index}",
                "Do'kon: ulush", 1, $"sms-23-{index}", null, default);
            var deviceId = job!.AssignedDeviceId!.Value;
            counts[deviceId] = counts.GetValueOrDefault(deviceId) + 1;
            var device = deviceId == large.Id ? large : small;
            device.SentThisPeriod++;
            job.Status = SmsGatewayJobStatus.Sent;
            job.SentAt = DateTime.UtcNow.AddHours(-2);
            await db.SaveChangesAsync();
        }

        Assert.Equal(6, counts.GetValueOrDefault(large.Id));
        Assert.Equal(1, counts.GetValueOrDefault(small.Id));
    }

    [Fact]
    public async Task SMS_24_Quiet_hours_hold_debt_but_not_receipt()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        var now = DateTime.Now;
        await settings.SetAsync(SettingKeys.Sms, new SmsSettings
        {
            QuietHoursEnabled = true,
            SendWindowStart = now.AddHours(1).ToString("HH:mm"),
            SendWindowEnd = now.AddHours(2).ToString("HH:mm")
        });
        db.SmsGatewayDevices.Add(Device(context.BranchId, "quiet", 1));
        await db.SaveChangesAsync();

        var debt = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.DebtReminder, "+998901234567",
            "Do'kon: qarz", 1, "sms-24-debt", null, default);
        var receipt = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.ReceiptLink, "+998901234568",
            "Do'kon: chek", 1, "sms-24-receipt", null, default);

        Assert.Equal(SmsGatewayJobStatus.Pending, debt!.Status);
        Assert.Equal("quiet_hours", debt.WaitingReason);
        Assert.Equal(SmsGatewayJobStatus.Assigned, receipt!.Status);
    }

    [Fact]
    public async Task SMS_25_Device_job_and_unified_delivery_have_the_same_status()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        const string token = "unified-token";
        await settings.SetAsync(SettingKeys.Sms, new SmsSettings { TestMode = false });
        var device = Device(context.BranchId, "unified", 1);
        device.CredentialHash = SmsGatewayCredential.Hash(token);
        db.SmsGatewayDevices.Add(device);
        await db.SaveChangesAsync();
        Fixture.CurrentUser.DeviceId = device.DeviceId;

        var job = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.Manual, "+998901234567",
            "Do'kon: jurnal", 1, "sms-25", null, default);
        var lease = new SmsGatewayLeaseRequest(device.DeviceId, device.SimSlot, token, job!.LeaseToken!);
        await sender.Send(new MarkSmsGatewayJobSentCommand(job.Id, lease));

        var delivery = await db.NotificationDeliveries.Include(x => x.Attempts)
            .SingleAsync(x => x.Id == job.NotificationDeliveryId);
        Assert.Equal(NotificationDeliveryStatus.Sent, delivery.Status);
        Assert.Equal(NotificationDeliveryStatus.Sent, Assert.Single(delivery.Attempts).Status);
        var unified = await sender.Send(new GetNotificationJournalQuery { Provider = "device" });
        var gateway = await sender.Send(new GetSmsGatewayJournalQuery(context.BranchId));
        Assert.Equal("Sent", Assert.Single(unified, x => x.Id == delivery.Id).Status);
        Assert.Equal(Cartex.Shared.Models.SmsGateway.SmsGatewayJobStatus.Sent,
            Assert.Single(gateway.Jobs, x => x.Id == job.Id).Status);

        await sender.Send(new MarkSmsGatewayJobDeliveredCommand(job.Id, lease));
        Assert.Equal(NotificationDeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(SmsGatewayJobStatus.Delivered, job.Status);
    }

    [Fact]
    public async Task SMS_27_Retry_and_cancel_create_a_new_attempt_and_write_audit()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        var failed = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.Manual, "+998901234567",
            "Do'kon: retry", 1, "sms-27-failed", null, default);
        failed!.Status = SmsGatewayJobStatus.Failed;
        await db.SaveChangesAsync();

        await sender.Send(new RetrySmsGatewayJobsCommand([failed.Id]));
        var retry = await db.SmsGatewayJobs.SingleAsync(x => x.RetryOfJobId == failed.Id);
        Assert.Equal(failed.NotificationDeliveryId, retry.NotificationDeliveryId);
        Assert.Equal(SmsGatewayJobStatus.Pending, retry.Status);
        Assert.Equal(2, await db.NotificationDeliveryAttempts.CountAsync(x => x.NotificationDeliveryId == failed.NotificationDeliveryId));

        await sender.Send(new CancelSmsGatewayJobsCommand([retry.Id]));
        Assert.Equal(SmsGatewayJobStatus.Cancelled, retry.Status);
        Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == "sms.gateway_job_retried"));
        Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == "sms.gateway_job_cancelled"));
    }

    /// SMS-33: shlyuz tinglayaptimi degan savolga jonli hub ulanishi javob beradi.
    [Fact]
    public async Task SMS_33_A_gateway_that_is_not_connected_is_not_assigned()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<SmsGatewayService>();
        var device = Device(context.BranchId, "asleep", 1);
        db.SmsGatewayDevices.Add(device);
        await db.SaveChangesAsync();
        Fixture.HubPresence.SmsGatewayOffline(device.DeviceId, device.SimSlot);

        var job = await service.CreateAsync(context.BranchId, SmsGatewayJobKind.Manual, "+998901234567",
            "Do'kon: xabar", 1, "sms-33", null, default);

        Assert.NotNull(job);
        Assert.Equal(SmsGatewayJobStatus.Pending, job.Status);
        Assert.Null(job.AssignedDeviceId);
        Assert.Equal("no_device", job.WaitingReason);
    }

    private async Task<(long BranchId, long BusinessId)> SeedContextAsync()
    {
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var branchId = await db.Branches.Where(x => x.Name == "Asosiy filial").Select(x => x.Id).SingleAsync();
        var businessId = await db.Businesses.Select(x => x.Id).SingleAsync();
        var adminId = await db.Users.Where(x => x.Username == "admin").Select(x => x.Id).SingleAsync();
        Fixture.CurrentUser.AsAdmin(adminId, businessId, branchId);
        return (branchId, businessId);
    }

    private static Customer Customer(long businessId) => new()
    {
        Party = new Party { BusinessId = businessId, FullName = "Mijoz" }
    };

    /// Telefon qayta o'rnatilganda lokal credential yo'qoladi. Agar server yozuvni
    /// abadiy qulflab qo'ysa, SIM shlyuzini faqat bazaga kirib tiklash mumkin bo'lardi.
    [Fact]
    public async Task SMS_21_Device_can_be_registered_again_after_losing_its_credential()
    {
        var context = await SeedContextAsync();
        using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var device = Device(context.BranchId, "reinstalled", 1);
        device.MonthlyQuota = 2_000;
        db.SmsGatewayDevices.Add(device);
        await db.SaveChangesAsync();
        Fixture.CurrentUser.DeviceId = device.DeviceId;

        var result = await sender.Send(new RegisterSmsGatewayCommand(new RegisterSmsGatewayRequest(
            context.BranchId, device.DeviceId, device.DeviceName, "store", device.SimSlot,
            "Operator", device.SimSubscriptionId!, "+998900000000", null,
            new SmsGatewayConsentScope(500, false, 1, 60, 4, 10))));

        var stored = await db.SmsGatewayDevices.AsNoTracking().SingleAsync(x => x.Id == device.Id);
        Assert.False(string.IsNullOrWhiteSpace(result.HostToken));
        Assert.Equal(500, stored.MonthlyQuota);
        Assert.True(stored.IsConsented);
        Assert.False(stored.IsTrusted);
    }

    private static SmsGatewayDevice Device(long branchId, string deviceId, int priority, bool consented = true, int simSlot = 0) => new()
    {
        BranchId = branchId,
        DeviceId = deviceId,
        CredentialHash = SmsGatewayCredential.Hash("token"),
        DeviceName = deviceId,
        Client = "store",
        SimSlot = simSlot,
        SimOperator = "Operator",
        SimSubscriptionId = deviceId,
        IsTrusted = true,
        IsConsented = consented,
        IsEnabled = true,
        Priority = priority,
        LastSeenAt = DateTime.UtcNow,
        PeriodStartedAt = DateTime.UtcNow
    };
}
