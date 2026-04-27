namespace HookBridge.Application.DTOs.Events;

public sealed class EventIngestionResponseDto
{
    public string Status { get; set; } = string.Empty;

    public string EventId { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
