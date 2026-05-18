using System.ComponentModel.DataAnnotations;

namespace HookBridge.Api.DTOs;

public sealed class AdminAiActionApplyRequestDto
{
    [Required]
    public string AppliedBy { get; set; } = string.Empty;

    [MaxLength(1_000)]
    public string? ApplyComment { get; set; }
}
