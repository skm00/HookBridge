using System.ComponentModel.DataAnnotations;

namespace HookBridge.AI.Worker.DTOs;

public sealed class AiConfidenceScoreResponseDto : IValidatableObject
{
    public double ConfidenceScore { get; set; }
    public AiConfidenceLevel ConfidenceLevel { get; set; } = AiConfidenceLevel.Unknown;
    public string Explanation { get; set; } = string.Empty;
    public IReadOnlyList<AiConfidenceScoreFactorDto> ScoreFactors { get; set; } = Array.Empty<AiConfidenceScoreFactorDto>();
    public DateTime CalculatedAtUtc { get; set; } = DateTime.UtcNow;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ConfidenceScore is < 0 or > 1)
        {
            yield return new ValidationResult("ConfidenceScore must be between 0 and 1.", [nameof(ConfidenceScore)]);
        }

        if (CalculatedAtUtc.Kind != DateTimeKind.Utc)
        {
            yield return new ValidationResult("CalculatedAtUtc must be UTC.", [nameof(CalculatedAtUtc)]);
        }
    }
}
