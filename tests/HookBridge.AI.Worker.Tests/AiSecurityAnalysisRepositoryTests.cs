using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using Moq;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiSecurityAnalysisRepositoryTests
{
    [Fact]
    public async Task InsertAsync_CallsMongoCollectionInsertOneAsyncAndNormalizesUtc()
    {
        var collection = new Mock<IMongoCollection<AiSecurityAnalysisResult>>();
        var repository = CreateRepository(collection.Object);
        var result = new AiSecurityAnalysisResult { EventId = "evt-1", GeneratedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), ReceivedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified), CreatedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) };

        await repository.InsertAsync(result);

        result.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        result.ReceivedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        collection.Verify(mongoCollection => mongoCollection.InsertOneAsync(result, It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("event")]
    [InlineData("correlation")]
    [InlineData("customer")]
    [InlineData("recent")]
    [InlineData("search")]
    public async Task QueryMethods_CallFindAsync(string queryType)
    {
        var collection = CreateCollectionReturning(new AiSecurityAnalysisResult { EventId = "evt-1", CorrelationId = "corr-1", CustomerId = "cust-1" });
        var repository = CreateRepository(collection.Object);

        IReadOnlyList<AiSecurityAnalysisResult> results;
        if (queryType == "event")
        {
            var result = await repository.GetByEventIdAsync("evt-1");
            results = result is null ? Array.Empty<AiSecurityAnalysisResult>() : new[] { result };
        }
        else
        {
            results = queryType switch
            {
                "correlation" => await repository.GetByCorrelationIdAsync("corr-1"),
                "customer" => await repository.GetByCustomerIdAsync("cust-1"),
                "recent" => await repository.GetRecentAsync(10),
                _ => await repository.SearchAsync(new AiSecurityAnalysisSearchRequestDto { CustomerId = "cust-1", RiskLevel = AiRiskLevel.High, IsSuspicious = true, Limit = 10 })
            };
        }

        results.Should().NotBeEmpty();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<AiSecurityAnalysisResult>>(), It.IsAny<FindOptions<AiSecurityAnalysisResult, AiSecurityAnalysisResult>>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }


    [Fact]
    public async Task GetRecentAsync_WhenLimitIsNotPositive_DoesNotQueryMongo()
    {
        var collection = new Mock<IMongoCollection<AiSecurityAnalysisResult>>();
        var repository = CreateRepository(collection.Object);

        var results = await repository.GetRecentAsync(0);

        results.Should().BeEmpty();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<AiSecurityAnalysisResult>>(), It.IsAny<FindOptions<AiSecurityAnalysisResult, AiSecurityAnalysisResult>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_NormalizesInvalidLimitToDefault()
    {
        var collection = CreateCollectionReturning(new AiSecurityAnalysisResult { EventId = "evt-1" });
        var repository = CreateRepository(collection.Object);

        var results = await repository.SearchAsync(new AiSecurityAnalysisSearchRequestDto { Limit = -5 });

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<AiSecurityAnalysisResult>>(),
            It.Is<FindOptions<AiSecurityAnalysisResult, AiSecurityAnalysisResult>>(options => options.Limit == 100 && options.Sort != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }



    [Theory]
    [InlineData("risk")]
    [InlineData("suspicious")]
    [InlineData("environment")]
    [InlineData("date-range")]
    public async Task SearchAsync_SupportsRequiredSecurityFilters(string scenario)
    {
        var collection = CreateCollectionReturning(new AiSecurityAnalysisResult { EventId = "evt-1", RiskLevel = "High", IsSuspicious = true, Environment = "qa", GeneratedAtUtc = DateTime.UtcNow });
        var repository = CreateRepository(collection.Object);
        var request = new AiSecurityAnalysisSearchRequestDto { Limit = 10 };
        if (scenario == "risk") request.RiskLevel = AiRiskLevel.High;
        if (scenario == "suspicious") request.IsSuspicious = true;
        if (scenario == "environment") request.Environment = "qa";
        if (scenario == "date-range")
        {
            request.FromUtc = new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc);
            request.ToUtc = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc);
        }

        var results = await repository.SearchAsync(request);

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<AiSecurityAnalysisResult>>(),
            It.Is<FindOptions<AiSecurityAnalysisResult, AiSecurityAnalysisResult>>(options => options.Limit == 10 && options.Sort != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void FromResponse_CopiesRequestMetadataAndNormalizesUtc()
    {
        var request = new AiSecurityAnalysisRequestDto
        {
            EventId = "evt-1",
            CorrelationId = "corr-1",
            CustomerId = "cust-1",
            CustomerIdType = "internal",
            SubscriptionId = "sub-1",
            EndpointId = "endpoint-1",
            Environment = "qa",
            Source = "HookBridge.API",
            EventType = "OrderCreated",
            TargetUrl = "https://customer.example.com/webhook",
            HttpMethod = "POST",
            SourceIp = "10.10.10.10",
            UserAgent = "HookBridgeTest/1.0",
            SignatureValidationFailed = true,
            AuthenticationFailed = true,
            PayloadSizeBytes = 42,
            ReceivedAtUtc = new DateTime(2026, 5, 14, 10, 30, 0, DateTimeKind.Utc)
        };
        var response = new AiSecurityAnalysisResponseDto
        {
            EventId = "evt-1",
            CorrelationId = "corr-1",
            IsSuspicious = true,
            SecurityRiskScore = 80,
            RiskLevel = AiRiskLevel.High,
            Summary = "summary",
            Recommendation = "recommendation",
            DetectedSecuritySignals = new[] { new AiSecuritySignalDto { SignalName = "SignatureValidationFailed" } },
            SuggestedAction = AiSecuritySuggestedAction.Quarantine,
            ConfidenceScore = 0.8,
            GeneratedAtUtc = new DateTime(2026, 5, 14, 10, 31, 0, DateTimeKind.Utc),
            Provider = "Ollama",
            Model = "llama3"
        };

        var result = AiSecurityAnalysisResult.FromResponse(response, request);

        result.CustomerId.Should().Be("cust-1");
        result.RiskLevel.Should().Be("High");
        result.SuggestedAction.Should().Be("Quarantine");
        result.DetectedSecuritySignals.Should().ContainSingle(signal => signal.SignalName == "SignatureValidationFailed");
        result.GeneratedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    private static AiSecurityAnalysisRepository CreateRepository(IMongoCollection<AiSecurityAnalysisResult> collection)
    {
        var provider = new Mock<IAiSecurityAnalysisCollectionProvider>();
        provider.Setup(collectionProvider => collectionProvider.GetCollection()).Returns(collection);
        return new AiSecurityAnalysisRepository(provider.Object);
    }

    private static Mock<IMongoCollection<AiSecurityAnalysisResult>> CreateCollectionReturning(AiSecurityAnalysisResult result)
    {
        var cursor = new Mock<IAsyncCursor<AiSecurityAnalysisResult>>();
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNext(It.IsAny<CancellationToken>())).Returns(true).Returns(false);
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true).ReturnsAsync(false);
        cursor.Setup(mongoCursor => mongoCursor.Current).Returns(new[] { result });

        var collection = new Mock<IMongoCollection<AiSecurityAnalysisResult>>();
        collection.Setup(mongoCollection => mongoCollection.FindAsync(It.IsAny<FilterDefinition<AiSecurityAnalysisResult>>(), It.IsAny<FindOptions<AiSecurityAnalysisResult, AiSecurityAnalysisResult>>(), It.IsAny<CancellationToken>())).ReturnsAsync(cursor.Object);
        return collection;
    }
}
