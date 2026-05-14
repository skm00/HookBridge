namespace HookBridge.AI.Worker.DTOs;

public sealed class PayloadFieldInsightDto
{
    public string FieldName { get; set; } = string.Empty;
    public string JsonPath { get; set; } = string.Empty;
    public string InferredType { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? SampleValue { get; set; }
    public string Description { get; set; } = string.Empty;
}
