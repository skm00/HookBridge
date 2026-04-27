namespace HookBridge.Infrastructure.Configuration;

/// <summary>
/// Represents Kafka connection and producer/consumer settings.
/// </summary>
public sealed class KafkaSettings
{
    public string BootstrapServers { get; set; } = string.Empty;

    public string SecurityProtocol { get; set; } = string.Empty;

    public string SaslMechanism { get; set; } = string.Empty;

    public string? SaslUsername { get; set; }

    public string? SaslPassword { get; set; }

    public string ConsumerGroupId { get; set; } = string.Empty;

    public bool EnableAutoCommit { get; set; }

    public int MessageTimeoutMs { get; set; }
}
