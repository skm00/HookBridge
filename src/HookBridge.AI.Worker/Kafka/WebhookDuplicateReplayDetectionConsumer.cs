using System.Runtime.CompilerServices;
using System.Text.Json;
using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Kafka;

public sealed class WebhookDuplicateReplayDetectionConsumer : IWebhookDuplicateReplayDetectionConsumer
{
    private readonly Func<ConsumerConfig, IConsumer<string, string>> _consumerFactory;
    private readonly ILogger<WebhookDuplicateReplayDetectionConsumer> _logger;
    private readonly AiKafkaOptions _options;

    public WebhookDuplicateReplayDetectionConsumer(IOptions<AiKafkaOptions> options, ILogger<WebhookDuplicateReplayDetectionConsumer> logger, Func<ConsumerConfig, IConsumer<string, string>>? consumerFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _consumerFactory = consumerFactory ?? (config => new ConsumerBuilder<string, string>(config).Build());
    }

    public async IAsyncEnumerable<WebhookDuplicateReplayDetectionMessage> ConsumeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.DuplicateReplayDetectionTopic)) yield break;

        using var consumer = _consumerFactory(AiKafkaConfigFactory.CreateConsumerConfig(_options));
        consumer.Subscribe(_options.DuplicateReplayDetectionTopic);
        _logger.LogInformation("Webhook duplicate/replay detection Kafka consumer started. Topic: {Topic}, ConsumerGroupId: {ConsumerGroupId}", _options.DuplicateReplayDetectionTopic, _options.ConsumerGroupId);

        while (!cancellationToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? consumeResult;
            try { consumeResult = consumer.Consume(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (ConsumeException ex) { _logger.LogError(ex, "Webhook duplicate/replay Kafka consume error. Topic: {Topic}", _options.DuplicateReplayDetectionTopic); continue; }

            if (consumeResult is null) continue;
            WebhookDuplicateReplayDetectionRequestDto? request;
            try { request = JsonSerializer.Deserialize<WebhookDuplicateReplayDetectionRequestDto>(consumeResult.Message.Value, AiAnalysisProducer.SerializerOptions); }
            catch (JsonException ex) { _logger.LogWarning(ex, "Invalid webhook duplicate/replay message skipped. Topic: {Topic}, Key: {Key}", consumeResult.Topic, consumeResult.Message.Key); continue; }
            if (request is null) continue;

            yield return new WebhookDuplicateReplayDetectionMessage(request, _ =>
            {
                if (_options.EnableAutoCommit) return Task.CompletedTask;
                try { consumer.Commit(consumeResult); }
                catch (KafkaException ex) { _logger.LogError(ex, "Webhook duplicate/replay Kafka offset commit failed. Topic: {Topic}", consumeResult.Topic); }
                return Task.CompletedTask;
            });
            await Task.Yield();
        }
    }
}
