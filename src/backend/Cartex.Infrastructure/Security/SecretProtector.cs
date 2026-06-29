using Cartex.Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace Cartex.Infrastructure.Security;

public sealed class SecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Cartex.Secrets.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);
    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
