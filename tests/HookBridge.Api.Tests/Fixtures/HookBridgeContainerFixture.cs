using Testcontainers.Kafka;
using Testcontainers.MongoDb;

namespace HookBridge.Api.Tests.Fixtures;

[CollectionDefinition("hookbridge-containers")]
public sealed class HookBridgeContainerCollection : ICollectionFixture<HookBridgeContainerFixture>
{
    public const string Name = "hookbridge-containers";
}

public sealed class HookBridgeContainerFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _mongoDbContainer = new MongoDbBuilder()
        .WithImage("mongo:7.0")
        .Build();

    private readonly KafkaContainer _kafkaContainer = new KafkaBuilder()
        .WithImage("confluentinc/cp-kafka:7.6.1")
        .Build();

    public string MongoConnectionString => _mongoDbContainer.GetConnectionString();
    public string KafkaBootstrapServers => _kafkaContainer.GetBootstrapAddress();

    public async Task InitializeAsync()
    {
        await _mongoDbContainer.StartAsync();
        await _kafkaContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _kafkaContainer.DisposeAsync().AsTask();
        await _mongoDbContainer.DisposeAsync().AsTask();
    }
}
