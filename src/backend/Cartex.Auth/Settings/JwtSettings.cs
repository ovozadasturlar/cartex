namespace Cartex.Auth.Settings;

public class JwtSettings
{
    public const string CustomerScheme = "Customer";

    public string Key { get; set; } = null!;
    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;
    public string CustomerAudience { get; set; } = "cartex-customer";
    public int ExpirationMinutes { get; set; } = 480;
}
