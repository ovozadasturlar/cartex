using System.Security.Cryptography;

namespace Cartex.Application.Sales;

public static class ReceiptTokens
{
    public static string Issue() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
