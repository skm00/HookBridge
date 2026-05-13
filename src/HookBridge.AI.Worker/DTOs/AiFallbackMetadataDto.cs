namespace HookBridge.AI.Worker.DTOs;

/// <summary>
/// Metadata describing whether deterministic AI fallback rules were used.
/// </summary>
public sealed class AiFallbackMetadataDto
{
    public bool UsedFallback { get; set; }

    public AiFallbackReason FallbackReason { get; set; } = AiFallbackReason.None;

    public string FallbackMessage { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
}
