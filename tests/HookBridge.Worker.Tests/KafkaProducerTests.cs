using HookBridge.Infrastructure.Configuration;
using HookBridge.Infrastructure.Services.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.Worker.Tests;

public sealed class KafkaProducerTests
{
    [Fact]
    public async Task ProduceAsync_ThrowsWhenTopicIsEmpty()
    {
        using var producer = CreateProducer();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            producer.ProduceAsync("", "tenant-1", new { value = 1 }));
    }

    [Fact]
    public async Task ProduceAsync_ThrowsWhenKeyIsEmpty()
    {
        using var producer = CreateProducer();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            producer.ProduceAsync("webhook-events", "", new { value = 1 }));
    }

    [Fact]
    public async Task ProduceAsync_ThrowsWhenMessageIsNull()
    {
        using var producer = CreateProducer();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            producer.ProduceAsync<object>("webhook-events", "tenant-1", null!));
    }

    private static KafkaProducer CreateProducer()
    {
        var settings = Options.Create(new KafkaSettings
        {
            BootstrapServers = "localhost:9092",
            MessageTimeoutMs = 10000,
            SecurityProtocol = "Plaintext",
            SaslMechanism = string.Empty,
            SaslUsername = null,
            SaslPassword = null,
        });

        return new KafkaProducer(settings, NullLogger<KafkaProducer>.Instance);
    }
}
