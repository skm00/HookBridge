using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using Moq;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Tests;

public sealed class ObservabilityAgentResultRepositoryTests
{
    [Fact]
    public async Task InsertAsync_CallsMongoCollectionInsertOneAsyncAndNormalizesUtc()
    {
        var collection = new Mock<IMongoCollection<ObservabilityAgentResult>>();
        var repository = CreateRepository(collection.Object);
        var result = CreateResult();
        result.GeneratedAtUtc = DateTime.SpecifyKind(result.GeneratedAtUtc, DateTimeKind.Unspecified);

        await repository.InsertAsync(result);

        result.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        collection.Verify(mongoCollection => mongoCollection.InsertOneAsync(result, It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByEventIdAsync_ReturnsLatestResult()
    {
        var collection = CreateCollectionReturning(CreateResult());
        var repository = CreateRepository(collection.Object);

        var result = await repository.GetByEventIdAsync("evt-1");

        result.Should().NotBeNull();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<ObservabilityAgentResult>>(), It.Is<FindOptions<ObservabilityAgentResult, ObservabilityAgentResult>>(options => options.Limit == 1 && options.Sort != null), It.IsAny<CancellationToken>()), Times.Once);
    }


    [Theory]
    [InlineData("environment")]
    [InlineData("service")]
    public async Task GetByDimensionAsync_ReturnsResults(string mode)
    {
        var collection = CreateCollectionReturning(CreateResult());
        var repository = CreateRepository(collection.Object);

        var results = mode == "environment"
            ? await repository.GetByEnvironmentAsync("qa")
            : await repository.GetByServiceNameAsync("HookBridge.AI.Worker");

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<ObservabilityAgentResult>>(), It.IsAny<FindOptions<ObservabilityAgentResult, ObservabilityAgentResult>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("environment")]
    [InlineData("service")]
    [InlineData("status")]
    public async Task SearchAsync_FiltersByExpectedFields(string mode)
    {
        var collection = CreateCollectionReturning(CreateResult());
        var repository = CreateRepository(collection.Object);
        var request = mode switch
        {
            "environment" => new ObservabilityAgentSearchRequestDto { Environment = "qa" },
            "service" => new ObservabilityAgentSearchRequestDto { ServiceName = "HookBridge.AI.Worker" },
            _ => new ObservabilityAgentSearchRequestDto { ObservabilityStatus = ObservabilityStatus.Critical }
        };

        var results = await repository.SearchAsync(request);

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<ObservabilityAgentResult>>(), It.IsAny<FindOptions<ObservabilityAgentResult, ObservabilityAgentResult>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void CreateObservabilityAgentResultIndexModels_ReturnsRequiredIndexes()
    {
        var indexes = AiMongoIndexInitializer.CreateObservabilityAgentResultIndexModels();
        indexes.Should().HaveCount(10);
        indexes.Select(index => index.Options.Name).Should().BeEquivalentTo(
            "idx_observability_agent_results_event_id",
            "idx_observability_agent_results_correlation_id",
            "idx_observability_agent_results_environment",
            "idx_observability_agent_results_service_name",
            "idx_observability_agent_results_customer_id",
            "idx_observability_agent_results_subscription_id",
            "idx_observability_agent_results_endpoint_id",
            "idx_observability_agent_results_status",
            "idx_observability_agent_results_risk_level",
            "idx_observability_agent_results_generated_at_utc_desc");
    }

    private static ObservabilityAgentResultRepository CreateRepository(IMongoCollection<ObservabilityAgentResult> collection)
    {
        var provider = new Mock<IObservabilityAgentResultCollectionProvider>();
        provider.Setup(collectionProvider => collectionProvider.GetCollection()).Returns(collection);
        return new ObservabilityAgentResultRepository(provider.Object);
    }

    private static Mock<IMongoCollection<ObservabilityAgentResult>> CreateCollectionReturning(ObservabilityAgentResult result)
    {
        var cursor = new Mock<IAsyncCursor<ObservabilityAgentResult>>();
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        cursor.Setup(mongoCursor => mongoCursor.Current).Returns(new[] { result });

        var collection = new Mock<IMongoCollection<ObservabilityAgentResult>>();
        collection.Setup(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<ObservabilityAgentResult>>(), It.IsAny<FindOptions<ObservabilityAgentResult, ObservabilityAgentResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cursor.Object);
        return collection;
    }

    private static ObservabilityAgentResult CreateResult() => new()
    {
        EventId = "evt-1",
        CorrelationId = "corr-1",
        Environment = "qa",
        ServiceName = "HookBridge.AI.Worker",
        CustomerId = "cust-1",
        SubscriptionId = "sub-1",
        EndpointId = "endpoint-1",
        ObservabilityStatus = ObservabilityStatus.Critical,
        RiskLevel = AiRiskLevel.Critical,
        EvaluationWindowFromUtc = DateTime.UtcNow.AddMinutes(-15),
        EvaluationWindowToUtc = DateTime.UtcNow,
        CreatedAtUtc = DateTime.UtcNow,
        GeneratedAtUtc = DateTime.UtcNow
    };
}
