using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Prompts;
using HookBridge.AI.Worker.Services;
using HookBridge.AI.Worker.Services.EndpointHealthScoring;
using HookBridge.AI.Worker.Services.Fallback;
using HookBridge.AI.Worker.Services.LogSummaries;
using HookBridge.AI.Worker.Services.CustomerEndpointRiskScoring;
using HookBridge.AI.Worker.Services.RetryRecommendations;
using HookBridge.AI.Worker.Services.WebhookFailureAnomalyDetection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiDependencyInjectionTests
{
    [Fact]
    public void AddAiServiceExtensions_RegisterCoreAiServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAiOptions(BuildConfiguration(enabled: true));
        services.AddAiPromptServices();
        services.AddAiKernelServices();
        services.AddAiRetryRecommendationServices();
        services.AddAiLogSummarizationServices();
        services.AddEndpointHealthScoringServices();
        services.AddCustomerEndpointRiskScoringServices();
        services.AddWebhookFailureAnomalyDetectionServices();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<IWebhookFailurePromptBuilder>().Should().BeOfType<WebhookFailurePromptBuilder>();
        provider.GetRequiredService<IAiLogSummaryPromptBuilder>().Should().BeOfType<AiLogSummaryPromptBuilder>();
        provider.GetRequiredService<IKernelFactory>().Should().BeOfType<SemanticKernelFactory>();
        provider.GetRequiredService<ILocalLlmClient>().Should().BeOfType<SemanticKernelLocalLlmClient>();
        provider.GetRequiredService<IAiRetryRecommendationService>().Should().BeOfType<AiRetryRecommendationService>();
        provider.GetRequiredService<IAiLogSummarizationService>().Should().BeOfType<AiLogSummarizationService>();
        provider.GetRequiredService<IEndpointHealthScoringService>().Should().BeOfType<EndpointHealthScoringService>();
        provider.GetRequiredService<ICustomerEndpointRiskScoringService>().Should().BeOfType<CustomerEndpointRiskScoringService>();
        provider.GetRequiredService<IWebhookFailureAnomalyDetectionService>().Should().BeOfType<WebhookFailureAnomalyDetectionService>();
        provider.GetRequiredService<IAiFallbackService>().Should().BeOfType<AiFallbackService>();
    }

    [Fact]
    public void AddAiKernelServices_DoesNotInstantiateLlmClientUntilResolved()
    {
        CountingKernelFactory.Instances = 0;
        CountingLocalLlmClient.Instances = 0;

        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<AiOptions>(options => options.Enabled = false);
        services.AddSingleton<IKernelFactory, CountingKernelFactory>();
        services.AddSingleton<ILocalLlmClient, CountingLocalLlmClient>();
        services.AddLogging();
        services.AddAiFallbackServices();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<IAiFallbackService>().Should().NotBeNull();
        CountingLocalLlmClient.Instances.Should().Be(0);
        CountingKernelFactory.Instances.Should().Be(0);
    }

    [Fact]
    public void AddAiServices_WithDisabledAi_ResolvesRequiredRuleBasedServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAiOptions(BuildConfiguration(enabled: false));
        services.AddAiPromptServices();
        services.AddAiFallbackServices();
        services.TryAddSingleton<ILocalLlmClient, DisabledModeLocalLlmClient>();
        services.AddAiRetryRecommendationServices();
        services.AddAiLogSummarizationServices();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<IOptions<AiOptions>>().Value.Enabled.Should().BeFalse();
        provider.GetRequiredService<IAiFallbackService>().Should().NotBeNull();
        provider.GetRequiredService<IAiRetryRecommendationService>().Should().NotBeNull();
        provider.GetRequiredService<IAiLogSummarizationService>().Should().NotBeNull();
    }

    private static IConfiguration BuildConfiguration(bool enabled)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{AiOptions.SectionName}:Enabled"] = enabled.ToString(),
                [$"{AiOptions.SectionName}:Provider"] = enabled ? "Ollama" : string.Empty,
                [$"{AiOptions.SectionName}:Model"] = enabled ? "llama3-test" : string.Empty,
                [$"{AiOptions.SectionName}:Endpoint"] = enabled ? "http://localhost:11434" : string.Empty
            })
            .Build();

    private sealed class CountingKernelFactory : IKernelFactory
    {
        public static int Instances { get; set; }

        public CountingKernelFactory()
        {
            Instances++;
        }

        public Microsoft.SemanticKernel.Kernel CreateKernel() => throw new NotSupportedException();
    }

    private sealed class CountingLocalLlmClient : ILocalLlmClient
    {
        public static int Instances { get; set; }

        public CountingLocalLlmClient()
        {
            Instances++;
        }

        public Task<LlmResponseResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class DisabledModeLocalLlmClient : ILocalLlmClient
    {
        public Task<LlmResponseResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(LlmResponseResult.Failure(AiFallbackReason.AiDisabled, "AI is disabled.", 0));
    }
}
