using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;

namespace HookBridge.AI.Worker.Kafka;

internal static class AiKafkaConfigFactory
{
    public static ProducerConfig CreateProducerConfig(AiKafkaOptions options)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            SecurityProtocol = ParseSecurityProtocol(options),
        };

        ConfigureSasl(config, options);
        return config;
    }

    public static ConsumerConfig CreateConsumerConfig(AiKafkaOptions options)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = options.ConsumerGroupId,
            EnableAutoCommit = options.EnableAutoCommit,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            SecurityProtocol = ParseSecurityProtocol(options),
        };

        ConfigureSasl(config, options);
        return config;
    }

    private static SecurityProtocol ParseSecurityProtocol(AiKafkaOptions options)
    {
        if (!Enum.TryParse<SecurityProtocol>(options.SecurityProtocol, ignoreCase: true, out var securityProtocol))
        {
            throw new ArgumentException($"Unsupported AI Kafka security protocol: '{options.SecurityProtocol}'.", nameof(options));
        }

        return securityProtocol;
    }

    private static void ConfigureSasl(ClientConfig config, AiKafkaOptions options)
    {
        if (config.SecurityProtocol is not (SecurityProtocol.SaslSsl or SecurityProtocol.SaslPlaintext))
        {
            return;
        }

        if (!Enum.TryParse<SaslMechanism>(options.SaslMechanism, ignoreCase: true, out var saslMechanism))
        {
            throw new ArgumentException($"Unsupported AI Kafka SASL mechanism: '{options.SaslMechanism}'.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.SaslUsername) || string.IsNullOrWhiteSpace(options.SaslPassword))
        {
            throw new ArgumentException("AI Kafka SASL username and password must be configured when using SASL security protocols.", nameof(options));
        }

        config.SaslMechanism = saslMechanism;
        config.SaslUsername = options.SaslUsername;
        config.SaslPassword = options.SaslPassword;
    }
}
