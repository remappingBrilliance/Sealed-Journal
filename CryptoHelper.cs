using System;
using System.Security.Cryptography;
using System.Text;

namespace SealedJournal;

/// <summary>
/// Provides AES-256 encryption and decryption with PBKDF2 key derivation.
/// Each encryption operation uses a unique random salt and IV.
/// </summary>
public static class CryptoHelper
{
    private const int KeySize = 256;
    private const int IvSize = 16;
    private const int SaltSize = 32;
    private const int Iterations = 100000;

    /// <summary>
    /// Encrypts plain text using AES-256 with PBKDF2 key derivation.
    /// </summary>
    /// <param name="plainText">The text to encrypt.</param>
    /// <param name="password">The password used for encryption.</param>
    /// <returns>Base64-encoded string containing salt, IV, and ciphertext.</returns>
    public static string Encrypt(string plainText, string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] iv = RandomNumberGenerator.GetBytes(IvSize);

        using (var aes = Aes.Create())
        {
            aes.KeySize = KeySize;
            aes.Key = DeriveKey(password, salt, aes.KeySize / 8);
            aes.IV = iv;

            using (var encryptor = aes.CreateEncryptor())
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                byte[] result = new byte[salt.Length + iv.Length + encryptedBytes.Length];
                Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
                Buffer.BlockCopy(iv, 0, result, salt.Length, iv.Length);
                Buffer.BlockCopy(encryptedBytes, 0, result, salt.Length + iv.Length, encryptedBytes.Length);

                return Convert.ToBase64String(result);
            }
        }
    }

    /// <summary>
    /// Decrypts a Base64-encoded encrypted string.
    /// </summary>
    /// <param name="encryptedText">The encrypted text as Base64 string.</param>
    /// <param name="password">The password used for decryption.</param>
    /// <returns>The decrypted plain text.</returns>
    /// <exception cref="FormatException">Thrown when the encrypted text is not valid Base64.</exception>
    /// <exception cref="CryptographicException">Thrown when decryption fails (wrong password or corrupted data).</exception>
    public static string Decrypt(string encryptedText, string password)
    {
        byte[] fullBytes = Convert.FromBase64String(encryptedText);

        if (fullBytes.Length < SaltSize + IvSize + 1)
        {
            throw new CryptographicException("Encrypted data is too short.");
        }

        byte[] salt = new byte[SaltSize];
        byte[] iv = new byte[IvSize];
        byte[] encryptedBytes = new byte[fullBytes.Length - SaltSize - IvSize];

        Buffer.BlockCopy(fullBytes, 0, salt, 0, SaltSize);
        Buffer.BlockCopy(fullBytes, SaltSize, iv, 0, IvSize);
        Buffer.BlockCopy(fullBytes, SaltSize + IvSize, encryptedBytes, 0, encryptedBytes.Length);

        using (var aes = Aes.Create())
        {
            aes.KeySize = KeySize;
            aes.Key = DeriveKey(password, salt, aes.KeySize / 8);
            aes.IV = iv;

            using (var decryptor = aes.CreateDecryptor())
            {
                byte[] plainBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                return Encoding.UTF8.GetString(plainBytes);
            }
        }
    }

    /// <summary>
    /// Derives an encryption key from a password using PBKDF2.
    /// </summary>
    /// <param name="password">The password to derive from.</param>
    /// <param name="salt">The cryptographic salt.</param>
    /// <param name="keySize">The desired key size in bytes.</param>
    /// <returns>The derived key.</returns>
    private static byte[] DeriveKey(string password, byte[] salt, int keySize)
    {
        using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
        {
            return pbkdf2.GetBytes(keySize);
        }
    }
}