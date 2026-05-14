using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiMongoOptionsTests
{
    [Fact]
    public void Configure_BindsAiMongoOptionsFromConfiguration()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{AiMongoOptions.SectionName}:ConnectionString"] = "mongodb://localhost:27017",
            [$"{AiMongoOptions.SectionName}:DatabaseName"] = "hookbridge_ai",
            [$"{AiMongoOptions.SectionName}:AiAnalysisResultsCollectionName"] = "custom_results"
        });

        var options = CreateOptions(configuration);

        options.ConnectionString.Should().Be("mongodb://localhost:27017");
        options.DatabaseName.Should().Be("hookbridge_ai");
        options.AiAnalysisResultsCollectionName.Should().Be("custom_results");
    }

    [Fact]
    public void AiMongoOptions_UsesDefaultCollectionName()
    {
        var options = new AiMongoOptions();

        options.AiAnalysisResultsCollectionName.Should().Be("ai_analysis_results");
        options.WebhookFailureAnomalyDetectionResultsCollectionName.Should().Be("webhook_failure_anomaly_detection_results");
    }

    [Fact]
    public void Validate_WithMissingConnectionString_ThrowsOptionsValidationException()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{AiMongoOptions.SectionName}:ConnectionString"] = " ",
            [$"{AiMongoOptions.SectionName}:DatabaseName"] = "hookbridge_ai"
        });

        var act = () => CreateOptions(configuration);

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*AiMongo:ConnectionString is required.*");
    }

    [Fact]
    public void Validate_WithMissingDatabaseName_ThrowsOptionsValidationException()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{AiMongoOptions.SectionName}:ConnectionString"] = "mongodb://localhost:27017",
            [$"{AiMongoOptions.SectionName}:DatabaseName"] = " "
        });

        var act = () => CreateOptions(configuration);

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*AiMongo:DatabaseName is required.*");
    }

    [Fact]
    public void Validate_WithMissingCollectionName_ThrowsOptionsValidationException()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{AiMongoOptions.SectionName}:ConnectionString"] = "mongodb://localhost:27017",
            [$"{AiMongoOptions.SectionName}:DatabaseName"] = "hookbridge_ai",
            [$"{AiMongoOptions.SectionName}:AiAnalysisResultsCollectionName"] = " "
        });

        var act = () => CreateOptions(configuration);

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*AiMongo:AiAnalysisResultsCollectionName is required.*");
    }

    private static AiMongoOptions CreateOptions(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddAiMongoOptions(configuration);
        using var provider = services.BuildServiceProvider(validateScopes: true);
        return provider.GetRequiredService<IOptions<AiMongoOptions>>().Value;
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> settings)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }
}
