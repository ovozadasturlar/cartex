namespace Cartex.Application.Common.Settings;

public sealed class StorageSettings
{
    public bool Enabled { get; set; }
    public string? Endpoint { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string? Bucket { get; set; }
    public bool UseSsl { get; set; }
}
