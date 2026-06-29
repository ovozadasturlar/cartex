namespace Cartex.Application.Common.Interfaces;

public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
