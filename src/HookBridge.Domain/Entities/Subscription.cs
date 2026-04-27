namespace HookBridge.Domain.Entities;

/// <summary>
/// Represents a tenant subscription that receives specific event types via webhook.
/// </summary>
public sealed class Subscription : BaseEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string TargetUrl { get; set; } = string.Empty;

    public List<KeyValueItem> Headers { get; set; } = [];

    public AuthenticationConfig? Authentication { get; set; }

    public RetryPolicy RetryPolicy { get; set; } = new();

    public int TimeoutSeconds { get; set; }

    public bool IsActive { get; set; }

    public DateTime? DisabledAt { get; set; }
}

public sealed class KeyValueItem
{
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public sealed class RetryPolicy
{
    public int MaxAttempts { get; set; }

    public int InitialDelaySeconds { get; set; }

    public string BackoffType { get; set; } = string.Empty;
}

public sealed class AuthenticationConfig
{
    public string Type { get; set; } = string.Empty;

    public BasicAuthConfig? Basic { get; set; }

    public OAuth2ClientCredentialsConfig? OAuth2 { get; set; }

    public ApiKeyHeaderConfig? ApiKeyHeader { get; set; }

    public HmacSignatureConfig? HmacSignature { get; set; }
}

public sealed class BasicAuthConfig
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed class OAuth2ClientCredentialsConfig
{
    public string TokenUrl { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string? Scope { get; set; }
}

public sealed class ApiKeyHeaderConfig
{
    public string HeaderName { get; set; } = string.Empty;

    public string HeaderValue { get; set; } = string.Empty;
}

public sealed class HmacSignatureConfig
{
    public string Secret { get; set; } = string.Empty;

    public string HeaderName { get; set; } = string.Empty;

    public string Algorithm { get; set; } = string.Empty;
}
