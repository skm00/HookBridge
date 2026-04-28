using System.Security.Cryptography;
using System.Text;
using HookBridge.Application.Interfaces.Security;

namespace HookBridge.Infrastructure.Services;

public sealed class WebhookSignatureValidator : IWebhookSignatureValidator
{
    private const string Prefix = "sha256=";

    public bool Validate(string payload, string signatureHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(payload)
            || string.IsNullOrWhiteSpace(signatureHeader)
            || string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        if (!signatureHeader.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var providedSignature = signatureHeader[Prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(providedSignature))
        {
            return false;
        }

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var secretBytes = Encoding.UTF8.GetBytes(secret);

        using var hmac = new HMACSHA256(secretBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        var expectedHex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        var expectedBase64 = Convert.ToBase64String(hashBytes);

        return FixedTimeEquals(providedSignature, expectedHex)
            || FixedTimeEquals(providedSignature, expectedBase64);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
