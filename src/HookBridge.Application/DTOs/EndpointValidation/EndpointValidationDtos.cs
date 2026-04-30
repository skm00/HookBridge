using HookBridge.Application.DTOs.Subscriptions;

namespace HookBridge.Application.DTOs.EndpointValidation;

public sealed class EndpointValidationRequestDto
{
    public string TargetUrl { get; set; } = string.Empty;

    public string Method { get; set; } = "POST";

    public object? SamplePayload { get; set; }

    public List<KeyValueDto>? Headers { get; set; }

    public AuthenticationDto? Authentication { get; set; }

    public int TimeoutSeconds { get; set; } = 10;
}

public sealed class EndpointValidationResponseDto
{
    public bool IsSuccess { get; set; }

    public int? StatusCode { get; set; }

    public string Message { get; set; } = string.Empty;

    public long DurationMs { get; set; }

    public string? ResponseBody { get; set; }
}
