namespace HookBridge.AI.Worker.DTOs;

public sealed class SuggestedDtoClassDto
{
    public string ClassName { get; set; } = string.Empty;
    public IReadOnlyList<SuggestedDtoPropertyDto> Properties { get; set; } = Array.Empty<SuggestedDtoPropertyDto>();
    public string Description { get; set; } = string.Empty;
}
