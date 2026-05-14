using System.Runtime.CompilerServices;
using System.Text.Json;
using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Kafka;

public sealed class WebhookFailureAnomalyDetectionConsumer : IWebhookFailureAnomalyDetectionConsumer
{
    private readonly Func<ConsumerConfig, IConsumer<string, string>> _consumerFactory;
    private readonly ILogger<WebhookFailureAnomalyDetectionConsumer> _logger;
    private readonly AiKafkaOptions _options;

    public WebhookFailureAnomalyDetectionConsumer(IOptions<AiKafkaOptions> options, ILogger<WebhookFailureAnomalyDetectionConsumer> logger, Func<ConsumerConfig, IConsumer<string, string>>? consumerFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _consumerFactory = consumerFactory ?? (config => new ConsumerBuilder<string, string>(config).Build());
    }

    public async IAsyncEnumerable<WebhookFailureAnomalyDetectionMessage> ConsumeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookFailureAnomalyDetectionTopic)) yield break;

        using var consumer = _consumerFactory(AiKafkaConfigFactory.CreateConsumerConfig(_options));
        consumer.Subscribe(_options.WebhookFailureAnomalyDetectionTopic);
        _logger.LogInformation("Webhook failure anomaly Kafka consumer started. Topic: {Topic}, ConsumerGroupId: {ConsumerGroupId}", _options.WebhookFailureAnomalyDetectionTopic, _options.ConsumerGroupId);

        while (!cancellationToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? consumeResult;
            try { consumeResult = consumer.Consume(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (ConsumeException ex) { _logger.LogError(ex, "Webhook failure anomaly Kafka consume error. Topic: {Topic}", _options.WebhookFailureAnomalyDetectionTopic); continue; }

            if (consumeResult is null) continue;

            WebhookFailureAnomalyDetectionRequestDto? request;
            try { request = JsonSerializer.Deserialize<WebhookFailureAnomalyDetectionRequestDto>(consumeResult.Message.Value, AiAnalysisProducer.SerializerOptions); }
            catch (JsonException ex) { _logger.LogWarning(ex, "Invalid webhook failure anomaly message skipped. Topic: {Topic}, Key: {Key}", consumeResult.Topic, consumeResult.Message.Key); continue; }
            if (request is null) continue;

            yield return new WebhookFailureAnomalyDetectionMessage(request, _ =>
            {
                if (_options.EnableAutoCommit) return Task.CompletedTask;
                try { consumer.Commit(consumeResult); }
                catch (KafkaException ex) { _logger.LogError(ex, "Webhook failure anomaly Kafka offset commit failed. Topic: {Topic}", consumeResult.Topic); }
                return Task.CompletedTask;
            });
            await Task.Yield();
        }
    }
}
