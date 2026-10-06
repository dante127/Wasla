using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Wasla.BuildingBlocks.Application.Abstractions;
using Wasla.BuildingBlocks.Infrastructure.Configuration;

namespace Wasla.BuildingBlocks.Infrastructure.Security;

/// <summary>
/// AES-256-GCM string encryption ("v1." + base64(nonce | tag | ciphertext)). The key comes
/// from Security:EncryptionKey (base64-encoded 32 bytes) and must be supplied per environment.
/// </summary>
public sealed class AesGcmStringEncryptor : IStringEncryptor
{
    private const string VersionPrefix = "v1.";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public AesGcmStringEncryptor(IOptions<SecurityOptions> options)
    {
        var configured = options.Value.EncryptionKey;

        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "Security:EncryptionKey is not configured. Supply a base64-encoded 32-byte key " +
                "via configuration, environment variable or secret store before using encrypted credentials.");
        }

        try
        {
            _key = Convert.FromBase64String(configured);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Security:EncryptionKey must be valid base64.", exception);
        }

        if (_key.Length != 32)
        {
            throw new InvalidOperationException(
                $"Security:EncryptionKey must decode to exactly 32 bytes (got {_key.Length}).");
        }
    }

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var payload = new byte[NonceSize + TagSize + ciphertext.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload, NonceSize);
        ciphertext.CopyTo(payload, NonceSize + TagSize);

        return VersionPrefix + Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedValue)
    {
        ArgumentNullException.ThrowIfNull(protectedValue);

        if (!protectedValue.StartsWith(VersionPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Unsupported protected-value format (expected 'v1.' prefix).");
        }

        byte[] payload;

        try
        {
            payload = Convert.FromBase64String(protectedValue[VersionPrefix.Length..]);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Protected value is not valid base64.", exception);
        }

        if (payload.Length < NonceSize + TagSize)
        {
            throw new InvalidOperationException("Protected value is truncated.");
        }

        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var ciphertext = payload.AsSpan(NonceSize + TagSize);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return System.Text.Encoding.UTF8.GetString(plaintext);
    }
}
