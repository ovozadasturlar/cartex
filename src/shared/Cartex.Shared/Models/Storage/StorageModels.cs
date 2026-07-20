namespace Cartex.Shared.Models.Storage;

public record UploadResult(string Key);

public record ImageUrlResult(string Url);

public record ImageFromUrlRequest(string Url);
