namespace HookBridge.Domain.Entities;

public sealed class PublicInboxRequest : BaseEntity
{
    public string InboxToken { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;

    public Dictionary<string, string> Headers { get; set; } = new();

    public string Body { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; }
}
