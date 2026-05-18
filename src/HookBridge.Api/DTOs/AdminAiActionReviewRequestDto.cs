using System.ComponentModel.DataAnnotations;

namespace HookBridge.Api.DTOs;

public sealed class AdminAiActionReviewRequestDto
{
    [Required]
    public string ReviewedBy { get; set; } = string.Empty;

    [MaxLength(1_000)]
    public string? ReviewComment { get; set; }
}
