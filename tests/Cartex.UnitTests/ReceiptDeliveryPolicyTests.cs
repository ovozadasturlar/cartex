using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Xunit;

namespace Cartex.UnitTests;

public class ReceiptDeliveryPolicyTests
{
    private static NotificationSettings Config(string? url, ReceiptDeliveryFormat tg = ReceiptDeliveryFormat.Auto, ReceiptDeliveryFormat em = ReceiptDeliveryFormat.Auto) =>
        new() { PublicBaseUrl = url, TelegramFormat = tg, EmailFormat = em };

    [Theory]
    [InlineData(NotificationChannel.Telegram)]
    [InlineData(NotificationChannel.Email)]
    public void Auto_with_url_sends_link(NotificationChannel channel)
    {
        Assert.Equal("link", ReceiptDeliveryPolicy.Resolve(channel, Config("https://x.uz")));
    }

    [Theory]
    [InlineData(NotificationChannel.Telegram)]
    [InlineData(NotificationChannel.Email)]
    public void Auto_without_url_falls_back_to_pdf(NotificationChannel channel)
    {
        Assert.Equal("pdf", ReceiptDeliveryPolicy.Resolve(channel, Config(null)));
    }

    [Fact]
    public void Link_without_url_falls_back_to_pdf()
    {
        Assert.Equal("pdf", ReceiptDeliveryPolicy.Resolve(NotificationChannel.Telegram, Config(null, tg: ReceiptDeliveryFormat.Link)));
    }

    [Fact]
    public void Pdf_is_forced_even_with_url()
    {
        Assert.Equal("pdf", ReceiptDeliveryPolicy.Resolve(NotificationChannel.Email, Config("https://x.uz", em: ReceiptDeliveryFormat.Pdf)));
    }

    [Fact]
    public void Sms_is_link_with_url_and_text_without()
    {
        Assert.Equal("link", ReceiptDeliveryPolicy.Resolve(NotificationChannel.Sms, Config("https://x.uz")));
        Assert.Equal("text", ReceiptDeliveryPolicy.Resolve(NotificationChannel.Sms, Config(null)));
    }
}
