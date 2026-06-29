namespace Cartex.Application.Common.Interfaces;

public interface IObjectStorage
{
    Task<string> UploadAsync(Stream content, long length, string contentType, string extension, CancellationToken cancellationToken = default);
    Task<string?> GetUrlAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string>> GetUrlsAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);
}
