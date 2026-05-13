using Confluent.Kafka;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services;
using HookBridge.AI.Worker.Services.RetryRecommendations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<IAiRetryRecommendationService, AiRetryRecommendationService>();
        return services;
    }

    public static IServiceCollection AddAiPromptServices(this IServiceCollection services)
    {
        services.AddSingleton<IWebhookFailurePromptBuilder, WebhookFailurePromptBuilder>();
        return services;
    }

    public static IServiceCollection AddAiKafkaServices(this IServiceCollection services)
    {
        services.AddSingleton<IAiAnalysisProducer, AiAnalysisProducer>();
        services.AddSingleton<IAiAnalysisConsumer, AiAnalysisConsumer>();
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
        services.AddHostedService<AiMongoIndexInitializer>();

        return services;
    }

    private static bool RequiresSasl(string securityProtocol)
        => Enum.TryParse<SecurityProtocol>(securityProtocol, ignoreCase: true, out var parsed) &&
           parsed is SecurityProtocol.SaslSsl or SecurityProtocol.SaslPlaintext;
}
