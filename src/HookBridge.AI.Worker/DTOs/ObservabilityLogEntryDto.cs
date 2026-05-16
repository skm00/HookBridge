using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class ObservabilityLogEntryDto : IValidatableObject
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string? Level { get; set; }
    public string? Message { get; set; }
    public string? Exception { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? Source { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TimestampUtc.Kind != DateTimeKind.Utc) yield return new ValidationResult("TimestampUtc must be UTC.", [nameof(TimestampUtc)]);
    }
}
