namespace HookBridge.AI.Worker.DTOs;

public sealed class SuggestedValidationRuleDto
{
    public string PropertyName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string RuleExpression { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public SuggestedValidationSeverity Severity { get; set; } = SuggestedValidationSeverity.Warning;
    public string Description { get; set; } = string.Empty;
}
