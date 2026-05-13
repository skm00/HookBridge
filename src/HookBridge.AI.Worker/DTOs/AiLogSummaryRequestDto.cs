namespace HookBridge.AI.Worker.DTOs;

/// <summary>
/// Request context for generating a short, actionable AI log summary.
/// </summary>
public sealed class AiLogSummaryRequestDto
{
    public string EventId { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }

    public string? Source { get; set; }

    public string? Environment { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    public IReadOnlyList<AiLogEntryDto> Logs { get; set; } = Array.Empty<AiLogEntryDto>();
}
