using System.Text.Json.Serialization;

namespace HookBridge.Application.DTOs.Events;

public sealed class EventIngestionRequestDto
{
    public string? EventType { get; set; }

    public string? EventId { get; set; }

    public DateTime? Timestamp { get; set; }

    public object? Data { get; set; }

    [JsonPropertyName("payload")]
    public object Payload
    {
        get => Data;
        set => Data = value;
    }
}
