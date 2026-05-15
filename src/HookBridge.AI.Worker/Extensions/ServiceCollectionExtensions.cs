using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.PromptVersioning;
using HookBridge.AI.Worker.Services;
using HookBridge.AI.Worker.Services.EndpointHealthScoring;
using HookBridge.AI.Worker.Services.Fallback;
using HookBridge.AI.Worker.Services.RetryRecommendations;
using HookBridge.AI.Worker.Services.LogSummaries;
using HookBridge.AI.Worker.Services.PayloadSchemaDetection;
using HookBridge.AI.Worker.Services.SecurityAnalysis;
using HookBridge.AI.Worker.Services.JsonToDtoSuggestion;
using HookBridge.AI.Worker.Services.FluentValidationRuleGeneration;
using HookBridge.AI.Worker.Services.WebhookTransformationRecommendation;
using HookBridge.AI.Worker.Services.CustomerEndpointRiskScoring;
using HookBridge.AI.Worker.Services.WebhookFailureAnomalyDetection;
using HookBridge.AI.Worker.Services.DuplicateReplayDetection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AiPromptOptions>()
            .Bind(configuration.GetSection(AiPromptOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.DefaultVersion), "AIPrompts:DefaultVersion is required.")
            .Validate(options => AiPromptOptions.IsValidVersion(options.DefaultVersion), "AIPrompts:DefaultVersion must follow semantic format like v1.0.0.")
            .Validate(options => options.Prompts.Keys.All(AiPromptNames.IsKnown), "AIPrompts:Prompts may only contain known prompt names.")
            .Validate(options => options.Prompts.Values.All(AiPromptOptions.IsValidVersion), "AIPrompts prompt versions must follow semantic format like v1.0.0.")
            .ValidateOnStart();

        services
            .AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Provider),
                "AI:Provider is required when AI is enabled.")
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Model),
                "AI:Model is required when AI is enabled.")
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Endpoint),
                "AI:Endpoint is required when AI is enabled.")
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddAiMongoOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AiMongoOptions>()
            .Bind(configuration.GetSection(AiMongoOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "AiMongo:ConnectionString is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.DatabaseName),
                "AiMongo:DatabaseName is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.AiAnalysisResultsCollectionName),
                "AiMongo:AiAnalysisResultsCollectionName is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.PayloadSchemaDetectionResultsCollectionName),
                "AiMongo:PayloadSchemaDetectionResultsCollectionName is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.JsonToDtoSuggestionResultsCollectionName),
                "AiMongo:JsonToDtoSuggestionResultsCollectionName is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.FluentValidationRuleGenerationResultsCollectionName),
                "AiMongo:FluentValidationRuleGenerationResultsCollectionName is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.WebhookTransformationRecommendationResultsCollectionName),
                "AiMongo:WebhookTransformationRecommendationResultsCollectionName is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.CustomerEndpointRiskScoreResultsCollectionName),
                "AiMongo:CustomerEndpointRiskScoreResultsCollectionName is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.WebhookFailureAnomalyDetectionResultsCollectionName),
                "AiMongo:WebhookFailureAnomalyDetectionResultsCollectionName is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.AiAnomalyRecordsCollectionName),
                "AiMongo:AiAnomalyRecordsCollectionName is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.AiSecurityAnalysisResultsCollectionName),
                "AiMongo:AiSecurityAnalysisResultsCollectionName is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.WebhookEventFingerprintsCollectionName),
                "AiMongo:WebhookEventFingerprintsCollectionName is required.")
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddDuplicateReplayDetectionOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<DuplicateReplayDetectionOptions>()
            .Bind(configuration.GetSection(DuplicateReplayDetectionOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => string.Equals(options.HashAlgorithm, "SHA256", StringComparison.OrdinalIgnoreCase), "DuplicateReplayDetection:HashAlgorithm must be SHA256.")
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddAiKafkaOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AiKafkaOptions>()
            .Bind(configuration.GetSection(AiKafkaOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.BootstrapServers),
                "AiKafka:BootstrapServers is required.")
            .Validate(
                options => Enum.TryParse<SecurityProtocol>(options.SecurityProtocol, ignoreCase: true, out _),
                "AiKafka:SecurityProtocol must be a valid Confluent.Kafka SecurityProtocol value.")
            .Validate(
                options => !RequiresSasl(options.SecurityProtocol) || !string.IsNullOrWhiteSpace(options.SaslMechanism),
                "AiKafka:SaslMechanism is required when using a SASL security protocol.")
            .Validate(
                options => !RequiresSasl(options.SecurityProtocol) || Enum.TryParse<SaslMechanism>(options.SaslMechanism, ignoreCase: true, out _),
                "AiKafka:SaslMechanism must be a valid Confluent.Kafka SaslMechanism value when using a SASL security protocol.")
            .Validate(
                options => !RequiresSasl(options.SecurityProtocol) || !string.IsNullOrWhiteSpace(options.SaslUsername),
                "AiKafka:SaslUsername is required when using a SASL security protocol.")
            .Validate(
                options => !RequiresSasl(options.SecurityProtocol) || !string.IsNullOrWhiteSpace(options.SaslPassword),
                "AiKafka:SaslPassword is required when using a SASL security protocol.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.AiAnalysisTopic),
                "AiKafka:AiAnalysisTopic is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.PayloadSchemaDetectionTopic),
                "AiKafka:PayloadSchemaDetectionTopic is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.JsonToDtoSuggestionTopic),
                "AiKafka:JsonToDtoSuggestionTopic is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.FluentValidationRuleGenerationTopic),
                "AiKafka:FluentValidationRuleGenerationTopic is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.WebhookTransformationRecommendationTopic),
                "AiKafka:WebhookTransformationRecommendationTopic is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.WebhookFailureAnomalyDetectionTopic),
                "AiKafka:WebhookFailureAnomalyDetectionTopic is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.AnomaliesTopic),
                "AiKafka:AnomaliesTopic is required when anomaly detection is enabled.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.SecurityAnalysisTopic),
                "AiKafka:SecurityAnalysisTopic is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.DuplicateReplayDetectionTopic),
                "AiKafka:DuplicateReplayDetectionTopic is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConsumerGroupId),
                "AiKafka:ConsumerGroupId is required.")
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddAiKernelServices(this IServiceCollection services)
    {
        services.AddSingleton<IKernelFactory, SemanticKernelFactory>();
        services.AddSingleton<ILocalLlmClient, SemanticKernelLocalLlmClient>();
        return services;
    }

    public static IServiceCollection AddAiRetryRecommendationServices(this IServiceCollection services)
    {
        services.AddAiFallbackServices();
        services.TryAddSingleton<IAiRetryRecommendationService, AiRetryRecommendationService>();
        return services;
    }

    public static IServiceCollection AddAiLogSummarizationServices(this IServiceCollection services)
    {
        services.AddAiFallbackServices();
        services.TryAddSingleton<IAiLogSummarizationService, AiLogSummarizationService>();
        return services;
    }

    public static IServiceCollection AddPayloadSchemaDetectionServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IPayloadSchemaDetectionAgent, PayloadSchemaDetectionAgent>();
        return services;
    }

    public static IServiceCollection AddJsonToDtoSuggestionServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IJsonToDtoSuggestionAgent, JsonToDtoSuggestionAgent>();
        return services;
    }

    public static IServiceCollection AddFluentValidationRuleGenerationServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IFluentValidationRuleGenerationAgent, FluentValidationRuleGenerationAgent>();
        return services;
    }

    public static IServiceCollection AddWebhookTransformationRecommendationServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IWebhookTransformationRecommendationAgent, WebhookTransformationRecommendationAgent>();
        return services;
    }

    public static IServiceCollection AddCustomerEndpointRiskScoringServices(this IServiceCollection services)
    {
        services.TryAddSingleton<ICustomerEndpointRiskScoringService, CustomerEndpointRiskScoringService>();
        return services;
    }

    public static IServiceCollection AddWebhookFailureAnomalyDetectionServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IWebhookFailureAnomalyDetectionService, WebhookFailureAnomalyDetectionService>();
        return services;
    }

    public static IServiceCollection AddAiSecurityAnalysisServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IAiSecurityAnalysisAgent, AiSecurityAnalysisAgent>();
        return services;
    }

    public static IServiceCollection AddWebhookDuplicateReplayDetectionServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IWebhookFingerprintHashService, WebhookFingerprintHashService>();
        services.TryAddSingleton<IWebhookDuplicateReplayDetectionService, WebhookDuplicateReplayDetectionService>();
        return services;
    }

    public static IServiceCollection AddAiFallbackServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IEndpointHealthScoringService, EndpointHealthScoringService>();
        services.TryAddSingleton<IAiFallbackService, AiFallbackService>();
        return services;
    }

    public static IServiceCollection AddEndpointHealthScoringServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IEndpointHealthScoringService, EndpointHealthScoringService>();
        services.TryAddSingleton<IAiFallbackService, AiFallbackService>();
        return services;
    }

    public static IServiceCollection AddAiPromptServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IAiPromptVersionProvider, AiPromptVersionProvider>();
        services.AddSingleton<IWebhookFailurePromptBuilder, WebhookFailurePromptBuilder>();
        services.AddSingleton<IAiLogSummaryPromptBuilder, AiLogSummaryPromptBuilder>();
        services.AddSingleton<IPayloadSchemaDetectionPromptBuilder, PayloadSchemaDetectionPromptBuilder>();
        services.AddSingleton<IJsonToDtoPromptBuilder, JsonToDtoPromptBuilder>();
        services.AddSingleton<IFluentValidationPromptBuilder, FluentValidationPromptBuilder>();
        services.AddSingleton<IWebhookTransformationPromptBuilder, WebhookTransformationPromptBuilder>();
        services.AddSingleton<IAiSecurityAnalysisPromptBuilder, AiSecurityAnalysisPromptBuilder>();
        return services;
    }

    public static IServiceCollection AddAiKafkaServices(this IServiceCollection services)
    {
        services.AddSingleton<IAiAnalysisProducer, AiAnalysisProducer>();
        services.AddSingleton<IAiAnalysisConsumer, AiAnalysisConsumer>();
        services.AddSingleton<IAiAnomalyProducer, AiAnomalyProducer>();
        services.AddSingleton<IAiAnomalyConsumer, AiAnomalyConsumer>();
        services.AddSingleton<IPayloadSchemaDetectionConsumer, PayloadSchemaDetectionConsumer>();
        services.AddSingleton<IJsonToDtoSuggestionConsumer, JsonToDtoSuggestionConsumer>();
        services.AddSingleton<IFluentValidationRuleGenerationConsumer, FluentValidationRuleGenerationConsumer>();
        services.AddSingleton<IWebhookTransformationRecommendationConsumer, WebhookTransformationRecommendationConsumer>();
        services.AddSingleton<ICustomerEndpointRiskScoreConsumer, CustomerEndpointRiskScoreConsumer>();
        services.AddSingleton<IWebhookFailureAnomalyDetectionConsumer, WebhookFailureAnomalyDetectionConsumer>();
        services.AddSingleton<IAiSecurityAnalysisConsumer, AiSecurityAnalysisConsumer>();
        services.AddSingleton<IWebhookDuplicateReplayDetectionConsumer, WebhookDuplicateReplayDetectionConsumer>();
        return services;
    }

    public static IServiceCollection AddAiMongoServices(this IServiceCollection services)
    {
        services.AddSingleton<IMongoClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<AiMongoOptions>>().Value;
            return new MongoClient(options.ConnectionString);
        });
        services.AddSingleton<IAiAnalysisResultCollectionProvider, AiAnalysisResultCollectionProvider>();
        services.AddSingleton<IAiAnalysisResultRepository, AiAnalysisResultRepository>();
        services.AddSingleton<IPayloadSchemaDetectionCollectionProvider, PayloadSchemaDetectionCollectionProvider>();
        services.AddSingleton<IPayloadSchemaDetectionRepository, PayloadSchemaDetectionRepository>();
        services.AddSingleton<IJsonToDtoSuggestionCollectionProvider, JsonToDtoSuggestionCollectionProvider>();
        services.AddSingleton<IJsonToDtoSuggestionRepository, JsonToDtoSuggestionRepository>();
        services.AddSingleton<IFluentValidationRuleGenerationCollectionProvider, FluentValidationRuleGenerationCollectionProvider>();
        services.AddSingleton<IFluentValidationRuleGenerationRepository, FluentValidationRuleGenerationRepository>();
        services.AddSingleton<IWebhookTransformationRecommendationCollectionProvider, WebhookTransformationRecommendationCollectionProvider>();
        services.AddSingleton<IWebhookTransformationRecommendationRepository, WebhookTransformationRecommendationRepository>();
        services.AddSingleton<ICustomerEndpointRiskScoreCollectionProvider, CustomerEndpointRiskScoreCollectionProvider>();
        services.AddSingleton<ICustomerEndpointRiskScoreRepository, CustomerEndpointRiskScoreRepository>();
        services.AddSingleton<IWebhookFailureAnomalyDetectionCollectionProvider, WebhookFailureAnomalyDetectionCollectionProvider>();
        services.AddSingleton<IWebhookFailureAnomalyDetectionRepository, WebhookFailureAnomalyDetectionRepository>();
        services.AddSingleton<IAiAnomalyRecordCollectionProvider, AiAnomalyRecordCollectionProvider>();
        services.AddSingleton<IAiAnomalyRecordRepository, AiAnomalyRecordRepository>();
        services.AddSingleton<IAiSecurityAnalysisCollectionProvider, AiSecurityAnalysisCollectionProvider>();
        services.AddSingleton<IAiSecurityAnalysisRepository, AiSecurityAnalysisRepository>();
        services.AddSingleton<IWebhookEventFingerprintCollectionProvider, WebhookEventFingerprintCollectionProvider>();
        services.AddSingleton<IWebhookEventFingerprintRepository, WebhookEventFingerprintRepository>();
        services.AddHostedService<AiMongoIndexInitializer>();

        return services;
    }

    private static bool RequiresSasl(string securityProtocol)
        => Enum.TryParse<SecurityProtocol>(securityProtocol, ignoreCase: true, out var parsed) &&
           parsed is SecurityProtocol.SaslSsl or SecurityProtocol.SaslPlaintext;
}
