using System.Runtime.CompilerServices;
using System.Text.Json;
using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Kafka;

public sealed class PayloadSchemaDetectionConsumer : IPayloadSchemaDetectionConsumer
{
    private readonly Func<ConsumerConfig, IConsumer<string, string>> _consumerFactory;
    private readonly ILogger<PayloadSchemaDetectionConsumer> _logger;
    private readonly AiKafkaOptions _options;

    public PayloadSchemaDetectionConsumer(
        IOptions<AiKafkaOptions> options,
        ILogger<PayloadSchemaDetectionConsumer> logger,
        Func<ConsumerConfig, IConsumer<string, string>>? consumerFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _consumerFactory = consumerFactory ?? (config => new ConsumerBuilder<string, string>(config).Build());
    }

    public async IAsyncEnumerable<PayloadSchemaDetectionRequestDto> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.PayloadSchemaDetectionTopic))
        {
            yield break;
        }

        using var consumer = _consumerFactory(AiKafkaConfigFactory.CreateConsumerConfig(_options));
        consumer.Subscribe(_options.PayloadSchemaDetectionTopic);
        _logger.LogInformation(
            "Payload schema detection Kafka consumer started. Topic: {Topic}, ConsumerGroupId: {ConsumerGroupId}",
            _options.PayloadSchemaDetectionTopic,
            _options.ConsumerGroupId);

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
                _logger.LogError(ex, "Payload schema detection Kafka consume error. Topic: {Topic}", _options.PayloadSchemaDetectionTopic);
                continue;
            }

            if (consumeResult is null)
            {
                continue;
            }

            PayloadSchemaDetectionRequestDto? request;
            try
            {
                request = JsonSerializer.Deserialize<PayloadSchemaDetectionRequestDto>(
                    consumeResult.Message.Value,
                    AiAnalysisProducer.SerializerOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid payload schema detection message skipped. Topic: {Topic}, Key: {Key}", consumeResult.Topic, consumeResult.Message.Key);
                continue;
            }

            if (request is null)
            {
                continue;
            }

            if (!_options.EnableAutoCommit)
            {
                try
                {
                    consumer.Commit(consumeResult);
                }
                catch (KafkaException ex)
                {
                    _logger.LogError(ex, "Payload schema detection Kafka offset commit failed. Topic: {Topic}", consumeResult.Topic);
                }
            }

            yield return request;
            await Task.Yield();
        }
    }
}
