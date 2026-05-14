using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.CustomerEndpointRiskScoring;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker;

public sealed class CustomerEndpointRiskScoreWorker : BackgroundService
{
    private readonly ICustomerEndpointRiskScoreConsumer _consumer;
    private readonly ICustomerEndpointRiskScoringService _scoringService;
    private readonly ICustomerEndpointRiskScoreRepository _repository;
    private readonly ILogger<CustomerEndpointRiskScoreWorker> _logger;
    private readonly AiKafkaOptions _kafkaOptions;

    public CustomerEndpointRiskScoreWorker(ICustomerEndpointRiskScoreConsumer consumer, ICustomerEndpointRiskScoringService scoringService, ICustomerEndpointRiskScoreRepository repository, ILogger<CustomerEndpointRiskScoreWorker> logger, IOptions<AiKafkaOptions> kafkaOptions)
    {
        _consumer = consumer;
        _scoringService = scoringService;
        _repository = repository;
        _logger = logger;
        _kafkaOptions = kafkaOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_kafkaOptions.CustomerEndpointRiskScoreTopic))
        {
            _logger.LogInformation("Customer endpoint risk score topic is not configured; worker flow is disabled.");
            return;
        }

        await foreach (var message in _consumer.ConsumeAsync(stoppingToken))
        {
            var request = message.Request;
            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["CustomerId"] = request.CustomerId,
                ["SubscriptionId"] = request.SubscriptionId,
                ["EndpointId"] = request.EndpointId
            });

            var response = _scoringService.CalculateRiskScore(request, DateTime.UtcNow);
            await _repository.InsertAsync(CustomerEndpointRiskScoreResult.FromResponse(response), stoppingToken);
            await message.AcknowledgeAsync(stoppingToken);

            _logger.LogInformation("Customer endpoint risk score completed. CustomerId: {CustomerId}, SubscriptionId: {SubscriptionId}, EndpointId: {EndpointId}, RiskScore: {RiskScore}, RiskLevel: {RiskLevel}, HealthStatus: {HealthStatus}, RiskFactorCount: {RiskFactorCount}", response.CustomerId, response.SubscriptionId, response.EndpointId, response.RiskScore, response.RiskLevel, response.HealthStatus, response.RiskFactors.Count);
        }
    }
}
