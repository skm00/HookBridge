using System.Runtime.CompilerServices;
using System.Text.Json;
using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Kafka;

public sealed class WebhookTransformationRecommendationConsumer : IWebhookTransformationRecommendationConsumer
{
    private readonly Func<ConsumerConfig, IConsumer<string, string>> _consumerFactory;
    private readonly ILogger<WebhookTransformationRecommendationConsumer> _logger;
    private readonly AiKafkaOptions _options;

    public WebhookTransformationRecommendationConsumer(IOptions<AiKafkaOptions> options, ILogger<WebhookTransformationRecommendationConsumer> logger, Func<ConsumerConfig, IConsumer<string, string>>? consumerFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _consumerFactory = consumerFactory ?? (config => new ConsumerBuilder<string, string>(config).Build());
    }

    public async IAsyncEnumerable<WebhookTransformationRecommendationMessage> ConsumeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookTransformationRecommendationTopic)) yield break;
        using var consumer = _consumerFactory(AiKafkaConfigFactory.CreateConsumerConfig(_options));
        consumer.Subscribe(_options.WebhookTransformationRecommendationTopic);
        _logger.LogInformation("Webhook transformation recommendation Kafka consumer started. Topic: {Topic}, ConsumerGroupId: {ConsumerGroupId}", _options.WebhookTransformationRecommendationTopic, _options.ConsumerGroupId);
        while (!cancellationToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result;
            try { result = consumer.Consume(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (ConsumeException ex) { _logger.LogError(ex, "Webhook transformation recommendation Kafka consume error. Topic: {Topic}", _options.WebhookTransformationRecommendationTopic); continue; }
            if (result is null) continue;
            WebhookTransformationRecommendationRequestDto? request;
            try { request = JsonSerializer.Deserialize<WebhookTransformationRecommendationRequestDto>(result.Message.Value, AiAnalysisProducer.SerializerOptions); }
            catch (JsonException ex) { _logger.LogWarning(ex, "Invalid webhook transformation recommendation message skipped. Topic: {Topic}, Key: {Key}", result.Topic, result.Message.Key); continue; }
            if (request is null) continue;
            yield return new WebhookTransformationRecommendationMessage(request, _ =>
            {
                if (_options.EnableAutoCommit) return Task.CompletedTask;
                try { consumer.Commit(result); }
                catch (KafkaException ex) { _logger.LogError(ex, "Webhook transformation recommendation Kafka offset commit failed. Topic: {Topic}", result.Topic); }
                return Task.CompletedTask;
            });
            await Task.Yield();
        }
    }
}
