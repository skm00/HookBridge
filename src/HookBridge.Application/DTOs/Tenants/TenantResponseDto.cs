using HookBridge.Domain.Enums;

namespace HookBridge.Application.DTOs.Tenants;

/// <summary>
/// Tenant response payload.
/// </summary>
public sealed class TenantResponseDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public TenantStatus Status { get; set; }

    public string? ContactEmail { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
