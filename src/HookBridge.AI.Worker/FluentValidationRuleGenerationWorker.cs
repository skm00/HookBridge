using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.FluentValidationRuleGeneration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker;

public sealed class FluentValidationRuleGenerationWorker : BackgroundService
{
    private readonly IFluentValidationRuleGenerationConsumer _consumer;
    private readonly IFluentValidationRuleGenerationAgent _agent;
    private readonly IFluentValidationRuleGenerationRepository _repository;
    private readonly ILogger<FluentValidationRuleGenerationWorker> _logger;
    private readonly AiKafkaOptions _kafkaOptions;

    public FluentValidationRuleGenerationWorker(IFluentValidationRuleGenerationConsumer consumer, IFluentValidationRuleGenerationAgent agent, IFluentValidationRuleGenerationRepository repository, ILogger<FluentValidationRuleGenerationWorker> logger, IOptions<AiKafkaOptions> kafkaOptions)
    {
        _consumer = consumer;
        _agent = agent;
        _repository = repository;
        _logger = logger;
        _kafkaOptions = kafkaOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_kafkaOptions.FluentValidationRuleGenerationTopic))
        {
            _logger.LogInformation("FluentValidation rule generation topic is not configured; worker flow is disabled.");
            return;
        }

        await foreach (var message in _consumer.ConsumeAsync(stoppingToken))
        {
            var request = message.Request;
            using var scope = _logger.BeginScope(new Dictionary<string, object?> { ["EventId"] = request.EventId, ["CorrelationId"] = request.CorrelationId });
            var response = await _agent.GenerateAsync(request, stoppingToken);
            await _repository.InsertAsync(FluentValidationRuleGenerationResult.FromResponse(response, request), stoppingToken);
            await message.AcknowledgeAsync(stoppingToken);
            _logger.LogInformation(
                "FluentValidation rule generation completed. EventId: {EventId}, CorrelationId: {CorrelationId}, ValidatorClassName: {ValidatorClassName}, RuleCount: {RuleCount}, ConfidenceScore: {ConfidenceScore}, RiskLevel: {RiskLevel}, FallbackUsed: {FallbackUsed}",
                response.EventId,
                response.CorrelationId,
                response.ValidatorClassName,
                response.Rules.Count,
                response.ConfidenceScore,
                response.RiskLevel,
                response.Fallback?.UsedFallback ?? false);
        }
    }
}
