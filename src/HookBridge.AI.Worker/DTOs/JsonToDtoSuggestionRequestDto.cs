namespace HookBridge.AI.Worker.DTOs;

public sealed class JsonToDtoSuggestionRequestDto
{
    public string EventId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? EventType { get; set; }
    public string? Source { get; set; }
    public string? CustomerId { get; set; }
    public string? RootClassName { get; set; }
    public string? Namespace { get; set; }
    public object? Payload { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
}
