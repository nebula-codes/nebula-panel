namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Service for encrypting and decrypting sensitive data at rest.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts a plain text string.
    /// </summary>
    /// <param name="plainText">The text to encrypt.</param>
    /// <returns>The encrypted cipher text.</returns>
    string Encrypt(string plainText);

    /// <summary>
    /// Decrypts a cipher text string.
    /// </summary>
    /// <param name="cipherText">The encrypted text to decrypt.</param>
    /// <returns>The decrypted plain text.</returns>
    string Decrypt(string cipherText);
}
