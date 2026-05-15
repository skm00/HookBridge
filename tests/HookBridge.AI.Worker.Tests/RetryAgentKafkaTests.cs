using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Kafka;

namespace HookBridge.AI.Worker.Tests;

public sealed class RetryAgentKafkaTests
{
    [Fact]
    public void AiKafkaTopics_RetryAgent_HasExpectedValue()
    {
        AiKafkaTopics.RetryAgent.Should().Be("hookbridge.ai.retry-agent");
        new AiKafkaOptions().RetryAgentTopic.Should().Be(AiKafkaTopics.RetryAgent);
    }
}
