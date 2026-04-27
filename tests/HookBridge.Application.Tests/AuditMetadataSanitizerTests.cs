using HookBridge.Application.Common;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class AuditMetadataSanitizerTests
{
    [Fact]
    public void Sanitize_RedactsSensitiveFields()
    {
        var metadata = new Dictionary<string, object?>
        {
            ["apiKey"] = "hb_live_secret",
            ["authorizationHeader"] = "Bearer abc",
            ["password"] = "pw",
            ["clientSecret"] = "oauth",
            ["safe"] = "value",
        };

        var sanitized = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(AuditMetadataSanitizer.Sanitize(metadata));

        Assert.Equal("[REDACTED]", sanitized["apiKey"]);
        Assert.Equal("[REDACTED]", sanitized["authorizationHeader"]);
        Assert.Equal("[REDACTED]", sanitized["password"]);
        Assert.Equal("[REDACTED]", sanitized["clientSecret"]);
        Assert.Equal("value", sanitized["safe"]);
    }
}
