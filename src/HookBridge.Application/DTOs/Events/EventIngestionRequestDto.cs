namespace HookBridge.Application.DTOs.Events;

public sealed class EventIngestionRequestDto
{
    public string EventType { get; set; } = string.Empty;

    public string EventId { get; set; } = string.Empty;

    public DateTime? Timestamp { get; set; }

    public object Data { get; set; } = new { };
}
