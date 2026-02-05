namespace NebulaPanel.Infrastructure.Security;

using Microsoft.AspNetCore.DataProtection;
using NebulaPanel.Domain.Interfaces;

/// <summary>
/// Encryption service using ASP.NET Core Data Protection API.
/// </summary>
public class DataProtectionEncryptionService : IEncryptionService
{
    private readonly IDataProtector _protector;

    public DataProtectionEncryptionService(IDataProtectionProvider provider)
    {
        // Use a purpose string to isolate this protector
        _protector = provider.CreateProtector("NebulaPanel.Credentials");
    }

    /// <inheritdoc />
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return plainText;
        }

        return _protector.Protect(plainText);
    }

    /// <inheritdoc />
    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return cipherText;
        }

        return _protector.Unprotect(cipherText);
    }
}
