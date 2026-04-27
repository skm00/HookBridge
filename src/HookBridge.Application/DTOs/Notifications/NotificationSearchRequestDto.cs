using HookBridge.Application.DTOs.Common;

namespace HookBridge.Application.DTOs.Notifications;

public sealed class NotificationSearchRequestDto : PagedRequestDto
{
    public string? TenantId { get; set; }
    public string? Type { get; set; }
    public string? Severity { get; set; }
    public bool? IsRead { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
