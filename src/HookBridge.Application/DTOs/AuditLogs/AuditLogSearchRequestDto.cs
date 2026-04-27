using HookBridge.Application.DTOs.Common;

namespace HookBridge.Application.DTOs.AuditLogs;

public sealed class AuditLogSearchRequestDto : PagedRequestDto
{
    public string? TenantId { get; set; }

    public string? UserId { get; set; }

    public string? UserEmail { get; set; }

    public string? Action { get; set; }

    public string? ResourceType { get; set; }

    public string? ResourceId { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }
}
