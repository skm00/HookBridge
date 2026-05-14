using System.Runtime.CompilerServices;
using System.Text.Json;
using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Kafka;

public sealed class CustomerEndpointRiskScoreConsumer : ICustomerEndpointRiskScoreConsumer
{
    private readonly Func<ConsumerConfig, IConsumer<string, string>> _consumerFactory;
    private readonly ILogger<CustomerEndpointRiskScoreConsumer> _logger;
    private readonly AiKafkaOptions _options;

    public CustomerEndpointRiskScoreConsumer(IOptions<AiKafkaOptions> options, ILogger<CustomerEndpointRiskScoreConsumer> logger, Func<ConsumerConfig, IConsumer<string, string>>? consumerFactory = null)
    {
        _options = options.Value;
        _logger = logger;
        _consumerFactory = consumerFactory ?? (config => new ConsumerBuilder<string, string>(config).Build());
    }

    public async IAsyncEnumerable<CustomerEndpointRiskScoreMessage> ConsumeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.CustomerEndpointRiskScoreTopic)) yield break;

        using var consumer = _consumerFactory(AiKafkaConfigFactory.CreateConsumerConfig(_options));
        consumer.Subscribe(_options.CustomerEndpointRiskScoreTopic);
        _logger.LogInformation("Customer endpoint risk score Kafka consumer started. Topic: {Topic}, ConsumerGroupId: {ConsumerGroupId}", _options.CustomerEndpointRiskScoreTopic, _options.ConsumerGroupId);

        while (!cancellationToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? consumeResult;
            try { consumeResult = consumer.Consume(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (ConsumeException ex) { _logger.LogError(ex, "Customer endpoint risk score Kafka consume error. Topic: {Topic}", _options.CustomerEndpointRiskScoreTopic); continue; }

            if (consumeResult is null) continue;

            CustomerEndpointRiskScoreRequestDto? request;
            try { request = JsonSerializer.Deserialize<CustomerEndpointRiskScoreRequestDto>(consumeResult.Message.Value, AiAnalysisProducer.SerializerOptions); }
            catch (JsonException ex) { _logger.LogWarning(ex, "Invalid customer endpoint risk score message skipped. Topic: {Topic}, Key: {Key}", consumeResult.Topic, consumeResult.Message.Key); continue; }
            if (request is null) continue;

            yield return new CustomerEndpointRiskScoreMessage(request, _ =>
            {
                if (_options.EnableAutoCommit) return Task.CompletedTask;
                try { consumer.Commit(consumeResult); }
                catch (KafkaException ex) { _logger.LogError(ex, "Customer endpoint risk score Kafka offset commit failed. Topic: {Topic}", consumeResult.Topic); }
                return Task.CompletedTask;
            });
            await Task.Yield();
        }
    }
}
