using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiMongoIndexInitializer : IHostedService
{
    private readonly IAiAnalysisResultCollectionProvider _collectionProvider;
    private readonly ICustomerEndpointRiskScoreCollectionProvider? _riskScoreCollectionProvider;
    private readonly IWebhookFailureAnomalyDetectionCollectionProvider? _failureAnomalyCollectionProvider;
    private readonly ILogger<AiMongoIndexInitializer> _logger;

    public AiMongoIndexInitializer(
        IAiAnalysisResultCollectionProvider collectionProvider,
        ILogger<AiMongoIndexInitializer> logger)
        : this(collectionProvider, null, null, logger)
    {
    }

    public AiMongoIndexInitializer(
        IAiAnalysisResultCollectionProvider collectionProvider,
        ICustomerEndpointRiskScoreCollectionProvider? riskScoreCollectionProvider,
        ILogger<AiMongoIndexInitializer> logger)
        : this(collectionProvider, riskScoreCollectionProvider, null, logger)
    {
    }

    public AiMongoIndexInitializer(
        IAiAnalysisResultCollectionProvider collectionProvider,
        ICustomerEndpointRiskScoreCollectionProvider? riskScoreCollectionProvider,
        IWebhookFailureAnomalyDetectionCollectionProvider? failureAnomalyCollectionProvider,
        ILogger<AiMongoIndexInitializer> logger)
    {
        _collectionProvider = collectionProvider;
        _riskScoreCollectionProvider = riskScoreCollectionProvider;
        _failureAnomalyCollectionProvider = failureAnomalyCollectionProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var collection = _collectionProvider.GetCollection();
        var indexModels = CreateIndexModels();

        await collection.Indexes.CreateManyAsync(indexModels, cancellationToken);

        if (_riskScoreCollectionProvider is not null)
        {
            var riskScoreCollection = _riskScoreCollectionProvider.GetCollection();
            await riskScoreCollection.Indexes.CreateManyAsync(CreateCustomerEndpointRiskScoreIndexModels(), cancellationToken);
        }

        if (_failureAnomalyCollectionProvider is not null)
        {
            var failureAnomalyCollection = _failureAnomalyCollectionProvider.GetCollection();
            await failureAnomalyCollection.Indexes.CreateManyAsync(CreateWebhookFailureAnomalyDetectionIndexModels(), cancellationToken);
        }

        _logger.LogInformation("MongoDB AI analysis result indexes are ready.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public static IReadOnlyList<CreateIndexModel<AiAnalysisResult>> CreateIndexModels()
    {
        return new[]
        {
            new CreateIndexModel<AiAnalysisResult>(
                Builders<AiAnalysisResult>.IndexKeys.Ascending(result => result.EventId),
                new CreateIndexOptions { Name = "idx_ai_analysis_results_event_id" }),
            new CreateIndexModel<AiAnalysisResult>(
                Builders<AiAnalysisResult>.IndexKeys.Ascending(result => result.CorrelationId),
                new CreateIndexOptions { Name = "idx_ai_analysis_results_correlation_id" }),
            new CreateIndexModel<AiAnalysisResult>(
                Builders<AiAnalysisResult>.IndexKeys.Descending(result => result.CreatedAtUtc),
                new CreateIndexOptions { Name = "idx_ai_analysis_results_created_at_utc_desc" })
        };
    }

    public static IReadOnlyList<CreateIndexModel<CustomerEndpointRiskScoreResult>> CreateCustomerEndpointRiskScoreIndexModels()
    {
        return new[]
        {
            new CreateIndexModel<CustomerEndpointRiskScoreResult>(Builders<CustomerEndpointRiskScoreResult>.IndexKeys.Ascending(result => result.CustomerId), new CreateIndexOptions { Name = "idx_customer_endpoint_risk_score_customer_id" }),
            new CreateIndexModel<CustomerEndpointRiskScoreResult>(Builders<CustomerEndpointRiskScoreResult>.IndexKeys.Ascending(result => result.SubscriptionId), new CreateIndexOptions { Name = "idx_customer_endpoint_risk_score_subscription_id" }),
            new CreateIndexModel<CustomerEndpointRiskScoreResult>(Builders<CustomerEndpointRiskScoreResult>.IndexKeys.Ascending(result => result.EndpointId), new CreateIndexOptions { Name = "idx_customer_endpoint_risk_score_endpoint_id" }),
            new CreateIndexModel<CustomerEndpointRiskScoreResult>(Builders<CustomerEndpointRiskScoreResult>.IndexKeys.Ascending(result => result.RiskLevel), new CreateIndexOptions { Name = "idx_customer_endpoint_risk_score_risk_level" }),
            new CreateIndexModel<CustomerEndpointRiskScoreResult>(Builders<CustomerEndpointRiskScoreResult>.IndexKeys.Descending(result => result.CalculatedAtUtc), new CreateIndexOptions { Name = "idx_customer_endpoint_risk_score_calculated_at_utc_desc" })
        };
    }

    public static IReadOnlyList<CreateIndexModel<WebhookFailureAnomalyDetectionResult>> CreateWebhookFailureAnomalyDetectionIndexModels()
    {
        return new[]
        {
            new CreateIndexModel<WebhookFailureAnomalyDetectionResult>(Builders<WebhookFailureAnomalyDetectionResult>.IndexKeys.Ascending(result => result.CustomerId), new CreateIndexOptions { Name = "idx_webhook_failure_anomaly_customer_id" }),
            new CreateIndexModel<WebhookFailureAnomalyDetectionResult>(Builders<WebhookFailureAnomalyDetectionResult>.IndexKeys.Ascending(result => result.SubscriptionId), new CreateIndexOptions { Name = "idx_webhook_failure_anomaly_subscription_id" }),
            new CreateIndexModel<WebhookFailureAnomalyDetectionResult>(Builders<WebhookFailureAnomalyDetectionResult>.IndexKeys.Ascending(result => result.EndpointId), new CreateIndexOptions { Name = "idx_webhook_failure_anomaly_endpoint_id" }),
            new CreateIndexModel<WebhookFailureAnomalyDetectionResult>(Builders<WebhookFailureAnomalyDetectionResult>.IndexKeys.Ascending(result => result.EventType), new CreateIndexOptions { Name = "idx_webhook_failure_anomaly_event_type" }),
            new CreateIndexModel<WebhookFailureAnomalyDetectionResult>(Builders<WebhookFailureAnomalyDetectionResult>.IndexKeys.Ascending(result => result.Environment), new CreateIndexOptions { Name = "idx_webhook_failure_anomaly_environment" }),
            new CreateIndexModel<WebhookFailureAnomalyDetectionResult>(Builders<WebhookFailureAnomalyDetectionResult>.IndexKeys.Ascending(result => result.IsAnomalyDetected), new CreateIndexOptions { Name = "idx_webhook_failure_anomaly_is_anomaly_detected" }),
            new CreateIndexModel<WebhookFailureAnomalyDetectionResult>(Builders<WebhookFailureAnomalyDetectionResult>.IndexKeys.Ascending(result => result.RiskLevel), new CreateIndexOptions { Name = "idx_webhook_failure_anomaly_risk_level" }),
            new CreateIndexModel<WebhookFailureAnomalyDetectionResult>(Builders<WebhookFailureAnomalyDetectionResult>.IndexKeys.Descending(result => result.CalculatedAtUtc), new CreateIndexOptions { Name = "idx_webhook_failure_anomaly_calculated_at_utc_desc" })
        };
    }
}
