namespace HookBridge.Infrastructure.Configuration;

/// <summary>
/// Configures retention windows for data cleanup routines.
/// </summary>
public sealed class DataRetentionSettings
{
    public bool Enabled { get; set; } = true;

    public int IncomingEventsDays { get; set; } = 30;

    public int DeliveryLogsDays { get; set; } = 30;

    public int FailedEventsDays { get; set; } = 90;

    public int AuditLogsDays { get; set; } = 90;

    public int NotificationsDays { get; set; } = 30;
}
