using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mappers;
using HookBridge.AI.Worker.Mongo;
using Moq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiAnomalyRecordTests
{
    [Fact]
    public void FromAnomalyEvent_MapsAllFieldsAndKeepsUtcDates()
    {
        var createdAt = DateTime.SpecifyKind(new DateTime(2026, 5, 14, 10, 16, 30), DateTimeKind.Utc);
        var storedAt = DateTime.SpecifyKind(new DateTime(2026, 5, 14, 10, 16, 31), DateTimeKind.Utc);
        var anomalyEvent = CreateEvent(createdAt);

        var record = AiAnomalyRecordMapper.FromAnomalyEvent(anomalyEvent, storedAt);

        record.AnomalyId.Should().Be("anm-1");
        record.EventId.Should().Be("evt-1");
        record.CorrelationId.Should().Be("corr-1");
        record.CustomerId.Should().Be("cust-1");
        record.CustomerIdType.Should().Be("MDM");
        record.SubscriptionId.Should().Be("sub-1");
        record.EndpointId.Should().Be("endpoint-1");
        record.TargetUrl.Should().Be("https://customer.example.com/webhook");
        record.Environment.Should().Be("qa");
        record.EventType.Should().Be("OrderCreated");
        record.AnomalyType.Should().Be(AiAnomalyType.RateLimitSpike.ToString());
        record.RiskLevel.Should().Be(AiRiskLevel.High.ToString());
        record.AnomalyScore.Should().Be(78);
        record.Summary.Should().Be("Summary");
        record.Recommendation.Should().Be("Recommendation");
        record.Source.Should().Be("HookBridge.AI.Worker");
        record.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        record.StoredAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task InsertAsync_WithValidRecord_InsertsAndReturnsSuccess()
    {
        var collection = CreateCollectionReturning(Array.Empty<AiAnomalyRecord>(), out _);
        var repository = CreateRepository(collection.Object);
        var record = CreateRecord();

        var result = await repository.InsertAsync(record);

        result.IsSuccess.Should().BeTrue();
        result.IsDuplicate.Should().BeFalse();
        result.AnomalyId.Should().Be(record.AnomalyId);
        record.StoredAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        collection.Verify(mongoCollection => mongoCollection.InsertOneAsync(
            record,
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InsertAsync_WithDuplicateAnomalyId_ReturnsDuplicateAndDoesNotInsert()
    {
        var existing = CreateRecord();
        existing.Id = "507f1f77bcf86cd799439011";
        var collection = CreateCollectionReturning(new[] { existing }, out _);
        var repository = CreateRepository(collection.Object);

        var result = await repository.InsertAsync(CreateRecord());

        result.IsSuccess.Should().BeFalse();
        result.IsDuplicate.Should().BeTrue();
        result.Id.Should().Be(existing.Id);
        collection.Verify(mongoCollection => mongoCollection.InsertOneAsync(
            It.IsAny<AiAnomalyRecord>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task InsertAsync_WithInvalidAnomalyScore_ReturnsFailure(int anomalyScore)
    {
        var collection = new Mock<IMongoCollection<AiAnomalyRecord>>();
        var repository = CreateRepository(collection.Object);
        var record = CreateRecord();
        record.AnomalyScore = anomalyScore;

        var result = await repository.InsertAsync(record);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("AnomalyScore");
    }

    [Fact]
    public async Task InsertAsync_WithInvalidTargetUrl_ReturnsFailure()
    {
        var collection = new Mock<IMongoCollection<AiAnomalyRecord>>();
        var repository = CreateRepository(collection.Object);
        var record = CreateRecord();
        record.TargetUrl = "not-a-url";

        var result = await repository.InsertAsync(record);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("TargetUrl");
    }

    [Fact]
    public async Task InsertAsync_WithMissingAnomalyId_ReturnsFailure()
    {
        var collection = new Mock<IMongoCollection<AiAnomalyRecord>>();
        var repository = CreateRepository(collection.Object);
        var record = CreateRecord();
        record.AnomalyId = " ";

        var result = await repository.InsertAsync(record);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("AnomalyId");
    }

    [Fact]
    public async Task GetByAnomalyIdAsync_ReturnsSingleRecord()
    {
        var collection = CreateCollectionReturning(CreateRecord(), out _);
        var repository = CreateRepository(collection.Object);

        var result = await repository.GetByAnomalyIdAsync("anm-1");

        result.Should().NotBeNull();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<AiAnomalyRecord>>(),
            It.Is<FindOptions<AiAnomalyRecord, AiAnomalyRecord>>(options => options.Limit == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByCustomerIdAsync_ReturnsMatchingRecords()
    {
        var collection = CreateCollectionReturning(new[] { CreateRecord() }, out _);
        var repository = CreateRepository(collection.Object);

        var results = await repository.GetByCustomerIdAsync("cust-1");

        results.Should().ContainSingle();
    }

    [Fact]
    public async Task GetByEndpointIdAsync_ReturnsMatchingRecords()
    {
        var collection = CreateCollectionReturning(new[] { CreateRecord() }, out _);
        var repository = CreateRepository(collection.Object);

        var results = await repository.GetByEndpointIdAsync("endpoint-1");

        results.Should().ContainSingle();
    }


    [Fact]
    public async Task GetByEventIdAsync_ReturnsMatchingRecords()
    {
        var collection = CreateCollectionReturning(new[] { CreateRecord() }, out _);
        var repository = CreateRepository(collection.Object);

        var results = await repository.GetByEventIdAsync("evt-1");

        results.Should().ContainSingle();
    }

    [Fact]
    public async Task GetBySubscriptionIdAsync_ReturnsMatchingRecords()
    {
        var collection = CreateCollectionReturning(new[] { CreateRecord() }, out _);
        var repository = CreateRepository(collection.Object);

        var results = await repository.GetBySubscriptionIdAsync("sub-1");

        results.Should().ContainSingle();
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsNewestRecordsWithLimit()
    {
        var collection = CreateCollectionReturning(new[] { CreateRecord() }, out _);
        var repository = CreateRepository(collection.Object);

        var results = await repository.GetRecentAsync(10);

        results.Should().ContainSingle();
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<AiAnomalyRecord>>(),
            It.Is<FindOptions<AiAnomalyRecord, AiAnomalyRecord>>(options => options.Limit == 10 && options.Sort != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_WithCustomerIdFilter_PaginatesAndSortsByCreatedAtDescending()
    {
        FilterDefinition<AiAnomalyRecord>? capturedFilter = null;
        var collection = CreateCollectionReturning(new[] { CreateRecord() }, out _, filter => capturedFilter = filter);
        var repository = CreateRepository(collection.Object);

        var results = await repository.SearchAsync(new AiAnomalyRecordSearchRequestDto
        {
            CustomerId = "cust-1",
            PageNumber = 2,
            PageSize = 25
        });

        results.Should().ContainSingle();
        Render(capturedFilter!).Should().Contain("customerId");
        collection.Verify(mongoCollection => mongoCollection.FindAsync(
            It.IsAny<FilterDefinition<AiAnomalyRecord>>(),
            It.Is<FindOptions<AiAnomalyRecord, AiAnomalyRecord>>(options => options.Skip == 25 && options.Limit == 25 && options.Sort != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_WithRiskLevelFilter_IncludesRiskLevel()
    {
        FilterDefinition<AiAnomalyRecord>? capturedFilter = null;
        var collection = CreateCollectionReturning(new[] { CreateRecord() }, out _, filter => capturedFilter = filter);
        var repository = CreateRepository(collection.Object);

        await repository.SearchAsync(new AiAnomalyRecordSearchRequestDto { RiskLevel = AiRiskLevel.High });

        Render(capturedFilter!).Should().Contain("riskLevel").And.Contain("High");
    }

    [Fact]
    public async Task SearchAsync_WithAnomalyTypeFilter_IncludesAnomalyType()
    {
        FilterDefinition<AiAnomalyRecord>? capturedFilter = null;
        var collection = CreateCollectionReturning(new[] { CreateRecord() }, out _, filter => capturedFilter = filter);
        var repository = CreateRepository(collection.Object);

        await repository.SearchAsync(new AiAnomalyRecordSearchRequestDto { AnomalyType = AiAnomalyType.RateLimitSpike });

        Render(capturedFilter!).Should().Contain("anomalyType").And.Contain("RateLimitSpike");
    }

    [Fact]
    public async Task SearchAsync_WithDateRangeFilter_IncludesCreatedAtUtcRange()
    {
        FilterDefinition<AiAnomalyRecord>? capturedFilter = null;
        var collection = CreateCollectionReturning(new[] { CreateRecord() }, out _, filter => capturedFilter = filter);
        var repository = CreateRepository(collection.Object);

        await repository.SearchAsync(new AiAnomalyRecordSearchRequestDto
        {
            FromUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 14), DateTimeKind.Utc),
            ToUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 15), DateTimeKind.Utc)
        });

        Render(capturedFilter!).Should().Contain("createdAtUtc");
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 0)]
    [InlineData(1, 501)]
    public async Task SearchAsync_WithInvalidPagination_Throws(int pageNumber, int pageSize)
    {
        var collection = new Mock<IMongoCollection<AiAnomalyRecord>>();
        var repository = CreateRepository(collection.Object);

        var act = () => repository.SearchAsync(new AiAnomalyRecordSearchRequestDto { PageNumber = pageNumber, PageSize = pageSize });

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreateAiAnomalyRecordIndexModels_ReturnsRequiredIndexes()
    {
        var indexes = AiMongoIndexInitializer.CreateAiAnomalyRecordIndexModels();

        indexes.Should().HaveCount(15);
        indexes.Select(index => index.Options.Name).Should().Contain(new[]
        {
            "idx_ai_anomaly_records_anomaly_id_unique",
            "idx_ai_anomaly_records_customer_id_created_at_desc",
            "idx_ai_anomaly_records_endpoint_id_created_at_desc",
            "idx_ai_anomaly_records_risk_level_created_at_desc"
        });
        indexes.Single(index => index.Options.Name == "idx_ai_anomaly_records_anomaly_id_unique").Options.Unique.Should().BeTrue();
    }

    private static AiAnomalyRecordRepository CreateRepository(IMongoCollection<AiAnomalyRecord> collection)
    {
        var provider = new Mock<IAiAnomalyRecordCollectionProvider>();
        provider.Setup(collectionProvider => collectionProvider.GetCollection()).Returns(collection);
        return new AiAnomalyRecordRepository(provider.Object);
    }

    private static Mock<IMongoCollection<AiAnomalyRecord>> CreateCollectionReturning(AiAnomalyRecord result, out Mock<IAsyncCursor<AiAnomalyRecord>> cursor)
        => CreateCollectionReturning(new[] { result }, out cursor);

    private static Mock<IMongoCollection<AiAnomalyRecord>> CreateCollectionReturning(
        IReadOnlyCollection<AiAnomalyRecord> results,
        out Mock<IAsyncCursor<AiAnomalyRecord>> cursor,
        Action<FilterDefinition<AiAnomalyRecord>>? captureFilter = null)
    {
        cursor = new Mock<IAsyncCursor<AiAnomalyRecord>>();
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNext(It.IsAny<CancellationToken>()))
            .Returns(true)
            .Returns(false);
        cursor.SetupSequence(mongoCursor => mongoCursor.MoveNextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        cursor.Setup(mongoCursor => mongoCursor.Current).Returns(results);

        var collection = new Mock<IMongoCollection<AiAnomalyRecord>>();
        collection.Setup(mongoCollection => mongoCollection.FindAsync(
                It.IsAny<FilterDefinition<AiAnomalyRecord>>(),
                It.IsAny<FindOptions<AiAnomalyRecord, AiAnomalyRecord>>(),
                It.IsAny<CancellationToken>()))
            .Callback<FilterDefinition<AiAnomalyRecord>, FindOptions<AiAnomalyRecord, AiAnomalyRecord>, CancellationToken>((filter, _, _) => captureFilter?.Invoke(filter))
            .ReturnsAsync(cursor.Object);

        return collection;
    }

    private static string Render(FilterDefinition<AiAnomalyRecord> filter)
    {
        var serializer = BsonSerializer.SerializerRegistry.GetSerializer<AiAnomalyRecord>();
        return filter.Render(new RenderArgs<AiAnomalyRecord>(serializer, BsonSerializer.SerializerRegistry)).ToJson();
    }

    private static AiAnomalyEventDto CreateEvent(DateTime createdAt) => new()
    {
        AnomalyId = "anm-1",
        EventId = "evt-1",
        CorrelationId = "corr-1",
        CustomerId = "cust-1",
        CustomerIdType = "MDM",
        SubscriptionId = "sub-1",
        EndpointId = "endpoint-1",
        TargetUrl = "https://customer.example.com/webhook",
        Environment = "qa",
        EventType = "OrderCreated",
        AnomalyType = AiAnomalyType.RateLimitSpike,
        RiskLevel = AiRiskLevel.High,
        AnomalyScore = 78,
        Summary = "Summary",
        Recommendation = "Recommendation",
        Source = "HookBridge.AI.Worker",
        CreatedAtUtc = createdAt
    };

    private static AiAnomalyRecord CreateRecord() => new()
    {
        AnomalyId = "anm-1",
        EventId = "evt-1",
        CorrelationId = "corr-1",
        CustomerId = "cust-1",
        CustomerIdType = "MDM",
        SubscriptionId = "sub-1",
        EndpointId = "endpoint-1",
        TargetUrl = "https://customer.example.com/webhook",
        Environment = "qa",
        EventType = "OrderCreated",
        AnomalyType = AiAnomalyType.RateLimitSpike.ToString(),
        RiskLevel = AiRiskLevel.High.ToString(),
        AnomalyScore = 78,
        Summary = "Summary",
        Recommendation = "Recommendation",
        Source = "HookBridge.AI.Worker",
        CreatedAtUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 14, 10, 16, 30), DateTimeKind.Utc),
        StoredAtUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 14, 10, 16, 31), DateTimeKind.Utc)
    };
}
