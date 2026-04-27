namespace HookBridge.Domain.Entities;

public sealed class Notification : BaseEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? ResourceType { get; set; }

    public string? ResourceId { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }
}
