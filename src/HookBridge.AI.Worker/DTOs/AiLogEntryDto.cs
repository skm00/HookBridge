namespace HookBridge.AI.Worker.DTOs;

/// <summary>
/// Log entry context used for AI-assisted webhook log summarization.
/// </summary>
public sealed class AiLogEntryDto
{
    public DateTime TimestampUtc { get; set; }

    public string Level { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Exception { get; set; }

    public string? ServiceName { get; set; }

    public string? TraceId { get; set; }

    public string? SpanId { get; set; }
}
