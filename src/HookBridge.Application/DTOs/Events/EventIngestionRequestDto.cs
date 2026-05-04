using System.Text.Json.Serialization;

namespace HookBridge.Application.DTOs.Events;

public sealed class EventIngestionRequestDto
{
    public string? EventType { get; set; }

    public string? EventId { get; set; }

    public DateTime? Timestamp { get; set; }

    public object? Data { get; set; }
    public string ContentMode { get; set; } = "Raw";
    public string? Source { get; set; }
    public string? SpecVersion { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public string RawBody { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public object Payload
    {
        get => Data;
        set => Data = value;
    }
}
