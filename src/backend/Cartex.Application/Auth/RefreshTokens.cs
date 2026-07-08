using System.Security.Cryptography;

namespace Cartex.Application.Auth;

public static class RefreshTokens
{
    public static string Generate() => Base64Url(RandomNumberGenerator.GetBytes(64));

    public static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
