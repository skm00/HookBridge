using System.Runtime.CompilerServices;
using System.Text.Json;
using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Kafka;

public sealed class DeadLetterAiAnalysisConsumer : IDeadLetterAiAnalysisConsumer
{
    private readonly Func<ConsumerConfig, IConsumer<string, string>> _consumerFactory;
    private readonly ILogger<DeadLetterAiAnalysisConsumer> _logger;
    private readonly AiKafkaOptions _options;

    public DeadLetterAiAnalysisConsumer(IOptions<AiKafkaOptions> options, ILogger<DeadLetterAiAnalysisConsumer> logger, Func<ConsumerConfig, IConsumer<string, string>>? consumerFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _consumerFactory = consumerFactory ?? (config => new ConsumerBuilder<string, string>(config).Build());
    }

    public async IAsyncEnumerable<DeadLetterAiAnalysisRequestDto> ConsumeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.DeadLetterAiAnalysisTopic)) yield break;
        using var consumer = _consumerFactory(AiKafkaConfigFactory.CreateConsumerConfig(_options));
        consumer.Subscribe(_options.DeadLetterAiAnalysisTopic);
        _logger.LogInformation("Dead-letter AI analysis Kafka consumer started. Topic: {Topic}, ConsumerGroupId: {ConsumerGroupId}", _options.DeadLetterAiAnalysisTopic, _options.ConsumerGroupId);

        while (!cancellationToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? consumeResult;
            try { consumeResult = consumer.Consume(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (ConsumeException ex) { _logger.LogError(ex, "Dead-letter AI analysis Kafka consume error. Topic: {Topic}", _options.DeadLetterAiAnalysisTopic); continue; }
            if (consumeResult is null) continue;

            DeadLetterAiAnalysisRequestDto? request;
            try { request = JsonSerializer.Deserialize<DeadLetterAiAnalysisRequestDto>(consumeResult.Message.Value, AiAnalysisProducer.SerializerOptions); }
            catch (JsonException ex) { _logger.LogWarning(ex, "Invalid dead-letter AI analysis message skipped. Topic: {Topic}, Key: {Key}", consumeResult.Topic, consumeResult.Message.Key); continue; }
            if (request is null) continue;

            if (!_options.EnableAutoCommit)
            {
                try { consumer.Commit(consumeResult); }
                catch (KafkaException ex) { _logger.LogError(ex, "Dead-letter AI analysis Kafka offset commit failed. Topic: {Topic}", consumeResult.Topic); }
            }

            yield return request;
            await Task.Yield();
        }
    }
}
