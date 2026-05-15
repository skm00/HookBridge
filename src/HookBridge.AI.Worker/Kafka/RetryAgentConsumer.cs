using System.Runtime.CompilerServices;
using System.Text.Json;
using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Kafka;

public sealed class RetryAgentConsumer : IRetryAgentConsumer
{
    private readonly Func<ConsumerConfig, IConsumer<string, string>> _consumerFactory;
    private readonly ILogger<RetryAgentConsumer> _logger;
    private readonly AiKafkaOptions _options;

    public RetryAgentConsumer(IOptions<AiKafkaOptions> options, ILogger<RetryAgentConsumer> logger, Func<ConsumerConfig, IConsumer<string, string>>? consumerFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _consumerFactory = consumerFactory ?? (config => new ConsumerBuilder<string, string>(config).Build());
    }

    public async IAsyncEnumerable<RetryAgentRequestDto> ConsumeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.RetryAgentTopic)) yield break;
        using var consumer = _consumerFactory(AiKafkaConfigFactory.CreateConsumerConfig(_options));
        consumer.Subscribe(_options.RetryAgentTopic);
        _logger.LogInformation("Retry agent Kafka consumer started. Topic: {Topic}, ConsumerGroupId: {ConsumerGroupId}", _options.RetryAgentTopic, _options.ConsumerGroupId);

        while (!cancellationToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? consumeResult;
            try { consumeResult = consumer.Consume(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (ConsumeException ex) { _logger.LogError(ex, "Retry agent Kafka consume error. Topic: {Topic}", _options.RetryAgentTopic); continue; }
            if (consumeResult is null) continue;

            RetryAgentRequestDto? request;
            try { request = JsonSerializer.Deserialize<RetryAgentRequestDto>(consumeResult.Message.Value, AiAnalysisProducer.SerializerOptions); }
            catch (JsonException ex) { _logger.LogWarning(ex, "Invalid retry agent message skipped. Topic: {Topic}, Key: {Key}", consumeResult.Topic, consumeResult.Message.Key); continue; }
            if (request is null) continue;

            if (!_options.EnableAutoCommit)
            {
                try { consumer.Commit(consumeResult); }
                catch (KafkaException ex) { _logger.LogError(ex, "Retry agent Kafka offset commit failed. Topic: {Topic}", consumeResult.Topic); }
            }

            yield return request;
            await Task.Yield();
        }
    }
}
