using System.Net;
using System.Net.Mail;
using Cartex.Application.Common.Interfaces;
using Cartex.Application.Common.Settings;
using Microsoft.Extensions.Logging;

namespace Cartex.Infrastructure.Notifications.Email;

public sealed class EmailService(
    ISettingsService settings,
    ISecretProtector protector,
    ILogger<EmailService> logger) : IEmailService
{
    public async Task<NotificationProviderResult?> SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default, EmailAttachment? attachment = null)
    {
        var cfg = await settings.GetAsync<EmailSettings>(SettingKeys.Email, cancellationToken);
        if (cfg is null || !cfg.Enabled || string.IsNullOrWhiteSpace(cfg.Host) || string.IsNullOrWhiteSpace(cfg.FromAddress) || string.IsNullOrWhiteSpace(to))
        {
            logger.LogInformation("Email not configured; skipped");
            return null;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(cfg.FromAddress, cfg.FromName ?? cfg.FromAddress),
            Subject = subject,
            Body = body
        };
        message.To.Add(to);
        if (attachment is not null)
            message.Attachments.Add(new Attachment(new MemoryStream(attachment.Content), attachment.FileName));

        using var client = new SmtpClient(cfg.Host, cfg.Port) { EnableSsl = cfg.UseSsl };
        if (!string.IsNullOrWhiteSpace(cfg.Username))
            client.Credentials = new NetworkCredential(cfg.Username, string.IsNullOrWhiteSpace(cfg.Password) ? "" : protector.Unprotect(cfg.Password));

        await client.SendMailAsync(message, cancellationToken);
        return new NotificationProviderResult(cfg.Host);
    }
}
