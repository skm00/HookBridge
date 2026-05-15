using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services;
using HookBridge.AI.Worker.Services.EndpointHealthScoring;
using HookBridge.AI.Worker.Services.Fallback;
using HookBridge.AI.Worker.Services.LogSummaries;
using HookBridge.AI.Worker.Services.RetryRecommendations;
using HookBridge.AI.Worker.Services.SecurityAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.IntegrationTests;

public sealed class AiWorkerServiceRegistrationIntegrationTests
{
    [Fact]
    public void ServiceCollectionExtensions_RegisterAiWorkerPipelineDependencies()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Enabled"] = "true",
                ["AI:Provider"] = "ollama",
                ["AI:Model"] = "llama3.1",
                ["AI:Endpoint"] = "http://localhost:11434",
                ["AiMongo:ConnectionString"] = "mongodb://localhost:27017",
                ["AiMongo:DatabaseName"] = "hookbridge_ai_tests",
                ["AiMongo:AiAnalysisResultsCollectionName"] = "ai_analysis_results",
                ["AiKafka:BootstrapServers"] = "localhost:9092",
                ["AiKafka:SecurityProtocol"] = "Plaintext",
                ["AiKafka:AiAnalysisTopic"] = "hookbridge.ai.analysis",
                ["AiKafka:AnomaliesTopic"] = "hookbridge.ai.anomalies",
                ["AiKafka:ConsumerGroupId"] = "hookbridge-ai-worker-tests"
            })
            .Build();

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<ILocalLlmClient, StubLocalLlmClient>()
            .AddAiOptions(configuration)
            .AddAiMongoOptions(configuration)
            .AddAiKafkaOptions(configuration)
            .AddAiRetryRecommendationServices()
            .AddAiLogSummarizationServices()
            .AddEndpointHealthScoringServices()
            .AddAiPromptServices()
            .AddAiSecurityAnalysisServices()
            .AddAiKafkaServices()
            .AddAiMongoServices();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        provider.GetRequiredService<IOptions<AiOptions>>().Value.Provider.Should().Be("ollama");
        provider.GetRequiredService<IAiRetryRecommendationService>().Should().NotBeNull();
        provider.GetRequiredService<IAiLogSummarizationService>().Should().NotBeNull();
        provider.GetRequiredService<IEndpointHealthScoringService>().Should().NotBeNull();
        provider.GetRequiredService<IAiFallbackService>().Should().NotBeNull();
        provider.GetRequiredService<IWebhookFailurePromptBuilder>().Should().NotBeNull();
        provider.GetRequiredService<IAiLogSummaryPromptBuilder>().Should().NotBeNull();
        provider.GetRequiredService<IAiSecurityAnalysisPromptBuilder>().Should().NotBeNull();
        provider.GetRequiredService<IAiSecurityAnalysisAgent>().Should().NotBeNull();
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IAiAnalysisProducer));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IAiAnalysisConsumer));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IAiAnomalyProducer));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IAiAnomalyConsumer));
        provider.GetRequiredService<IAiAnalysisResultRepository>().Should().NotBeNull();
        provider.GetRequiredService<IAiSecurityAnalysisRepository>().Should().NotBeNull();
    }
}

internal sealed class StubLocalLlmClient : ILocalLlmClient
{
    public Task<LlmResponseResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        => Task.FromResult(LlmResponseResult.Success("{}", 0));
}
