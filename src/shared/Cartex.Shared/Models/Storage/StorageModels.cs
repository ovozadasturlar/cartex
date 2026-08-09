namespace Cartex.Shared.Models.Storage;

public record UploadResult(string Key);
public record LogoUploadResult(string ColorKey, string MonochromeKey);

public record ImageUrlResult(string Url);

public record ImageFromUrlRequest(string Url);
