using FluentAssertions;
using HookBridge.AI.Worker.Mongo;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiMongoIndexInitializerTests
{
    [Fact]
    public void CreateIndexModels_ReturnsEventCorrelationAndCreatedAtIndexes()
    {
        var indexModels = AiMongoIndexInitializer.CreateIndexModels();

        indexModels.Should().HaveCount(3);
        indexModels.Select(model => model.Options.Name).Should().BeEquivalentTo(
            "idx_ai_analysis_results_event_id",
            "idx_ai_analysis_results_correlation_id",
            "idx_ai_analysis_results_created_at_utc_desc");
    }

    [Fact]
    public async Task StartAsync_CreatesConfiguredIndexesOnCollection()
    {
        var indexManager = new Mock<IMongoIndexManager<AiAnalysisResult>>();
        var collection = new Mock<IMongoCollection<AiAnalysisResult>>();
        collection.SetupGet(mongoCollection => mongoCollection.Indexes).Returns(indexManager.Object);
        var provider = new Mock<IAiAnalysisResultCollectionProvider>();
        provider.Setup(collectionProvider => collectionProvider.GetCollection()).Returns(collection.Object);
        var initializer = new AiMongoIndexInitializer(provider.Object, NullLogger<AiMongoIndexInitializer>.Instance);

        await initializer.StartAsync(CancellationToken.None);

        indexManager.Verify(manager => manager.CreateManyAsync(
            It.Is<IEnumerable<CreateIndexModel<AiAnalysisResult>>>(models => models.Count() == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
