namespace Cartex.Application.Common.Interfaces;

public record EmailAttachment(byte[] Content, string FileName);

public interface IEmailService
{
    Task<NotificationProviderResult?> SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default, EmailAttachment? attachment = null);
}
