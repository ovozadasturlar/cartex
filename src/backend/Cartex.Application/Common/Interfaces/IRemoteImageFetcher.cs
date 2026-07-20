namespace Cartex.Application.Common.Interfaces;

public record RemoteImage(byte[] Content, string ContentType, string Extension);

public interface IRemoteImageFetcher
{
    Task<RemoteImage?> FetchAsync(string url, CancellationToken cancellationToken = default);
}
