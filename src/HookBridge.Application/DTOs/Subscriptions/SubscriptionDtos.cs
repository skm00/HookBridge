using HookBridge.Application.DTOs.Common;

namespace HookBridge.Application.DTOs.Subscriptions;

public sealed class CreateSubscriptionRequestDto
{
    public string TenantId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string TargetUrl { get; set; } = string.Empty;

    public List<KeyValueDto> Headers { get; set; } = [];

    public AuthenticationDto? Authentication { get; set; }

    public RetryPolicyDto? RetryPolicy { get; set; }

    public int? TimeoutSeconds { get; set; }
}

public sealed class UpdateSubscriptionRequestDto
{
    public string? EventType { get; set; }

    public string? TargetUrl { get; set; }

    public List<KeyValueDto>? Headers { get; set; }

    public AuthenticationDto? Authentication { get; set; }

    public RetryPolicyDto? RetryPolicy { get; set; }

    public int? TimeoutSeconds { get; set; }
}

public sealed class SubscriptionResponseDto
{
    public string Id { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string TargetUrl { get; set; } = string.Empty;

    public List<KeyValueDto> Headers { get; set; } = [];

    public AuthenticationDto? Authentication { get; set; }

    public RetryPolicyDto RetryPolicy { get; set; } = new();

    public int TimeoutSeconds { get; set; }

    public bool IsActive { get; set; }

    public DateTime? DisabledAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public sealed class SubscriptionSearchRequestDto : PagedRequestDto
{
    public string? TenantId { get; set; }

    public string? EventType { get; set; }

    public string? TargetUrl { get; set; }

    public bool? IsActive { get; set; }
}

public sealed class KeyValueDto
{
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public sealed class RetryPolicyDto
{
    public int MaxAttempts { get; set; }

    public int InitialDelaySeconds { get; set; }

    public string BackoffType { get; set; } = string.Empty;
}

public sealed class AuthenticationDto
{
    public string Type { get; set; } = "None";

    public BasicAuthDto? Basic { get; set; }

    public OAuth2ClientCredentialsDto? OAuth2 { get; set; }

    public ApiKeyHeaderDto? ApiKeyHeader { get; set; }

    public HmacSignatureDto? HmacSignature { get; set; }
}

public sealed class BasicAuthDto
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed class OAuth2ClientCredentialsDto
{
    public string TokenUrl { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string? Scope { get; set; }
}

public sealed class ApiKeyHeaderDto
{
    public string HeaderName { get; set; } = string.Empty;

    public string HeaderValue { get; set; } = string.Empty;
}

public sealed class HmacSignatureDto
{
    public string Secret { get; set; } = string.Empty;

    public string HeaderName { get; set; } = string.Empty;

    public string Algorithm { get; set; } = string.Empty;
}
