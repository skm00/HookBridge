using System.Security.Cryptography;
using System.Text;
using HookBridge.Application.Interfaces;
using HookBridge.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace HookBridge.Infrastructure.Services;

public sealed class SecretEncryptionService(IOptions<EncryptionSettings> encryptionOptions) : ISecretEncryptionService
{
    private const string Prefix = "enc:v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key = BuildKey(encryptionOptions.Value.MasterKey);

    public string Encrypt(string plainText)
    {
        ArgumentNullException.ThrowIfNull(plainText);

        if (IsEncrypted(plainText))
        {
            return plainText;
        }

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintextBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherTextBytes = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Encrypt(nonce, plaintextBytes, cipherTextBytes, tag);

        return string.Create(
            Prefix.Length + Convert.ToBase64String(nonce).Length + Convert.ToBase64String(cipherTextBytes).Length + Convert.ToBase64String(tag).Length + 2,
            (nonce, cipherTextBytes, tag),
            static (destination, state) =>
            {
                var (nonceBytes, cipherBytes, tagBytes) = state;
                var value = $"{Prefix}{Convert.ToBase64String(nonceBytes)}:{Convert.ToBase64String(cipherBytes)}:{Convert.ToBase64String(tagBytes)}";
                value.AsSpan().CopyTo(destination);
            });
    }

    public string Decrypt(string cipherText)
    {
        ArgumentNullException.ThrowIfNull(cipherText);

        if (!IsEncrypted(cipherText))
        {
            return cipherText;
        }

        try
        {
            var parts = cipherText.Split(':', StringSplitOptions.None);
            if (parts.Length != 5 || parts[0] != "enc" || parts[1] != "v1")
            {
                throw new FormatException("Encrypted secret format is invalid.");
            }

            var nonce = Convert.FromBase64String(parts[2]);
            var ciphertextBytes = Convert.FromBase64String(parts[3]);
            var tag = Convert.FromBase64String(parts[4]);

            var plaintextBytes = new byte[ciphertextBytes.Length];

            using var aesGcm = new AesGcm(_key, TagSize);
            aesGcm.Decrypt(nonce, ciphertextBytes, tag, plaintextBytes);

            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            throw new InvalidOperationException("Unable to decrypt secret value.");
        }
    }

    public bool IsEncrypted(string value)
        => !string.IsNullOrWhiteSpace(value)
           && value.StartsWith(Prefix, StringComparison.Ordinal);

    private static byte[] BuildKey(string masterKey)
    {
        if (string.IsNullOrWhiteSpace(masterKey))
        {
            throw new InvalidOperationException("Encryption:MasterKey is required to encrypt/decrypt secrets.");
        }

        var source = Encoding.UTF8.GetBytes(masterKey);
        var keyBytes = new byte[32];

        if (source.Length >= keyBytes.Length)
        {
            Array.Copy(source, keyBytes, keyBytes.Length);
        }
        else
        {
            Array.Copy(source, keyBytes, source.Length);
        }

        return keyBytes;
    }
}
