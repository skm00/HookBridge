using System.Text.Json;
using Confluent.Kafka;
using HookBridge.Application.Messaging;
using HookBridge.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.Infrastructure.Services.Messaging;

public sealed class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;
    private bool _disposed;

    public KafkaProducer(
        IOptions<KafkaSettings> kafkaOptions,
        ILogger<KafkaProducer> logger,
        Func<ProducerConfig, IProducer<string, string>>? producerFactory = null)
    {
        _logger = logger;
        var settings = kafkaOptions.Value;

        if (string.IsNullOrWhiteSpace(settings.BootstrapServers))
        {
            throw new ArgumentException("Kafka bootstrap servers must be configured.", nameof(kafkaOptions));
        }

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            MessageTimeoutMs = settings.MessageTimeoutMs,
        };

        if (!Enum.TryParse<SecurityProtocol>(settings.SecurityProtocol, ignoreCase: true, out var securityProtocol))
        {
            throw new ArgumentException($"Unsupported Kafka security protocol: '{settings.SecurityProtocol}'.", nameof(kafkaOptions));
        }

        producerConfig.SecurityProtocol = securityProtocol;

        if (securityProtocol == SecurityProtocol.SaslSsl)
        {
            if (string.IsNullOrWhiteSpace(settings.SaslMechanism))
            {
                throw new ArgumentException("Kafka SASL mechanism must be configured when using SaslSsl.", nameof(kafkaOptions));
            }

            if (!Enum.TryParse<SaslMechanism>(settings.SaslMechanism, ignoreCase: true, out var saslMechanism))
            {
                throw new ArgumentException($"Unsupported Kafka SASL mechanism: '{settings.SaslMechanism}'.", nameof(kafkaOptions));
            }

            if (string.IsNullOrWhiteSpace(settings.SaslUsername) || string.IsNullOrWhiteSpace(settings.SaslPassword))
            {
                throw new ArgumentException("Kafka SASL username and password must be configured when using SaslSsl.", nameof(kafkaOptions));
            }

            producerConfig.SaslMechanism = saslMechanism;
            producerConfig.SaslUsername = settings.SaslUsername;
            producerConfig.SaslPassword = settings.SaslPassword;
        }

        _producer = (producerFactory ?? BuildProducer)(producerConfig);
    }

    public async Task ProduceAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(topic))
        {
            throw new ArgumentException("Kafka topic cannot be empty.", nameof(topic));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Kafka key cannot be empty.", nameof(key));
        }

        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        var payload = JsonSerializer.Serialize(message);

        try
        {
            var deliveryResult = await _producer.ProduceAsync(
                topic,
                new Message<string, string>
                {
                    Key = key,
                    Value = payload,
                },
                cancellationToken);

            _logger.LogInformation(
                "Kafka message published. Topic: {Topic}, Key: {Key}, Partition: {Partition}, Offset: {Offset}",
                topic,
                key,
                deliveryResult.Partition,
                deliveryResult.Offset);
        }
        catch (Exception ex)
        {
            var reason = ex is ProduceException<string, string> produceException
                ? produceException.Error.Reason
                : ex.Message;

            _logger.LogError(
                ex,
                "Kafka publish failed. Topic: {Topic}, Key: {Key}, Reason: {Reason}",
                topic,
                key,
                reason);

            throw;
        }
    }

    private static IProducer<string, string> BuildProducer(ProducerConfig producerConfig)
    {
        return new ProducerBuilder<string, string>(producerConfig).Build();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
        _disposed = true;
    }
}
