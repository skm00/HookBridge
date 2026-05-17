using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class AiSafeModeEvaluationResponseDto : IValidatableObject
{
    public AiSafeModeDecision Decision { get; set; }
    public bool IsAllowed { get; set; }
    public bool RequiresApproval { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? BlockMessage { get; set; }
    public AiActionType ActionType { get; set; }
    public string Environment { get; set; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; set; } = DateTime.UtcNow;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EvaluatedAtUtc.Kind != DateTimeKind.Utc) yield return new ValidationResult("EvaluatedAtUtc must be UTC.", [nameof(EvaluatedAtUtc)]);
    }
}
