using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiMongoIndexInitializer : IHostedService
{
    private readonly IAiAnalysisResultCollectionProvider _collectionProvider;
    private readonly ICustomerEndpointRiskScoreCollectionProvider? _riskScoreCollectionProvider;
    private readonly IWebhookFailureAnomalyDetectionCollectionProvider? _failureAnomalyCollectionProvider;
    private readonly IAiAnomalyRecordCollectionProvider? _anomalyRecordCollectionProvider;
    private readonly IAiSecurityAnalysisCollectionProvider? _securityAnalysisCollectionProvider;
    private readonly IWebhookEventFingerprintCollectionProvider? _fingerprintCollectionProvider;
    private readonly IAiRecommendationApprovalCollectionProvider? _approvalCollectionProvider;
    private readonly IWebhookTransformationRecommendationCollectionProvider? _transformationRecommendationCollectionProvider;
    private readonly ILogger<AiMongoIndexInitializer> _logger;

    public AiMongoIndexInitializer(
        IAiAnalysisResultCollectionProvider collectionProvider,
        ILogger<AiMongoIndexInitializer> logger)
        : this(collectionProvider, null, null, null, null, null, null, null, logger)
    {
    }

    public AiMongoIndexInitializer(
        IAiAnalysisResultCollectionProvider collectionProvider,
        ICustomerEndpointRiskScoreCollectionProvider? riskScoreCollectionProvider,
        ILogger<AiMongoIndexInitializer> logger)
        : this(collectionProvider, riskScoreCollectionProvider, null, null, null, null, null, null, logger)
    {
    }

    public AiMongoIndexInitializer(
        IAiAnalysisResultCollectionProvider collectionProvider,
        ICustomerEndpointRiskScoreCollectionProvider? riskScoreCollectionProvider,
        IWebhookFailureAnomalyDetectionCollectionProvider? failureAnomalyCollectionProvider,
        ILogger<AiMongoIndexInitializer> logger)
        : this(collectionProvider, riskScoreCollectionProvider, failureAnomalyCollectionProvider, null, null, null, null, null, logger)
    {
    }

    public AiMongoIndexInitializer(
        IAiAnalysisResultCollectionProvider collectionProvider,
        ICustomerEndpointRiskScoreCollectionProvider? riskScoreCollectionProvider,
        IWebhookFailureAnomalyDetectionCollectionProvider? failureAnomalyCollectionProvider,
        IAiAnomalyRecordCollectionProvider? anomalyRecordCollectionProvider,
        IAiSecurityAnalysisCollectionProvider? securityAnalysisCollectionProvider,
        IWebhookEventFingerprintCollectionProvider? fingerprintCollectionProvider,
        IAiRecommendationApprovalCollectionProvider? approvalCollectionProvider,
        IWebhookTransformationRecommendationCollectionProvider? transformationRecommendationCollectionProvider,
        ILogger<AiMongoIndexInitializer> logger)
    {
        _collectionProvider = collectionProvider;
        _riskScoreCollectionProvider = riskScoreCollectionProvider;
        _failureAnomalyCollectionProvider = failureAnomalyCollectionProvider;
        _anomalyRecordCollectionProvider = anomalyRecordCollectionProvider;
        _securityAnalysisCollectionProvider = securityAnalysisCollectionProvider;
        _fingerprintCollectionProvider = fingerprintCollectionProvider;
        _approvalCollectionProvider = approvalCollectionProvider;
        _transformationRecommendationCollectionProvider = transformationRecommendationCollectionProvider;
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

        if (_anomalyRecordCollectionProvider is not null)
        {
            var anomalyRecordCollection = _anomalyRecordCollectionProvider.GetCollection();
            await anomalyRecordCollection.Indexes.CreateManyAsync(CreateAiAnomalyRecordIndexModels(), cancellationToken);
        }

        if (_securityAnalysisCollectionProvider is not null)
        {
            var securityAnalysisCollection = _securityAnalysisCollectionProvider.GetCollection();
            await securityAnalysisCollection.Indexes.CreateManyAsync(CreateAiSecurityAnalysisIndexModels(), cancellationToken);
        }

        if (_fingerprintCollectionProvider is not null)
        {
            var fingerprintCollection = _fingerprintCollectionProvider.GetCollection();
            await fingerprintCollection.Indexes.CreateManyAsync(CreateWebhookEventFingerprintIndexModels(), cancellationToken);
        }

        if (_approvalCollectionProvider is not null)
        {
            var approvalCollection = _approvalCollectionProvider.GetCollection();
            await approvalCollection.Indexes.CreateManyAsync(CreateAiRecommendationApprovalIndexModels(), cancellationToken);
        }

        if (_transformationRecommendationCollectionProvider is not null)
        {
            var transformationCollection = _transformationRecommendationCollectionProvider.GetCollection();
            await transformationCollection.Indexes.CreateManyAsync(CreateWebhookTransformationRecommendationIndexModels(), cancellationToken);
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




    public static IReadOnlyList<CreateIndexModel<AiRecommendationApproval>> CreateAiRecommendationApprovalIndexModels()
    {
        return new[]
        {
            new CreateIndexModel<AiRecommendationApproval>(Builders<AiRecommendationApproval>.IndexKeys.Ascending(approval => approval.RecommendationId), new CreateIndexOptions { Name = "idx_ai_recommendation_approvals_recommendation_id_unique", Unique = true }),
            new CreateIndexModel<AiRecommendationApproval>(Builders<AiRecommendationApproval>.IndexKeys.Ascending(approval => approval.EventId), new CreateIndexOptions { Name = "idx_ai_recommendation_approvals_event_id" }),
            new CreateIndexModel<AiRecommendationApproval>(Builders<AiRecommendationApproval>.IndexKeys.Ascending(approval => approval.CorrelationId), new CreateIndexOptions { Name = "idx_ai_recommendation_approvals_correlation_id" }),
            new CreateIndexModel<AiRecommendationApproval>(Builders<AiRecommendationApproval>.IndexKeys.Ascending(approval => approval.CustomerId), new CreateIndexOptions { Name = "idx_ai_recommendation_approvals_customer_id" }),
            new CreateIndexModel<AiRecommendationApproval>(Builders<AiRecommendationApproval>.IndexKeys.Ascending(approval => approval.SubscriptionId), new CreateIndexOptions { Name = "idx_ai_recommendation_approvals_subscription_id" }),
            new CreateIndexModel<AiRecommendationApproval>(Builders<AiRecommendationApproval>.IndexKeys.Ascending(approval => approval.EndpointId), new CreateIndexOptions { Name = "idx_ai_recommendation_approvals_endpoint_id" }),
            new CreateIndexModel<AiRecommendationApproval>(Builders<AiRecommendationApproval>.IndexKeys.Ascending(approval => approval.RecommendationType), new CreateIndexOptions { Name = "idx_ai_recommendation_approvals_recommendation_type" }),
            new CreateIndexModel<AiRecommendationApproval>(Builders<AiRecommendationApproval>.IndexKeys.Ascending(approval => approval.ApprovalStatus), new CreateIndexOptions { Name = "idx_ai_recommendation_approvals_approval_status" }),
            new CreateIndexModel<AiRecommendationApproval>(Builders<AiRecommendationApproval>.IndexKeys.Ascending(approval => approval.RiskLevel), new CreateIndexOptions { Name = "idx_ai_recommendation_approvals_risk_level" }),
            new CreateIndexModel<AiRecommendationApproval>(Builders<AiRecommendationApproval>.IndexKeys.Descending(approval => approval.CreatedAtUtc), new CreateIndexOptions { Name = "idx_ai_recommendation_approvals_created_at_utc_desc" }),
            new CreateIndexModel<AiRecommendationApproval>(Builders<AiRecommendationApproval>.IndexKeys.Ascending(approval => approval.ExpiresAtUtc), new CreateIndexOptions { Name = "idx_ai_recommendation_approvals_expires_at_utc" })
        };
    }

    public static IReadOnlyList<CreateIndexModel<WebhookTransformationRecommendationResult>> CreateWebhookTransformationRecommendationIndexModels()
    {
        return new[]
        {
            new CreateIndexModel<WebhookTransformationRecommendationResult>(Builders<WebhookTransformationRecommendationResult>.IndexKeys.Ascending(x => x.EventId), new CreateIndexOptions { Name = "idx_webhook_transformation_recommendations_event_id" }),
            new CreateIndexModel<WebhookTransformationRecommendationResult>(Builders<WebhookTransformationRecommendationResult>.IndexKeys.Ascending(x => x.CorrelationId), new CreateIndexOptions { Name = "idx_webhook_transformation_recommendations_correlation_id" }),
            new CreateIndexModel<WebhookTransformationRecommendationResult>(Builders<WebhookTransformationRecommendationResult>.IndexKeys.Ascending(x => x.CustomerId), new CreateIndexOptions { Name = "idx_webhook_transformation_recommendations_customer_id" }),
            new CreateIndexModel<WebhookTransformationRecommendationResult>(Builders<WebhookTransformationRecommendationResult>.IndexKeys.Ascending(x => x.EventType), new CreateIndexOptions { Name = "idx_webhook_transformation_recommendations_event_type" }),
            new CreateIndexModel<WebhookTransformationRecommendationResult>(Builders<WebhookTransformationRecommendationResult>.IndexKeys.Ascending(x => x.RiskLevel), new CreateIndexOptions { Name = "idx_webhook_transformation_recommendations_risk_level" }),
            new CreateIndexModel<WebhookTransformationRecommendationResult>(Builders<WebhookTransformationRecommendationResult>.IndexKeys.Descending(x => x.GeneratedAtUtc), new CreateIndexOptions { Name = "idx_webhook_transformation_recommendations_generated_at_desc" })
        };
    }

    public static IReadOnlyList<CreateIndexModel<WebhookEventFingerprint>> CreateWebhookEventFingerprintIndexModels()
    {
        return new[]
        {
            new CreateIndexModel<WebhookEventFingerprint>(Builders<WebhookEventFingerprint>.IndexKeys.Ascending(x => x.EventId), new CreateIndexOptions { Name = "idx_webhook_event_fingerprints_event_id" }),
            new CreateIndexModel<WebhookEventFingerprint>(Builders<WebhookEventFingerprint>.IndexKeys.Ascending(x => x.CorrelationId), new CreateIndexOptions { Name = "idx_webhook_event_fingerprints_correlation_id" }),
            new CreateIndexModel<WebhookEventFingerprint>(Builders<WebhookEventFingerprint>.IndexKeys.Ascending(x => x.CustomerId), new CreateIndexOptions { Name = "idx_webhook_event_fingerprints_customer_id" }),
            new CreateIndexModel<WebhookEventFingerprint>(Builders<WebhookEventFingerprint>.IndexKeys.Ascending(x => x.SubscriptionId), new CreateIndexOptions { Name = "idx_webhook_event_fingerprints_subscription_id" }),
            new CreateIndexModel<WebhookEventFingerprint>(Builders<WebhookEventFingerprint>.IndexKeys.Ascending(x => x.EndpointId), new CreateIndexOptions { Name = "idx_webhook_event_fingerprints_endpoint_id" }),
            new CreateIndexModel<WebhookEventFingerprint>(Builders<WebhookEventFingerprint>.IndexKeys.Ascending(x => x.PayloadHash), new CreateIndexOptions { Name = "idx_webhook_event_fingerprints_payload_hash" }),
            new CreateIndexModel<WebhookEventFingerprint>(Builders<WebhookEventFingerprint>.IndexKeys.Ascending(x => x.SignatureHash), new CreateIndexOptions { Name = "idx_webhook_event_fingerprints_signature_hash" }),
            new CreateIndexModel<WebhookEventFingerprint>(Builders<WebhookEventFingerprint>.IndexKeys.Descending(x => x.ReceivedAtUtc), new CreateIndexOptions { Name = "idx_webhook_event_fingerprints_received_at_desc" }),
            new CreateIndexModel<WebhookEventFingerprint>(Builders<WebhookEventFingerprint>.IndexKeys.Ascending(x => x.ExpiresAtUtc), new CreateIndexOptions { Name = "idx_webhook_event_fingerprints_expires_at_ttl", ExpireAfter = TimeSpan.Zero }),
            new CreateIndexModel<WebhookEventFingerprint>(Builders<WebhookEventFingerprint>.IndexKeys.Ascending(x => x.CustomerId).Ascending(x => x.PayloadHash).Descending(x => x.ReceivedAtUtc), new CreateIndexOptions { Name = "idx_webhook_event_fingerprints_customer_payload_received_desc" }),
            new CreateIndexModel<WebhookEventFingerprint>(Builders<WebhookEventFingerprint>.IndexKeys.Ascending(x => x.SubscriptionId).Ascending(x => x.EventId), new CreateIndexOptions { Name = "idx_webhook_event_fingerprints_subscription_event" }),
            new CreateIndexModel<WebhookEventFingerprint>(Builders<WebhookEventFingerprint>.IndexKeys.Ascending(x => x.EndpointId).Ascending(x => x.SignatureHash), new CreateIndexOptions { Name = "idx_webhook_event_fingerprints_endpoint_signature" })
        };
    }

    public static IReadOnlyList<CreateIndexModel<AiAnomalyRecord>> CreateAiAnomalyRecordIndexModels()
    {
        return new[]
        {
            new CreateIndexModel<AiAnomalyRecord>(Builders<AiAnomalyRecord>.IndexKeys.Ascending(record => record.AnomalyId), new CreateIndexOptions { Name = "idx_ai_anomaly_records_anomaly_id_unique", Unique = true }),
            new CreateIndexModel<AiAnomalyRecord>(Builders<AiAnomalyRecord>.IndexKeys.Ascending(record => record.EventId), new CreateIndexOptions { Name = "idx_ai_anomaly_records_event_id" }),
            new CreateIndexModel<AiAnomalyRecord>(Builders<AiAnomalyRecord>.IndexKeys.Ascending(record => record.CorrelationId), new CreateIndexOptions { Name = "idx_ai_anomaly_records_correlation_id" }),
            new CreateIndexModel<AiAnomalyRecord>(Builders<AiAnomalyRecord>.IndexKeys.Ascending(record => record.CustomerId), new CreateIndexOptions { Name = "idx_ai_anomaly_records_customer_id" }),
            new CreateIndexModel<AiAnomalyRecord>(Builders<AiAnomalyRecord>.IndexKeys.Ascending(record => record.SubscriptionId), new CreateIndexOptions { Name = "idx_ai_anomaly_records_subscription_id" }),
            new CreateIndexModel<AiAnomalyRecord>(Builders<AiAnomalyRecord>.IndexKeys.Ascending(record => record.EndpointId), new CreateIndexOptions { Name = "idx_ai_anomaly_records_endpoint_id" }),
            new CreateIndexModel<AiAnomalyRecord>(Builders<AiAnomalyRecord>.IndexKeys.Ascending(record => record.Environment), new CreateIndexOptions { Name = "idx_ai_anomaly_records_environment" }),
            new CreateIndexModel<AiAnomalyRecord>(Builders<AiAnomalyRecord>.IndexKeys.Ascending(record => record.EventType), new CreateIndexOptions { Name = "idx_ai_anomaly_records_event_type" }),
            new CreateIndexModel<AiAnomalyRecord>(Builders<AiAnomalyRecord>.IndexKeys.Ascending(record => record.AnomalyType), new CreateIndexOptions { Name = "idx_ai_anomaly_records_anomaly_type" }),
            new CreateIndexModel<AiAnomalyRecord>(Builders<AiAnomalyRecord>.IndexKeys.Ascending(record => record.RiskLevel), new CreateIndexOptions { Name = "idx_ai_anomaly_records_risk_level" }),
            new CreateIndexModel<AiAnomalyRecord>(Builders<AiAnomalyRecord>.IndexKeys.Ascending(record => record.AnomalyScore), new CreateIndexOptions { Name = "idx_ai_anomaly_records_anomaly_score" }),
            new CreateIndexModel<AiAnomalyRecord>(Builders<AiAnomalyRecord>.IndexKeys.Descending(record => record.CreatedAtUtc), new CreateIndexOptions { Name = "idx_ai_anomaly_records_created_at_utc_desc" }),
            new CreateIndexModel<AiAnomalyRecord>(Builders<AiAnomalyRecord>.IndexKeys.Ascending(record => record.CustomerId).Descending(record => record.CreatedAtUtc), new CreateIndexOptions { Name = "idx_ai_anomaly_records_customer_id_created_at_desc" }),
            new CreateIndexModel<AiAnomalyRecord>(Builders<AiAnomalyRecord>.IndexKeys.Ascending(record => record.EndpointId).Descending(record => record.CreatedAtUtc), new CreateIndexOptions { Name = "idx_ai_anomaly_records_endpoint_id_created_at_desc" }),
            new CreateIndexModel<AiAnomalyRecord>(Builders<AiAnomalyRecord>.IndexKeys.Ascending(record => record.RiskLevel).Descending(record => record.CreatedAtUtc), new CreateIndexOptions { Name = "idx_ai_anomaly_records_risk_level_created_at_desc" })
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

    public static IReadOnlyList<CreateIndexModel<AiSecurityAnalysisResult>> CreateAiSecurityAnalysisIndexModels()
    {
        return new[]
        {
            new CreateIndexModel<AiSecurityAnalysisResult>(Builders<AiSecurityAnalysisResult>.IndexKeys.Ascending(result => result.EventId), new CreateIndexOptions { Name = "idx_ai_security_analysis_event_id" }),
            new CreateIndexModel<AiSecurityAnalysisResult>(Builders<AiSecurityAnalysisResult>.IndexKeys.Ascending(result => result.CorrelationId), new CreateIndexOptions { Name = "idx_ai_security_analysis_correlation_id" }),
            new CreateIndexModel<AiSecurityAnalysisResult>(Builders<AiSecurityAnalysisResult>.IndexKeys.Ascending(result => result.CustomerId), new CreateIndexOptions { Name = "idx_ai_security_analysis_customer_id" }),
            new CreateIndexModel<AiSecurityAnalysisResult>(Builders<AiSecurityAnalysisResult>.IndexKeys.Ascending(result => result.SubscriptionId), new CreateIndexOptions { Name = "idx_ai_security_analysis_subscription_id" }),
            new CreateIndexModel<AiSecurityAnalysisResult>(Builders<AiSecurityAnalysisResult>.IndexKeys.Ascending(result => result.EndpointId), new CreateIndexOptions { Name = "idx_ai_security_analysis_endpoint_id" }),
            new CreateIndexModel<AiSecurityAnalysisResult>(Builders<AiSecurityAnalysisResult>.IndexKeys.Ascending(result => result.Environment), new CreateIndexOptions { Name = "idx_ai_security_analysis_environment" }),
            new CreateIndexModel<AiSecurityAnalysisResult>(Builders<AiSecurityAnalysisResult>.IndexKeys.Ascending(result => result.RiskLevel), new CreateIndexOptions { Name = "idx_ai_security_analysis_risk_level" }),
            new CreateIndexModel<AiSecurityAnalysisResult>(Builders<AiSecurityAnalysisResult>.IndexKeys.Ascending(result => result.IsSuspicious), new CreateIndexOptions { Name = "idx_ai_security_analysis_is_suspicious" }),
            new CreateIndexModel<AiSecurityAnalysisResult>(Builders<AiSecurityAnalysisResult>.IndexKeys.Descending(result => result.GeneratedAtUtc), new CreateIndexOptions { Name = "idx_ai_security_analysis_generated_at_utc_desc" })
        };
    }
}
