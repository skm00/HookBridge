using FluentAssertions;
using HookBridge.AI.Worker.Mongo;
using Moq;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiDashboardRepositoryQueryTests
{
    [Fact]
    public async Task AiAnalysisDashboardQueries_GroupAverageAndMapRecentFindings()
    {
        var createdAtUtc = new DateTime(2026, 5, 14, 10, 0, 0, DateTimeKind.Utc);
        var collection = CreateCollectionReturning(
            new[]
            {
                new AiAnalysisResult
                {
                    Id = "66460f4f9f1e2a5a12345670",
                    EventId = "evt-1",
                    CorrelationId = "corr-1",
                    CustomerId = "cust-1",
                    CustomerIdType = "external",
                    SubscriptionId = "sub-1",
                    EndpointId = "endpoint-1",
                    Environment = "qa",
                    EventType = "OrderCreated",
                    AiSummary = "Rate limited.",
                    RootCause = " ",
                    RiskLevel = "High",
                    SuggestedRetryAction = "RetryWithBackoff",
                    ConfidenceScore = 0.7,
                    CreatedAtUtc = createdAtUtc
                },
                new AiAnalysisResult
                {
                    Id = "66460f4f9f1e2a5a12345671",
                    EventId = "evt-2",
                    RootCause = "Timeout spike",
                    RiskLevel = " ",
                    SuggestedRetryAction = " ",
                    ConfidenceScore = 0.9,
                    CreatedAtUtc = createdAtUtc.AddMinutes(-1)
                }
            });
        var repository = CreateAnalysisRepository(collection.Object);
        var filter = CreateFullFilter();

        var riskCounts = await repository.CountByRiskLevelAsync(filter);
        var retryCounts = await repository.CountByRetryActionAsync(filter);
        var averageConfidence = await repository.GetAverageConfidenceScoreAsync(filter);
        var findings = await repository.GetRecentFindingsAsync(filter, 10);

        riskCounts.Should().ContainKey("High").WhoseValue.Should().Be(1);
        riskCounts.Should().ContainKey("Unknown").WhoseValue.Should().Be(1);
        retryCounts.Should().ContainKey("RetryWithBackoff").WhoseValue.Should().Be(1);
        retryCounts.Should().ContainKey("Unknown").WhoseValue.Should().Be(1);
        averageConfidence.Should().Be(0.8);
        findings.Should().HaveCount(2);
        findings[0].Title.Should().Be("AI analysis completed");
        findings[1].Title.Should().Be("Timeout spike");
        findings[0].FindingType.Should().Be("Analysis");
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<AiAnalysisResult>>(),
            It.Is<FindOptions<AiAnalysisResult, AiAnalysisResult>>(options => options.Limit == 10 && options.Sort != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AiAnalysisDashboardQueries_ReturnZeroAndSkipMongoForEmptyPaths()
    {
        var collection = CreateCollectionReturning(Array.Empty<AiAnalysisResult>());
        var repository = CreateAnalysisRepository(collection.Object);
        var filter = CreateMinimalFilter();

        var averageConfidence = await repository.GetAverageConfidenceScoreAsync(filter);
        var findings = await repository.GetRecentFindingsAsync(filter, 0);

        averageConfidence.Should().Be(0);
        findings.Should().BeEmpty();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<AiAnalysisResult>>(),
            It.IsAny<FindOptions<AiAnalysisResult, AiAnalysisResult>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AiAnomalyDashboardQueries_GroupAndMapRecentFindings()
    {
        var collection = CreateCollectionReturning(
            new[]
            {
                new AiAnomalyRecord
                {
                    Id = "66460f4f9f1e2a5a12345672",
                    EventId = "evt-1",
                    CorrelationId = "corr-1",
                    CustomerId = "cust-1",
                    CustomerIdType = "external",
                    SubscriptionId = "sub-1",
                    EndpointId = "endpoint-1",
                    Environment = "qa",
                    EventType = "OrderCreated",
                    AnomalyType = "RateLimitSpike",
                    RiskLevel = "Critical",
                    Summary = "429s increased.",
                    Recommendation = "RetryWithBackoff",
                    CreatedAtUtc = new DateTime(2026, 5, 14, 10, 0, 0, DateTimeKind.Utc)
                },
                new AiAnomalyRecord
                {
                    Id = "66460f4f9f1e2a5a12345673",
                    CustomerId = "cust-1",
                    AnomalyType = " ",
                    RiskLevel = " ",
                    Summary = "Unknown anomaly.",
                    Recommendation = "Manual review.",
                    CreatedAtUtc = new DateTime(2026, 5, 14, 9, 59, 0, DateTimeKind.Utc)
                }
            });
        var repository = CreateAnomalyRepository(collection.Object);
        var filter = CreateFullFilter();

        var riskCounts = await repository.CountByRiskLevelAsync(filter);
        var anomalyCounts = await repository.CountByAnomalyTypeAsync(filter);
        var findings = await repository.GetRecentFindingsAsync(filter, 10);

        riskCounts.Should().ContainKey("Critical").WhoseValue.Should().Be(1);
        riskCounts.Should().ContainKey("Unknown").WhoseValue.Should().Be(1);
        anomalyCounts.Should().ContainKey("RateLimitSpike").WhoseValue.Should().Be(1);
        anomalyCounts.Should().ContainKey("Unknown").WhoseValue.Should().Be(1);
        findings.Should().HaveCount(2);
        findings[0].Title.Should().Be("RateLimitSpike detected");
        findings[1].Title.Should().Be("Unknown detected");
        findings[0].FindingType.Should().Be("Anomaly");
    }

    [Fact]
    public async Task AiAnomalyDashboardQueries_ReturnEmptyRecentFindingsForNonPositiveLimit()
    {
        var collection = new Mock<IMongoCollection<AiAnomalyRecord>>();
        var repository = CreateAnomalyRepository(collection.Object);

        var findings = await repository.GetRecentFindingsAsync(CreateMinimalFilter(), 0);

        findings.Should().BeEmpty();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<AiAnomalyRecord>>(),
            It.IsAny<FindOptions<AiAnomalyRecord, AiAnomalyRecord>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AiSecurityDashboardQueries_GroupAverageAndMapSuspiciousTitles()
    {
        var collection = CreateCollectionReturning(
            new[]
            {
                new AiSecurityAnalysisResult
                {
                    Id = "66460f4f9f1e2a5a12345674",
                    EventId = "evt-1",
                    CorrelationId = "corr-1",
                    CustomerId = "cust-1",
                    CustomerIdType = "external",
                    SubscriptionId = "sub-1",
                    EndpointId = "endpoint-1",
                    Environment = "qa",
                    EventType = "OrderCreated",
                    IsSuspicious = true,
                    RiskLevel = "High",
                    Summary = "Signature failures increased.",
                    SuggestedAction = "Quarantine",
                    ConfidenceScore = 0.6,
                    GeneratedAtUtc = new DateTime(2026, 5, 14, 10, 0, 0, DateTimeKind.Utc)
                },
                new AiSecurityAnalysisResult
                {
                    Id = "66460f4f9f1e2a5a12345675",
                    EventId = "evt-2",
                    IsSuspicious = false,
                    RiskLevel = " ",
                    Summary = "No suspicious activity.",
                    SuggestedAction = "None",
                    ConfidenceScore = 0.8,
                    GeneratedAtUtc = new DateTime(2026, 5, 14, 9, 59, 0, DateTimeKind.Utc)
                }
            });
        var repository = CreateSecurityRepository(collection.Object);
        var filter = CreateFullFilter();

        var riskCounts = await repository.CountByRiskLevelAsync(filter);
        var averageConfidence = await repository.GetAverageConfidenceScoreAsync(filter);
        var findings = await repository.GetRecentFindingsAsync(filter, 10);

        riskCounts.Should().ContainKey("High").WhoseValue.Should().Be(1);
        riskCounts.Should().ContainKey("Unknown").WhoseValue.Should().Be(1);
        averageConfidence.Should().Be(0.7);
        findings.Should().HaveCount(2);
        findings[0].Title.Should().Be("Suspicious webhook activity detected");
        findings[1].Title.Should().Be("Security analysis completed");
    }

    [Fact]
    public async Task AiSecurityDashboardQueries_ReturnZeroAndSkipMongoForEmptyPaths()
    {
        var collection = CreateCollectionReturning(Array.Empty<AiSecurityAnalysisResult>());
        var repository = CreateSecurityRepository(collection.Object);
        var filter = CreateMinimalFilter();

        var averageConfidence = await repository.GetAverageConfidenceScoreAsync(filter);
        var findings = await repository.GetRecentFindingsAsync(filter, 0);

        averageConfidence.Should().Be(0);
        findings.Should().BeEmpty();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<AiSecurityAnalysisResult>>(),
            It.IsAny<FindOptions<AiSecurityAnalysisResult, AiSecurityAnalysisResult>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CustomerEndpointRiskDashboardQueries_CountHealthAndHighRiskEndpoints()
    {
        var collection = CreateCollectionReturning(
            new[]
            {
                new CustomerEndpointRiskScoreResult
                {
                    CustomerId = "cust-1",
                    CustomerIdType = "external",
                    SubscriptionId = "sub-1",
                    EndpointId = "endpoint-1",
                    Environment = "qa",
                    RiskLevel = "High",
                    HealthStatus = "Healthy",
                    CalculatedAtUtc = new DateTime(2026, 5, 14, 10, 0, 0, DateTimeKind.Utc)
                },
                new CustomerEndpointRiskScoreResult
                {
                    CustomerId = "cust-2",
                    RiskLevel = "Critical",
                    HealthStatus = " ",
                    CalculatedAtUtc = new DateTime(2026, 5, 14, 9, 59, 0, DateTimeKind.Utc)
                }
            });
        collection
            .Setup(mongoCollection => mongoCollection.CountDocumentsAsync(
                It.IsAny<FilterDefinition<CustomerEndpointRiskScoreResult>>(),
                It.IsAny<CountOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(2L);
        var repository = CreateRiskScoreRepository(collection.Object);
        var filter = CreateFullFilter();

        var highRiskEndpoints = await repository.CountHighRiskEndpointsAsync(filter);
        var healthCounts = await repository.CountByHealthStatusAsync(filter);

        highRiskEndpoints.Should().Be(2);
        healthCounts.Should().ContainKey("Healthy").WhoseValue.Should().Be(1);
        healthCounts.Should().ContainKey("Unknown").WhoseValue.Should().Be(1);
        collection.Verify(mongoCollection => mongoCollection.CountDocumentsAsync(
            It.IsAny<FilterDefinition<CustomerEndpointRiskScoreResult>>(),
            It.IsAny<CountOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AiDashboardQueryFilter CreateFullFilter()
        => new()
        {
            Environment = "qa",
            CustomerId = "cust-1",
            CustomerIdType = "external",
            SubscriptionId = "sub-1",
            EndpointId = "endpoint-1",
            EventType = "OrderCreated",
            FromUtc = new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc)
        };

    private static AiDashboardQueryFilter CreateMinimalFilter()
        => new()
        {
            FromUtc = new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc)
        };

    private static AiAnalysisResultRepository CreateAnalysisRepository(IMongoCollection<AiAnalysisResult> collection)
    {
        var provider = new Mock<IAiAnalysisResultCollectionProvider>();
        provider.Setup(collectionProvider => collectionProvider.GetCollection()).Returns(collection);
        return new AiAnalysisResultRepository(provider.Object);
    }

    private static AiAnomalyRecordRepository CreateAnomalyRepository(IMongoCollection<AiAnomalyRecord> collection)
    {
        var provider = new Mock<IAiAnomalyRecordCollectionProvider>();
        provider.Setup(collectionProvider => collectionProvider.GetCollection()).Returns(collection);
        return new AiAnomalyRecordRepository(provider.Object);
    }

    private static AiSecurityAnalysisRepository CreateSecurityRepository(IMongoCollection<AiSecurityAnalysisResult> collection)
    {
        var provider = new Mock<IAiSecurityAnalysisCollectionProvider>();
        provider.Setup(collectionProvider => collectionProvider.GetCollection()).Returns(collection);
        return new AiSecurityAnalysisRepository(provider.Object);
    }

    private static CustomerEndpointRiskScoreRepository CreateRiskScoreRepository(IMongoCollection<CustomerEndpointRiskScoreResult> collection)
    {
        var provider = new Mock<ICustomerEndpointRiskScoreCollectionProvider>();
        provider.Setup(collectionProvider => collectionProvider.GetCollection()).Returns(collection);
        return new CustomerEndpointRiskScoreRepository(provider.Object);
    }

    private static Mock<IMongoCollection<TDocument>> CreateCollectionReturning<TDocument>(IReadOnlyCollection<TDocument> results)
    {
        var collection = new Mock<IMongoCollection<TDocument>>();
        collection
            .Setup(mongoCollection => mongoCollection.FindAsync(
                It.IsAny<FilterDefinition<TDocument>>(),
                It.IsAny<FindOptions<TDocument, TDocument>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateCursor(results).Object);
        return collection;
    }

    private static Mock<IAsyncCursor<TDocument>> CreateCursor<TDocument>(IReadOnlyCollection<TDocument> results)
    {
        var cursor = new Mock<IAsyncCursor<TDocument>>();
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNext(It.IsAny<CancellationToken>()))
            .Returns(true)
            .Returns(false);
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        cursor.Setup(mongoCursor => mongoCursor.Current).Returns(results);
        return cursor;
    }
}
