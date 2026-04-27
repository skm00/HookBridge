using System.Text.Json;
using Confluent.Kafka;
using HookBridge.Application.Messaging;
using HookBridge.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.Infrastructure.Services.Messaging;

public sealed class KafkaConsumer : IKafkaConsumer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaConsumer> _logger;
    private readonly Func<ConsumerConfig, IConsumer<string, string>> _consumerFactory;

    public KafkaConsumer(
        IOptions<KafkaSettings> kafkaOptions,
        ILogger<KafkaConsumer> logger,
        Func<ConsumerConfig, IConsumer<string, string>>? consumerFactory = null)
    {
        _settings = kafkaOptions.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_settings.BootstrapServers))
        {
            throw new ArgumentException("Kafka bootstrap servers must be configured.", nameof(kafkaOptions));
        }

        if (!Enum.TryParse<SecurityProtocol>(_settings.SecurityProtocol, ignoreCase: true, out _))
        {
            throw new ArgumentException($"Unsupported Kafka security protocol: '{_settings.SecurityProtocol}'.", nameof(kafkaOptions));
        }

        _consumerFactory = consumerFactory ?? BuildConsumer;
    }

    public async IAsyncEnumerable<T> ConsumeAsync<T>(
        string topic,
        string groupId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            throw new ArgumentException("Kafka topic cannot be empty.", nameof(topic));
        }

        if (string.IsNullOrWhiteSpace(groupId))
        {
            throw new ArgumentException("Kafka groupId cannot be empty.", nameof(groupId));
        }

        using var consumer = _consumerFactory(CreateConsumerConfig(groupId));
        consumer.Subscribe(topic);

        while (!cancellationToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? consumeResult;
            try
            {
                consumeResult = consumer.Consume(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error on topic {Topic}: {Reason}", topic, ex.Error.Reason);
                continue;
            }

            if (consumeResult is null)
            {
                continue;
            }

            T? message;
            try
            {
                message = JsonSerializer.Deserialize<T>(consumeResult.Message.Value, SerializerOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Invalid Kafka message payload. Topic: {Topic}, Key: {Key}, Partition: {Partition}, Offset: {Offset}",
                    consumeResult.Topic,
                    consumeResult.Message.Key,
                    consumeResult.Partition,
                    consumeResult.Offset);
                continue;
            }

            if (message is null)
            {
                _logger.LogWarning(
                    "Kafka message deserialized to null. Topic: {Topic}, Key: {Key}, Partition: {Partition}, Offset: {Offset}",
                    consumeResult.Topic,
                    consumeResult.Message.Key,
                    consumeResult.Partition,
                    consumeResult.Offset);
                continue;
            }

            _logger.LogInformation(
                "Kafka message consumed. Topic: {Topic}, Key: {Key}, Partition: {Partition}, Offset: {Offset}",
                consumeResult.Topic,
                consumeResult.Message.Key,
                consumeResult.Partition,
                consumeResult.Offset);

            if (!_settings.EnableAutoCommit)
            {
                try
                {
                    consumer.Commit(consumeResult);
                    _logger.LogInformation(
                        "Kafka offset commit succeeded. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}",
                        consumeResult.Topic,
                        consumeResult.Partition,
                        consumeResult.Offset);
                }
                catch (KafkaException ex)
                {
                    _logger.LogError(
                        ex,
                        "Kafka offset commit failed. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}",
                        consumeResult.Topic,
                        consumeResult.Partition,
                        consumeResult.Offset);
                }
            }

            yield return message;
            await Task.Yield();
        }
    }

    private ConsumerConfig CreateConsumerConfig(string groupId)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = groupId,
            EnableAutoCommit = _settings.EnableAutoCommit,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        };

        var parsedProtocol = Enum.Parse<SecurityProtocol>(_settings.SecurityProtocol, ignoreCase: true);
        consumerConfig.SecurityProtocol = parsedProtocol;

        if (parsedProtocol == SecurityProtocol.SaslSsl)
        {
            if (!Enum.TryParse<SaslMechanism>(_settings.SaslMechanism, ignoreCase: true, out var saslMechanism))
            {
                throw new ArgumentException($"Unsupported Kafka SASL mechanism: '{_settings.SaslMechanism}'.", nameof(_settings.SaslMechanism));
            }

            if (string.IsNullOrWhiteSpace(_settings.SaslUsername) || string.IsNullOrWhiteSpace(_settings.SaslPassword))
            {
                throw new ArgumentException("Kafka SASL username and password must be configured when using SaslSsl.", nameof(_settings));
            }

            consumerConfig.SaslMechanism = saslMechanism;
            consumerConfig.SaslUsername = _settings.SaslUsername;
            consumerConfig.SaslPassword = _settings.SaslPassword;
        }

        return consumerConfig;
    }

    private static IConsumer<string, string> BuildConsumer(ConsumerConfig consumerConfig)
    {
        return new ConsumerBuilder<string, string>(consumerConfig).Build();
    }
}
