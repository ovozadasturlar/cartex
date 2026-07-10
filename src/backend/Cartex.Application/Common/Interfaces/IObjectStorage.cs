namespace Cartex.Application.Common.Interfaces;

public interface IObjectStorage
{
    Task<string> UploadAsync(Stream content, long length, string contentType, string extension, CancellationToken cancellationToken = default, string? key = null);
    Task<string?> GetUrlAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string>> GetUrlsAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);
    Task<(Stream Content, string ContentType)?> DownloadAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
