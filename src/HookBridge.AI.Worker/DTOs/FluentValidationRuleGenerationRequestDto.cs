namespace HookBridge.AI.Worker.DTOs;

public sealed class FluentValidationRuleGenerationRequestDto
{
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? EventType { get; set; }
    public string? Source { get; set; }
    public string? CustomerId { get; set; }
    public string RootClassName { get; set; } = string.Empty;
    public string? Namespace { get; set; }
    public object? Payload { get; set; }
    public string? GeneratedDtoCode { get; set; }
    public IReadOnlyList<PayloadFieldInsightDto> DetectedFields { get; set; } = Array.Empty<PayloadFieldInsightDto>();
    public IReadOnlyList<string> RequiredFields { get; set; } = Array.Empty<string>();
    public DateTime ReceivedAtUtc { get; set; }
}
