using System.Runtime.CompilerServices;
using System.Text.Json;
using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Kafka;

public sealed class FluentValidationRuleGenerationConsumer : IFluentValidationRuleGenerationConsumer
{
    private readonly Func<ConsumerConfig, IConsumer<string, string>> _consumerFactory;
    private readonly ILogger<FluentValidationRuleGenerationConsumer> _logger;
    private readonly AiKafkaOptions _options;

    public FluentValidationRuleGenerationConsumer(IOptions<AiKafkaOptions> options, ILogger<FluentValidationRuleGenerationConsumer> logger, Func<ConsumerConfig, IConsumer<string, string>>? consumerFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _consumerFactory = consumerFactory ?? (config => new ConsumerBuilder<string, string>(config).Build());
    }

    public async IAsyncEnumerable<FluentValidationRuleGenerationMessage> ConsumeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FluentValidationRuleGenerationTopic)) yield break;
        using var consumer = _consumerFactory(AiKafkaConfigFactory.CreateConsumerConfig(_options));
        consumer.Subscribe(_options.FluentValidationRuleGenerationTopic);
        _logger.LogInformation("FluentValidation rule generation Kafka consumer started. Topic: {Topic}, ConsumerGroupId: {ConsumerGroupId}", _options.FluentValidationRuleGenerationTopic, _options.ConsumerGroupId);
        while (!cancellationToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result;
            try { result = consumer.Consume(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (ConsumeException ex) { _logger.LogError(ex, "FluentValidation rule generation Kafka consume error. Topic: {Topic}", _options.FluentValidationRuleGenerationTopic); continue; }
            if (result is null) continue;
            FluentValidationRuleGenerationRequestDto? request;
            try { request = JsonSerializer.Deserialize<FluentValidationRuleGenerationRequestDto>(result.Message.Value, AiAnalysisProducer.SerializerOptions); }
            catch (JsonException ex) { _logger.LogWarning(ex, "Invalid FluentValidation rule generation message skipped. Topic: {Topic}, Key: {Key}", result.Topic, result.Message.Key); continue; }
            if (request is null) continue;
            yield return new FluentValidationRuleGenerationMessage(request, _ =>
            {
                if (_options.EnableAutoCommit) return Task.CompletedTask;
                try { consumer.Commit(result); }
                catch (KafkaException ex) { _logger.LogError(ex, "FluentValidation rule generation Kafka offset commit failed. Topic: {Topic}", result.Topic); }
                return Task.CompletedTask;
            });
            await Task.Yield();
        }
    }
}
