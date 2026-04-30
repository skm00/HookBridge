using System.Text.Json.Serialization;

namespace HookBridge.Application.DTOs.Events;

public sealed class EventIngestionRequestDto
{
    public string EventType { get; set; } = string.Empty;

    public string? EventId { get; set; }

    public DateTime? Timestamp { get; set; }

    public object Data { get; set; } = new { };

    [JsonPropertyName("payload")]
    public object Payload
    {
        get => Data;
        set => Data = value;
    }
}
