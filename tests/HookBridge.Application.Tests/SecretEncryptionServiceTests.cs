using HookBridge.Infrastructure.Configuration;
using HookBridge.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace HookBridge.Application.Tests;

public sealed class SecretEncryptionServiceTests
{
    [Fact]
    public void Encrypt_ProducesVersionedEncryptedValue()
    {
        var service = CreateService();

        var encrypted = service.Encrypt("plain-secret");

        Assert.StartsWith("enc:v1:", encrypted, StringComparison.Ordinal);
    }

    [Fact]
    public void Decrypt_ReturnsOriginalPlainText()
    {
        var service = CreateService();
        var encrypted = service.Encrypt("plain-secret");

        var decrypted = service.Decrypt(encrypted);

        Assert.Equal("plain-secret", decrypted);
    }

    [Fact]
    public void Encrypt_UsesDifferentNonceEachTime()
    {
        var service = CreateService();

        var first = service.Encrypt("plain-secret");
        var second = service.Encrypt("plain-secret");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void IsEncrypted_DetectsEncryptedValues()
    {
        var service = CreateService();
        var encrypted = service.Encrypt("plain-secret");

        Assert.True(service.IsEncrypted(encrypted));
        Assert.False(service.IsEncrypted("plain-secret"));
    }

    private static SecretEncryptionService CreateService()
        => new(Options.Create(new EncryptionSettings
        {
            MasterKey = "12345678901234567890123456789012",
        }));
}
