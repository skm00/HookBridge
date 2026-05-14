namespace HookBridge.AI.Worker.DTOs;

public sealed class WebhookFieldMappingRecommendationDto
{
    public string SourceJsonPath { get; set; } = string.Empty;
    public string TargetJsonPath { get; set; } = string.Empty;
    public string SourceFieldName { get; set; } = string.Empty;
    public string TargetFieldName { get; set; } = string.Empty;
    public WebhookTransformationType TransformationType { get; set; }
    public string TransformationExpression { get; set; } = string.Empty;
    public string? DefaultValue { get; set; }
    public bool IsRequired { get; set; }
    public double ConfidenceScore { get; set; }
    public string Notes { get; set; } = string.Empty;
}
