using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiKafkaOptionsTests
{
    [Fact]
    public void Configure_BindsAiKafkaOptionsFromConfiguration()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{AiKafkaOptions.SectionName}:BootstrapServers"] = "broker-1:9092,broker-2:9092",
            [$"{AiKafkaOptions.SectionName}:SecurityProtocol"] = "SaslSsl",
            [$"{AiKafkaOptions.SectionName}:SaslMechanism"] = "Plain",
            [$"{AiKafkaOptions.SectionName}:SaslUsername"] = "user",
            [$"{AiKafkaOptions.SectionName}:SaslPassword"] = "password",
            [$"{AiKafkaOptions.SectionName}:AiAnalysisTopic"] = "hookbridge.ai.analysis",
            [$"{AiKafkaOptions.SectionName}:ConsumerGroupId"] = "hookbridge-ai-tests",
            [$"{AiKafkaOptions.SectionName}:EnableAutoCommit"] = "true",
        });

        var options = CreateOptions(configuration);

        options.BootstrapServers.Should().Be("broker-1:9092,broker-2:9092");
        options.SecurityProtocol.Should().Be("SaslSsl");
        options.SaslMechanism.Should().Be("Plain");
        options.SaslUsername.Should().Be("user");
        options.SaslPassword.Should().Be("password");
        options.AiAnalysisTopic.Should().Be("hookbridge.ai.analysis");
        options.ConsumerGroupId.Should().Be("hookbridge-ai-tests");
        options.EnableAutoCommit.Should().BeTrue();
    }

    [Fact]
    public void AiKafkaOptions_ProvidesDefaultAnalysisTopic()
    {
        var options = new AiKafkaOptions();

        options.AiAnalysisTopic.Should().Be(AiKafkaTopics.Analysis);
        options.AiAnalysisTopic.Should().Be("hookbridge.ai.analysis");
    }

    [Fact]
    public void AiKafkaTopics_Analysis_HasExpectedValue()
    {
        AiKafkaTopics.Analysis.Should().Be("hookbridge.ai.analysis");
    }


    [Fact]
    public void AiKafkaTopics_EndpointRiskScore_HasExpectedValue()
    {
        AiKafkaTopics.EndpointRiskScore.Should().Be("hookbridge.ai.endpoint-risk-score");
        new AiKafkaOptions().CustomerEndpointRiskScoreTopic.Should().Be(AiKafkaTopics.EndpointRiskScore);
    }

    [Fact]
    public void Validate_WhenTopicMissing_ThrowsOptionsValidationException()
    {
        var settings = ValidSettings();
        settings[$"{AiKafkaOptions.SectionName}:AiAnalysisTopic"] = " ";
        var configuration = BuildConfiguration(settings);

        var act = () => CreateOptions(configuration);

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*AiKafka:AiAnalysisTopic is required.*");
    }

    [Fact]
    public void Validate_WhenBootstrapServersMissing_ThrowsOptionsValidationException()
    {
        var settings = ValidSettings();
        settings[$"{AiKafkaOptions.SectionName}:BootstrapServers"] = " ";
        var configuration = BuildConfiguration(settings);

        var act = () => CreateOptions(configuration);

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*AiKafka:BootstrapServers is required.*");
    }

    private static Dictionary<string, string?> ValidSettings()
        => new()
        {
            [$"{AiKafkaOptions.SectionName}:BootstrapServers"] = "localhost:9092",
            [$"{AiKafkaOptions.SectionName}:SecurityProtocol"] = "Plaintext",
            [$"{AiKafkaOptions.SectionName}:AiAnalysisTopic"] = "hookbridge.ai.analysis",
            [$"{AiKafkaOptions.SectionName}:ConsumerGroupId"] = "hookbridge-ai-worker",
            [$"{AiKafkaOptions.SectionName}:EnableAutoCommit"] = "false",
        };

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> settings)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    private static AiKafkaOptions CreateOptions(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddAiKafkaOptions(configuration);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<AiKafkaOptions>>().Value;
    }
}
