namespace HookBridge.AI.Worker.DTOs;

public sealed class SuggestedDtoPropertyDto
{
    public string PropertyName { get; set; } = string.Empty;
    public string JsonName { get; set; } = string.Empty;
    public string CSharpType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public bool IsRequired { get; set; }
    public string Description { get; set; } = string.Empty;
}
