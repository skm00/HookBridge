using System.ComponentModel.DataAnnotations;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Services.Confidence;

public sealed class AiConfidenceScoreService : IAiConfidenceScoreService
{
    private const double ValidJsonBonus = 0.10;
    private const double RequiredFieldsBonus = 0.05;
    private const double StrongEvidenceBonus = 0.05;
    private const double UnknownRiskPenalty = 0.10;
    private const double RuleBasedEvidenceBonus = 0.05;
    private readonly AiConfidenceScoreOptions _options;
    private readonly ILogger<AiConfidenceScoreService> _logger;

    public AiConfidenceScoreService(IOptions<AiConfidenceScoreOptions> options, ILogger<AiConfidenceScoreService> logger)
    {
        _options = options.Value;
        ValidateOptions(_options);
        _logger = logger;
    }

    public AiConfidenceScoreResponseDto Calculate(AiConfidenceScoreRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);

        var factors = new List<AiConfidenceScoreFactorDto>
        {
            new() { FactorName = "BaseScore", Impact = _options.BaseScore, Description = "Deterministic base confidence score." }
        };

        var score = _options.Enabled ? _options.BaseScore : 0;
        AddIf(request.UsedAi && request.LlmResponseWasValidJson, "ValidAiJson", ValidJsonBonus, "AI output was valid JSON.");
        AddIf(request.LlmResponseHadRequiredFields, "RequiredFields", RequiredFieldsBonus, "Required decision fields were present.");
        AddIf(request.EvidenceCount >= 3, "StrongEvidence", StrongEvidenceBonus, "At least three evidence signals supported the decision.");
        AddIf(request.UsedFallback, "FallbackPenalty", -_options.FallbackPenalty, "Fallback path reduced confidence.");
        AddPenalty("MissingDataPenalty", request.MissingDataCount, _options.MissingDataPenalty, 0.20, "Missing input data reduced confidence.");
        AddPenalty("ValidationIssuePenalty", request.ValidationIssueCount, _options.ValidationIssuePenalty, 0.20, "Validation issues reduced confidence.");
        AddPenalty("FailedAgentPenalty", request.FailedAgentCount, _options.FailedAgentPenalty, 0.30, "Failed agent executions reduced confidence.");
        AddIf(request.RiskLevel == AiRiskLevel.Unknown, "UnknownRiskPenalty", -UnknownRiskPenalty, "Unknown risk level reduced confidence.");
        AddIf(request.IsRuleBased && request.EvidenceCount > 0, "RuleBasedEvidence", RuleBasedEvidenceBonus, "Rule-based decision had supporting evidence.");

        var unclamped = score;
        var clamped = Math.Clamp(Math.Round(score, 4), 0, 1);
        if (Math.Abs(unclamped - clamped) > 0.0001)
        {
            _logger.LogWarning("Confidence score clamped. DecisionType: {DecisionType}, AgentName: {AgentName}, UnclampedScore: {UnclampedScore}, ClampedScore: {ClampedScore}", request.DecisionType, request.AgentName, Math.Round(unclamped, 4), clamped);
        }

        var level = MapConfidenceLevel(clamped);
        if (clamped < _options.LowConfidenceReviewThreshold)
        {
            _logger.LogWarning("Low confidence decision detected. DecisionType: {DecisionType}, AgentName: {AgentName}, ConfidenceScore: {ConfidenceScore}, ConfidenceLevel: {ConfidenceLevel}", request.DecisionType, request.AgentName, clamped, level);
        }

        _logger.LogInformation("Confidence score calculated. DecisionType: {DecisionType}, AgentName: {AgentName}, ConfidenceScore: {ConfidenceScore}, ConfidenceLevel: {ConfidenceLevel}", request.DecisionType, request.AgentName, clamped, level);

        return new AiConfidenceScoreResponseDto
        {
            ConfidenceScore = clamped,
            ConfidenceLevel = level,
            Explanation = BuildExplanation(factors, clamped, level),
            ScoreFactors = factors,
            CalculatedAtUtc = DateTime.UtcNow
        };

        void AddIf(bool condition, string name, double impact, string description)
        {
            if (!condition) return;
            score += impact;
            factors.Add(new AiConfidenceScoreFactorDto { FactorName = name, Impact = impact, Description = description });
        }

        void AddPenalty(string name, int count, double perItem, double max, string description)
        {
            if (count <= 0) return;
            AddIf(true, name, -Math.Min(count * perItem, max), description);
        }
    }

    public AiConfidenceLevel MapConfidenceLevel(double confidenceScore)
    {
        if (double.IsNaN(confidenceScore) || confidenceScore is < 0 or > 1) return AiConfidenceLevel.Unknown;
        return confidenceScore switch
        {
            < 0.40 => AiConfidenceLevel.Low,
            < 0.70 => AiConfidenceLevel.Medium,
            < 0.90 => AiConfidenceLevel.High,
            _ => AiConfidenceLevel.VeryHigh
        };
    }

    public bool RequiresManualReview(double confidenceScore, AiRiskLevel riskLevel)
    {
        var requires = riskLevel is AiRiskLevel.High or AiRiskLevel.Critical || confidenceScore < _options.LowConfidenceReviewThreshold;
        if (requires && confidenceScore < _options.LowConfidenceReviewThreshold)
        {
            _logger.LogWarning("Manual review required due to low confidence. ConfidenceScore: {ConfidenceScore}, RiskLevel: {RiskLevel}", confidenceScore, riskLevel);
        }
        return requires;
    }

    public bool RequiresNeedsMoreInfoOrManualReview(double confidenceScore) => confidenceScore < _options.VeryLowConfidenceReviewThreshold;

    private static string BuildExplanation(IReadOnlyList<AiConfidenceScoreFactorDto> factors, double score, AiConfidenceLevel level)
    {
        var positives = factors.Where(f => f.Impact > 0 && f.FactorName != "BaseScore").Select(f => f.Description).ToList();
        var negatives = factors.Where(f => f.Impact < 0).Select(f => f.Description).ToList();
        var parts = new List<string>();
        if (positives.Count > 0) parts.Add(string.Join(" ", positives));
        if (negatives.Count > 0) parts.Add(string.Join(" ", negatives));
        if (parts.Count == 0) parts.Add("Only the deterministic base score applied.");
        parts.Add($"Final confidence is {score:0.00} ({level}).");
        return string.Join(" ", parts);
    }

    private static void ValidateOptions(AiConfidenceScoreOptions options)
    {
        if (options.BaseScore is < 0 or > 1) throw new OptionsValidationException(nameof(AiConfidenceScoreOptions), typeof(AiConfidenceScoreOptions), ["BaseScore must be between 0 and 1."]);
        if (options.FallbackPenalty is < 0 or > 1 || options.MissingDataPenalty is < 0 or > 1 || options.ValidationIssuePenalty is < 0 or > 1 || options.FailedAgentPenalty is < 0 or > 1)
        {
            throw new OptionsValidationException(nameof(AiConfidenceScoreOptions), typeof(AiConfidenceScoreOptions), ["Penalties must be between 0 and 1."]);
        }
    }
}
