using System.Runtime.CompilerServices;
using System.Text.Json;
using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Kafka;

public sealed class AutoRemediationRecommendationConsumer : IAutoRemediationRecommendationConsumer
{
    private readonly Func<ConsumerConfig, IConsumer<string, string>> _consumerFactory;
    private readonly ILogger<AutoRemediationRecommendationConsumer> _logger;
    private readonly AiKafkaOptions _options;

    public AutoRemediationRecommendationConsumer(IOptions<AiKafkaOptions> options, ILogger<AutoRemediationRecommendationConsumer> logger, Func<ConsumerConfig, IConsumer<string, string>>? consumerFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _consumerFactory = consumerFactory ?? (config => new ConsumerBuilder<string, string>(config).Build());
    }

    public async IAsyncEnumerable<AutoRemediationRecommendationRequestDto> ConsumeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AutoRemediationTopic)) yield break;
        using var consumer = _consumerFactory(AiKafkaConfigFactory.CreateConsumerConfig(_options));
        consumer.Subscribe(_options.AutoRemediationTopic);
        _logger.LogInformation("Auto-remediation recommendation Kafka consumer started. Topic: {Topic}, ConsumerGroupId: {ConsumerGroupId}", _options.AutoRemediationTopic, _options.ConsumerGroupId);

        while (!cancellationToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? consumeResult;
            try { consumeResult = consumer.Consume(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (ConsumeException ex) { _logger.LogError(ex, "Auto-remediation recommendation Kafka consume error. Topic: {Topic}", _options.AutoRemediationTopic); continue; }
            if (consumeResult is null) continue;

            AutoRemediationRecommendationRequestDto? request;
            try { request = JsonSerializer.Deserialize<AutoRemediationRecommendationRequestDto>(consumeResult.Message.Value, AiAnalysisProducer.SerializerOptions); }
            catch (JsonException ex) { _logger.LogWarning(ex, "Invalid auto-remediation recommendation message skipped. Topic: {Topic}, Key: {Key}", consumeResult.Topic, consumeResult.Message.Key); continue; }
            if (request is null) continue;

            if (!_options.EnableAutoCommit)
            {
                try { consumer.Commit(consumeResult); }
                catch (KafkaException ex) { _logger.LogError(ex, "Auto-remediation recommendation Kafka offset commit failed. Topic: {Topic}", consumeResult.Topic); }
            }

            yield return request;
            await Task.Yield();
        }
    }
}
