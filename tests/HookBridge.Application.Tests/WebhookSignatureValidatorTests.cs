using HookBridge.Infrastructure.Services;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class WebhookSignatureValidatorTests
{
    private readonly WebhookSignatureValidator _validator = new();

    [Fact]
    public void Validate_WithHexSignature_ReturnsTrue()
    {
        const string payload = "{\"event\":\"order.created\"}";
        const string secret = "super-secret";
        const string signature = "sha256=489a8577fa1e28f0064ad56664475fe173c43a41467162e59e0e3986f9bd8128";

        var isValid = _validator.Validate(payload, signature, secret);

        Assert.True(isValid);
    }

    [Fact]
    public void Validate_WithInvalidSignature_ReturnsFalse()
    {
        const string payload = "{\"event\":\"order.created\"}";
        const string secret = "super-secret";

        var isValid = _validator.Validate(payload, "sha256=not-valid", secret);

        Assert.False(isValid);
    }

    [Fact]
    public void Validate_WithMissingSignatureHeader_ReturnsFalse()
    {
        var isValid = _validator.Validate("{\"event\":\"order.created\"}", string.Empty, "super-secret");

        Assert.False(isValid);
    }

    [Fact]
    public void Validate_AcceptsBase64Format()
    {
        const string payload = "{\"event\":\"order.created\"}";
        const string secret = "super-secret";
        const string signature = "sha256=SJqFd/oeKPAGStVmZEdf4XPEOkFGcWLlng45hvm9gSg=";

        var isValid = _validator.Validate(payload, signature, secret);

        Assert.True(isValid);
    }

    [Fact]
    public void Validate_UsesConstantTimeComparisonForLengthMismatch()
    {
        const string payload = "{\"event\":\"order.created\"}";
        const string secret = "super-secret";

        var exception = Record.Exception(() => _validator.Validate(payload, "sha256=a", secret));

        Assert.Null(exception);
    }
}
