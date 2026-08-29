using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Cartex.Application.Settings.Commands;
using Cartex.Application.Tests.Common;
using Cartex.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cartex.Application.Tests;

[Collection("database")]
public sealed class SmsSettingsInformationArchitectureTests(DatabaseFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task SMS_08_Device_transport_does_not_store_aggregator_fields()
    {
        using var scope = Fixture.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var handler = new UpdateSmsSettingsCommandHandler(
            settings,
            new TestProtector(),
            scope.ServiceProvider.GetRequiredService<IAuditService>());

        await handler.Handle(new UpdateSmsSettingsCommand(
            true,
            "device",
            "aggregator-login",
            "aggregator-password",
            "CARTEX",
            "https://aggregator.example",
            "none",
            0), default);

        var saved = await settings.GetAsync<SmsSettings>(SettingKeys.Sms);

        Assert.NotNull(saved);
        Assert.Null(saved.Login);
        Assert.Null(saved.Password);
        Assert.Null(saved.Sender);
        Assert.Null(saved.BaseUrl);
        Assert.Equal("none", saved.FallbackProvider);
    }

    [Fact]
    public void SMS_08_Fallback_transport_requires_a_positive_wait()
    {
        var validator = new UpdateSmsSettingsCommandValidator();

        var fallback = validator.Validate(new UpdateSmsSettingsCommand(
            true, "device", "login", "password", "CARTEX", null, "eskiz", 0));
        var device = validator.Validate(new UpdateSmsSettingsCommand(
            true, "device", null, null, null, null, "none", 0));
        var aggregator = validator.Validate(new UpdateSmsSettingsCommand(
            true, "eskiz", "login", "password", "CARTEX", null, "none", 0));

        Assert.False(fallback.IsValid);
        Assert.True(device.IsValid);
        Assert.True(aggregator.IsValid);
    }

    private sealed class TestProtector : ISecretProtector
    {
        public string Protect(string plaintext) => $"protected:{plaintext}";
        public string Unprotect(string ciphertext) => ciphertext["protected:".Length..];
    }
}
