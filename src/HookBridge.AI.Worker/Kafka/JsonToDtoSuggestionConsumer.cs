using System.Runtime.CompilerServices;
using System.Text.Json;
using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Kafka;

public sealed class JsonToDtoSuggestionConsumer : IJsonToDtoSuggestionConsumer
{
    private readonly Func<ConsumerConfig, IConsumer<string, string>> _consumerFactory;
    private readonly ILogger<JsonToDtoSuggestionConsumer> _logger;
    private readonly AiKafkaOptions _options;

    public JsonToDtoSuggestionConsumer(
        IOptions<AiKafkaOptions> options,
        ILogger<JsonToDtoSuggestionConsumer> logger,
        Func<ConsumerConfig, IConsumer<string, string>>? consumerFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _consumerFactory = consumerFactory ?? (config => new ConsumerBuilder<string, string>(config).Build());
    }

    public async IAsyncEnumerable<JsonToDtoSuggestionMessage> ConsumeAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.JsonToDtoSuggestionTopic))
        {
            yield break;
        }

        using var consumer = _consumerFactory(AiKafkaConfigFactory.CreateConsumerConfig(_options));
        consumer.Subscribe(_options.JsonToDtoSuggestionTopic);
        _logger.LogInformation(
            "JSON-to-DTO suggestion Kafka consumer started. Topic: {Topic}, ConsumerGroupId: {ConsumerGroupId}",
            _options.JsonToDtoSuggestionTopic,
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
                _logger.LogError(ex, "JSON-to-DTO suggestion Kafka consume error. Topic: {Topic}", _options.JsonToDtoSuggestionTopic);
                continue;
            }

            if (consumeResult is null)
            {
                continue;
            }

            JsonToDtoSuggestionRequestDto? request;
            try
            {
                request = JsonSerializer.Deserialize<JsonToDtoSuggestionRequestDto>(
                    consumeResult.Message.Value,
                    AiAnalysisProducer.SerializerOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Invalid JSON-to-DTO suggestion message skipped. Topic: {Topic}, Key: {Key}", consumeResult.Topic, consumeResult.Message.Key);
                continue;
            }

            if (request is null)
            {
                continue;
            }

            yield return new JsonToDtoSuggestionMessage(
                request,
                _ =>
                {
                    if (_options.EnableAutoCommit)
                    {
                        return Task.CompletedTask;
                    }

                    try
                    {
                        consumer.Commit(consumeResult);
                    }
                    catch (KafkaException ex)
                    {
                        _logger.LogError(ex, "JSON-to-DTO suggestion Kafka offset commit failed. Topic: {Topic}", consumeResult.Topic);
                    }

                    return Task.CompletedTask;
                });
            await Task.Yield();
        }
    }
}
