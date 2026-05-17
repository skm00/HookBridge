using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class AiSafeModeEvaluationRequestDto : IValidatableObject
{
    public AiActionType ActionType { get; set; } = AiActionType.Unknown;
    public string Environment { get; set; } = string.Empty;
    public string? EventId { get; set; }
    public string? CorrelationId { get; set; }
    public string? CustomerId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? RiskLevel { get; set; }
    public double? ConfidenceScore { get; set; }
    public string? ApprovalId { get; set; }
    public AiRecommendationApprovalStatus? ApprovalStatus { get; set; }
    public string? RequestedBy { get; set; }
    public string? Reason { get; set; }
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ActionType == AiActionType.Unknown) yield return new ValidationResult("ActionType is required.", [nameof(ActionType)]);
        if (string.IsNullOrWhiteSpace(Environment)) yield return new ValidationResult("Environment is required.", [nameof(Environment)]);
        if (RequestedAtUtc.Kind != DateTimeKind.Utc) yield return new ValidationResult("RequestedAtUtc must be UTC.", [nameof(RequestedAtUtc)]);
        if (ConfidenceScore is < 0 or > 1) yield return new ValidationResult("ConfidenceScore must be between 0 and 1.", [nameof(ConfidenceScore)]);
    }
}
