using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Mongo;

public sealed class AiMongoIndexInitializer : IHostedService
{
    private readonly IAiAnalysisResultCollectionProvider _collectionProvider;
    private readonly ILogger<AiMongoIndexInitializer> _logger;

    public AiMongoIndexInitializer(
        IAiAnalysisResultCollectionProvider collectionProvider,
        ILogger<AiMongoIndexInitializer> logger)
    {
        _collectionProvider = collectionProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var collection = _collectionProvider.GetCollection();
        var indexModels = CreateIndexModels();

        await collection.Indexes.CreateManyAsync(indexModels, cancellationToken);
        _logger.LogInformation("MongoDB AI analysis result indexes are ready.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public static IReadOnlyList<CreateIndexModel<AiAnalysisResult>> CreateIndexModels()
    {
        return new[]
        {
            new CreateIndexModel<AiAnalysisResult>(
                Builders<AiAnalysisResult>.IndexKeys.Ascending(result => result.EventId),
                new CreateIndexOptions { Name = "idx_ai_analysis_results_event_id" }),
            new CreateIndexModel<AiAnalysisResult>(
                Builders<AiAnalysisResult>.IndexKeys.Ascending(result => result.CorrelationId),
                new CreateIndexOptions { Name = "idx_ai_analysis_results_correlation_id" }),
            new CreateIndexModel<AiAnalysisResult>(
                Builders<AiAnalysisResult>.IndexKeys.Descending(result => result.CreatedAtUtc),
                new CreateIndexOptions { Name = "idx_ai_analysis_results_created_at_utc_desc" })
        };
    }
}
