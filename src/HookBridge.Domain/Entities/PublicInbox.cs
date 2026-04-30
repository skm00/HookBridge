namespace HookBridge.Domain.Entities;

public sealed class PublicInbox : BaseEntity
{
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public int MaxRequests { get; set; } = 50;

    public int RequestCount { get; set; }

    public string CreatedByIp { get; set; } = string.Empty;
}
