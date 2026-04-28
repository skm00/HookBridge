using HookBridge.Domain.Entities;

namespace HookBridge.Application.Models;

public sealed class TenantBackupPackage
{
    public string Version { get; set; } = "1.0";

    public DateTime ExportedAtUtc { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public Tenant Tenant { get; set; } = new();

    public List<Subscription> Subscriptions { get; set; } = [];

    public List<ApiKey> ApiKeys { get; set; } = [];

    public List<IncomingEvent> Events { get; set; } = [];

    public List<FailedEvent> FailedEvents { get; set; } = [];

    public List<Notification> Notifications { get; set; } = [];

    public List<AuditLog> AuditLogs { get; set; } = [];
}
