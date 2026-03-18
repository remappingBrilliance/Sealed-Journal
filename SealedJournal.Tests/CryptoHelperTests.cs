using System;
using System.Security.Cryptography;
using Xunit;

namespace SealedJournal;

public class CryptoHelperTests
{
    [Fact]
    public void Encrypt_WithValidInput_ReturnsBase64String()
    {
        // Arrange
        string plainText = "Hello, World!";
        string password = "TestPassword123";

        // Act
        string encrypted = CryptoHelper.Encrypt(plainText, password);

        // Assert
        Assert.NotNull(encrypted);
        Assert.NotEmpty(encrypted);
        
        // Should be valid Base64
        byte[] data = Convert.FromBase64String(encrypted);
        Assert.True(data.Length > 0);
    }

    [Fact]
    public void Encrypt_WithSameInput_ProducesDifferentOutput()
    {
        // Arrange
        string plainText = "Hello, World!";
        string password = "TestPassword123";

        // Act
        string encrypted1 = CryptoHelper.Encrypt(plainText, password);
        string encrypted2 = CryptoHelper.Encrypt(plainText, password);

        // Assert
        Assert.NotEqual(encrypted1, encrypted2);
    }

    [Fact]
    public void Decrypt_WithCorrectPassword_ReturnsOriginalText()
    {
        // Arrange
        string plainText = "Hello, World!";
        string password = "TestPassword123";
        string encrypted = CryptoHelper.Encrypt(plainText, password);

        // Act
        string decrypted = CryptoHelper.Decrypt(encrypted, password);

        // Assert
        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void Decrypt_WithWrongPassword_ThrowsCryptographicException()
    {
        // Arrange
        string plainText = "Hello, World!";
        string correctPassword = "CorrectPassword123";
        string wrongPassword = "WrongPassword456";
        string encrypted = CryptoHelper.Encrypt(plainText, correctPassword);

        // Act & Assert
        Assert.Throws<CryptographicException>(() => CryptoHelper.Decrypt(encrypted, wrongPassword));
    }

    [Fact]
    public void Decrypt_WithInvalidBase64_ThrowsFormatException()
    {
        // Arrange
        string invalidBase64 = "This is not valid base64!@#$";

        // Act & Assert
        Assert.Throws<FormatException>(() => CryptoHelper.Decrypt(invalidBase64, "password"));
    }

    [Fact]
    public void Decrypt_WithTooShortData_ThrowsCryptographicException()
    {
        // Arrange
        // Create a valid Base64 string that's too short (less than salt + iv + 1 byte)
        byte[] shortData = new byte[40]; // Salt(32) + IV(16) = 48 minimum, this is only 40
        RandomNumberGenerator.Fill(shortData);
        string tooShortBase64 = Convert.ToBase64String(shortData);

        // Act & Assert
        Assert.Throws<CryptographicException>(() => CryptoHelper.Decrypt(tooShortBase64, "password"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("The quick brown fox jumps over the lazy dog.")]
    [InlineData("Special characters: !@#$%^&*()_+-=[]{}|;':\",./<>?")]
    [InlineData("Unicode: 你好世界 🌍 مرحبا")]
    [InlineData("Newlines:\nLine1\r\nLine2")]
    public void EncryptDecrypt_WithVariousInputs_RoundTripsCorrectly(string plainText)
    {
        // Arrange
        string password = "TestPassword123";

        // Act
        string encrypted = CryptoHelper.Encrypt(plainText, password);
        string decrypted = CryptoHelper.Decrypt(encrypted, password);

        // Assert
        Assert.Equal(plainText, decrypted);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("P@ssw0rd!")]
    [InlineData("   spaces   ")]
    [InlineData("a")]
    [InlineData("A very long password that might be used by someone who is paranoid about security")]
    public void EncryptDecrypt_WithVariousPasswords_RoundTripsCorrectly(string password)
    {
        // Arrange
        string plainText = "Test content";

        // Act
        string encrypted = CryptoHelper.Encrypt(plainText, password);
        string decrypted = CryptoHelper.Decrypt(encrypted, password);

        // Assert
        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void Encrypt_WithEmptyPlainText_Works()
    {
        // Arrange
        string plainText = "";
        string password = "TestPassword123";

        // Act
        string encrypted = CryptoHelper.Encrypt(plainText, password);
        string decrypted = CryptoHelper.Decrypt(encrypted, password);

        // Assert
        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void Encrypt_ProducesExpectedMinimumLength()
    {
        // Arrange
        string plainText = "A";
        string password = "password";

        // Act
        string encrypted = CryptoHelper.Encrypt(plainText, password);
        byte[] data = Convert.FromBase64String(encrypted);

        // Assert
        // Salt (32) + IV (16) + at least 1 encrypted byte = 49 minimum
        Assert.True(data.Length >= 49, $"Expected at least 49 bytes, got {data.Length}");
    }
}