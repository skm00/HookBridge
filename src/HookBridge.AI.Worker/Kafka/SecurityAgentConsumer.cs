using System.Runtime.CompilerServices;
using System.Text.Json;
using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Kafka;

public sealed class SecurityAgentConsumer : ISecurityAgentConsumer
{
    private readonly Func<ConsumerConfig, IConsumer<string, string>> _consumerFactory;
    private readonly ILogger<SecurityAgentConsumer> _logger;
    private readonly AiKafkaOptions _options;

    public SecurityAgentConsumer(IOptions<AiKafkaOptions> options, ILogger<SecurityAgentConsumer> logger, Func<ConsumerConfig, IConsumer<string, string>>? consumerFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _consumerFactory = consumerFactory ?? (config => new ConsumerBuilder<string, string>(config).Build());
    }

    public async IAsyncEnumerable<SecurityAgentRequestDto> ConsumeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SecurityAgentTopic)) yield break;
        using var consumer = _consumerFactory(AiKafkaConfigFactory.CreateConsumerConfig(_options));
        consumer.Subscribe(_options.SecurityAgentTopic);
        _logger.LogInformation("Security agent Kafka consumer started. Topic: {Topic}, ConsumerGroupId: {ConsumerGroupId}", _options.SecurityAgentTopic, _options.ConsumerGroupId);

        while (!cancellationToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? consumeResult;
            try { consumeResult = consumer.Consume(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (ConsumeException ex) { _logger.LogError(ex, "Security agent Kafka consume error. Topic: {Topic}", _options.SecurityAgentTopic); continue; }
            if (consumeResult is null) continue;

            SecurityAgentRequestDto? request;
            try { request = JsonSerializer.Deserialize<SecurityAgentRequestDto>(consumeResult.Message.Value, AiAnalysisProducer.SerializerOptions); }
            catch (JsonException ex) { _logger.LogWarning(ex, "Invalid security agent message skipped. Topic: {Topic}, Key: {Key}", consumeResult.Topic, consumeResult.Message.Key); continue; }
            if (request is null) continue;

            if (!_options.EnableAutoCommit)
            {
                try { consumer.Commit(consumeResult); }
                catch (KafkaException ex) { _logger.LogError(ex, "Security agent Kafka offset commit failed. Topic: {Topic}", consumeResult.Topic); }
            }

            yield return request;
            await Task.Yield();
        }
    }
}
