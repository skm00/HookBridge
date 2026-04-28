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
        const string signature = "sha256=2ea0dc6ffc89f7af67275bcf79e64ecadf93c9f10b23f91688aeefbe020ca329";

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
        const string signature = "sha256=LqDcb/yJ969nJ1vPeeZOyt+TyfELI/kWiK7vvgIMoyk=";

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
