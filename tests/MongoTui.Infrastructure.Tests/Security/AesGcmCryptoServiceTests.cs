using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using MongoTui.Infrastructure.Security;

namespace MongoTui.Infrastructure.Tests.Security;

public sealed class AesGcmCryptoServiceTests
{
    private readonly AesGcmCryptoService _sut = new();

    [Fact]
    public void Should_DecryptOriginalPlaintext_When_Roundtrip()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var plaintext = Encoding.UTF8.GetBytes("secret-value");

        var ciphertext = _sut.Encrypt(plaintext, key, out var nonce);
        var decrypted = _sut.Decrypt(ciphertext, nonce, key);

        decrypted.Should().Equal(plaintext);
    }

    [Fact]
    public void Should_ProduceDifferentNonce_When_EncryptingTwice()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var plaintext = Encoding.UTF8.GetBytes("secret-value");

        _sut.Encrypt(plaintext, key, out var firstNonce);
        _sut.Encrypt(plaintext, key, out var secondNonce);

        firstNonce.Should().NotEqual(secondNonce);
    }

    [Fact]
    public void ShouldThrow_CryptographicException_When_TagTampered()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var plaintext = Encoding.UTF8.GetBytes("secret-value");
        var ciphertext = _sut.Encrypt(plaintext, key, out var nonce);
        ciphertext[^1] ^= 0xFF;

        var act = () => _sut.Decrypt(ciphertext, nonce, key);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void ShouldThrow_CryptographicException_When_WrongKey()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var wrongKey = RandomNumberGenerator.GetBytes(32);
        var plaintext = Encoding.UTF8.GetBytes("secret-value");
        var ciphertext = _sut.Encrypt(plaintext, key, out var nonce);

        var act = () => _sut.Decrypt(ciphertext, nonce, wrongKey);

        act.Should().Throw<CryptographicException>();
    }

    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(64)]
    public void ShouldThrow_ArgumentException_When_KeySizeInvalid(int size)
    {
        var key = RandomNumberGenerator.GetBytes(size);
        var plaintext = Encoding.UTF8.GetBytes("secret-value");

        var act = () => _sut.Encrypt(plaintext, key, out _);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ShouldThrow_ArgumentException_When_NonceSizeInvalid()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var ciphertext = RandomNumberGenerator.GetBytes(32);
        var nonce = RandomNumberGenerator.GetBytes(8);

        var act = () => _sut.Decrypt(ciphertext, nonce, key);

        act.Should().Throw<ArgumentException>();
    }
}
