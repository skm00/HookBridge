using HookBridge.Infrastructure.Configuration;
using HookBridge.Shared.Constants;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HookBridge.Worker.Tests;

public sealed class KafkaConfigurationTests
{
    [Fact]
    public void KafkaSettings_BindsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:BootstrapServers"] = "localhost:9092",
                ["Kafka:SecurityProtocol"] = "Plaintext",
                ["Kafka:SaslMechanism"] = string.Empty,
                ["Kafka:SaslUsername"] = string.Empty,
                ["Kafka:SaslPassword"] = string.Empty,
                ["Kafka:ConsumerGroupId"] = "hookbridge-worker",
                ["Kafka:EnableAutoCommit"] = "false",
                ["Kafka:MessageTimeoutMs"] = "10000",
            })
            .Build();

        var settings = config.GetSection("Kafka").Get<KafkaSettings>();

        Assert.NotNull(settings);
        Assert.Equal("localhost:9092", settings!.BootstrapServers);
        Assert.Equal("Plaintext", settings.SecurityProtocol);
        Assert.Equal(string.Empty, settings.SaslMechanism);
        Assert.Equal(string.Empty, settings.SaslUsername);
        Assert.Equal(string.Empty, settings.SaslPassword);
        Assert.Equal("hookbridge-worker", settings.ConsumerGroupId);
        Assert.False(settings.EnableAutoCommit);
        Assert.Equal(10000, settings.MessageTimeoutMs);
    }

    [Fact]
    public void KafkaTopicConstants_AreCorrect()
    {
        Assert.Equal("webhook-events", KafkaTopics.WebhookEvents);
        Assert.Equal("webhook-retry", KafkaTopics.WebhookRetry);
        Assert.Equal("webhook-dlq", KafkaTopics.WebhookDlq);
    }
}
