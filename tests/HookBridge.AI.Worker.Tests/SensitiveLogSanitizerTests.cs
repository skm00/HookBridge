using FluentAssertions;
using HookBridge.AI.Worker.Logging;

namespace HookBridge.AI.Worker.Tests;

public sealed class SensitiveLogSanitizerTests
{
    [Theory]
    [InlineData("Authorization", "Bearer secret-token")]
    [InlineData("Cookie", "session=secret")]
    [InlineData("X-API-Key", "api-key-value")]
    [InlineData("accessToken", "token-value")]
    [InlineData("MongoConnectionString", "mongodb://user:password@host/db")]
    [InlineData("Password", "p@ssw0rd")]
    public void MaskIfSensitive_WhenNameIsSensitive_ReturnsMaskedValue(string name, string value)
    {
        SensitiveLogSanitizer.MaskIfSensitive(name, value).Should().Be("[MASKED]");
    }

    [Fact]
    public void MaskIfSensitive_WhenNameIsNotSensitive_ReturnsOriginalValue()
    {
        SensitiveLogSanitizer.MaskIfSensitive("EventId", "evt-123").Should().Be("evt-123");
    }

    [Fact]
    public void MaskSensitiveValues_MasksOnlySensitiveEntries()
    {
        var sanitized = SensitiveLogSanitizer.MaskSensitiveValues(new Dictionary<string, string?>
        {
            ["EventId"] = "evt-123",
            ["Authorization"] = "Bearer secret",
            ["CorrelationId"] = "corr-123",
            ["Cookie"] = "session=secret"
        });

        sanitized["EventId"].Should().Be("evt-123");
        sanitized["CorrelationId"].Should().Be("corr-123");
        sanitized["Authorization"].Should().Be("[MASKED]");
        sanitized["Cookie"].Should().Be("[MASKED]");
    }
}
