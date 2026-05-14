using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.JsonToDtoSuggestion;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker;

public sealed class JsonToDtoSuggestionWorker : BackgroundService
{
    private readonly IJsonToDtoSuggestionConsumer _consumer;
    private readonly IJsonToDtoSuggestionAgent _agent;
    private readonly IJsonToDtoSuggestionRepository _repository;
    private readonly ILogger<JsonToDtoSuggestionWorker> _logger;
    private readonly AiKafkaOptions _kafkaOptions;

    public JsonToDtoSuggestionWorker(
        IJsonToDtoSuggestionConsumer consumer,
        IJsonToDtoSuggestionAgent agent,
        IJsonToDtoSuggestionRepository repository,
        ILogger<JsonToDtoSuggestionWorker> logger,
        IOptions<AiKafkaOptions> kafkaOptions)
    {
        _consumer = consumer;
        _agent = agent;
        _repository = repository;
        _logger = logger;
        _kafkaOptions = kafkaOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_kafkaOptions.JsonToDtoSuggestionTopic))
        {
            _logger.LogInformation("JSON-to-DTO suggestion topic is not configured; worker flow is disabled.");
            return;
        }

        await foreach (var request in _consumer.ConsumeAsync(stoppingToken))
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["EventId"] = request.EventId,
                ["CorrelationId"] = request.CorrelationId
            });

            var response = await _agent.SuggestAsync(request, stoppingToken);
            var result = JsonToDtoSuggestionResult.FromResponse(response, request);
            await _repository.InsertAsync(result, stoppingToken);

            _logger.LogInformation(
                "JSON-to-DTO suggestion completed. EventId: {EventId}, CorrelationId: {CorrelationId}, SuggestedRootClassName: {SuggestedRootClassName}, ClassCount: {ClassCount}, ConfidenceScore: {ConfidenceScore}, RiskLevel: {RiskLevel}, FallbackUsed: {FallbackUsed}",
                response.EventId,
                response.CorrelationId,
                response.SuggestedRootClassName,
                response.Classes.Count,
                response.ConfidenceScore,
                response.RiskLevel,
                response.Fallback?.UsedFallback ?? false);
        }
    }
}
