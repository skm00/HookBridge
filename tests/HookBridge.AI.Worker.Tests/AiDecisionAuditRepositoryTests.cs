using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using MongoDB.Driver;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiDecisionAuditRepositoryTests
{
    [Fact]
    public async Task InsertAsync_ValidatesRequiredAuditId()
    {
        var repository = CreateRepository();
        var record = CreateRecord();
        record.AuditId = " ";

        Func<Task> act = async () => await repository.InsertAsync(record);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("AuditId is required.*");
    }

    [Fact]
    public async Task InsertAsync_ValidatesDecisionType()
    {
        var repository = CreateRepository();
        var record = CreateRecord();
        record.DecisionType = AiDecisionAuditType.Unknown;

        Func<Task> act = async () => await repository.InsertAsync(record);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("DecisionType is required.*");
    }

    [Fact]
    public async Task InsertAsync_ValidatesUtcCreatedAt()
    {
        var repository = CreateRepository();
        var record = CreateRecord();
        record.CreatedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local);

        Func<Task> act = async () => await repository.InsertAsync(record);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*CreatedAtUtc*UTC*");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public async Task InsertAsync_ValidatesConfidenceScore(double confidenceScore)
    {
        var repository = CreateRepository();
        var record = CreateRecord();
        record.ConfidenceScore = confidenceScore;

        Func<Task> act = async () => await repository.InsertAsync(record);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithMessage("ConfidenceScore must be between 0 and 1.*");
    }

    [Fact]
    public async Task InsertAsync_InsertsValidRecord()
    {
        var collection = new Mock<IMongoCollection<AiDecisionAuditRecord>>();
        collection
            .Setup(item => item.InsertOneAsync(It.IsAny<AiDecisionAuditRecord>(), It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var repository = CreateRepository(collection);
        var record = CreateRecord();

        await repository.InsertAsync(record);

        collection.Verify(item => item.InsertOneAsync(record, It.IsAny<InsertOneOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ValidateSearch_AllowsValidUtcPaginationRange()
    {
        var request = new AiDecisionAuditSearchRequestDto
        {
            FromUtc = DateTime.UtcNow.AddHours(-1),
            ToUtc = DateTime.UtcNow,
            PageNumber = 2,
            PageSize = 500
        };

        Action act = () => AiDecisionAuditRepository.ValidateSearch(request);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0, 10, "PageNumber")]
    [InlineData(1, 0, "PageSize")]
    [InlineData(1, 501, "PageSize")]
    public void ValidateSearch_RejectsInvalidPagination(int pageNumber, int pageSize, string expectedMessage)
    {
        var request = new AiDecisionAuditSearchRequestDto { PageNumber = pageNumber, PageSize = pageSize };

        Action act = () => AiDecisionAuditRepository.ValidateSearch(request);

        act.Should().Throw<ArgumentException>().WithMessage($"*{expectedMessage}*");
    }

    [Fact]
    public void ValidateSearch_RejectsNonUtcDates()
    {
        var request = new AiDecisionAuditSearchRequestDto { FromUtc = DateTime.Now, PageNumber = 1, PageSize = 50 };

        Action act = () => AiDecisionAuditRepository.ValidateSearch(request);

        act.Should().Throw<ArgumentException>().WithMessage("*FromUtc*UTC*");
    }

    [Fact]
    public void ValidateSearch_RejectsToUtcBeforeFromUtc()
    {
        var now = DateTime.UtcNow;
        var request = new AiDecisionAuditSearchRequestDto { FromUtc = now, ToUtc = now.AddMinutes(-1), PageNumber = 1, PageSize = 50 };

        Action act = () => AiDecisionAuditRepository.ValidateSearch(request);

        act.Should().Throw<ArgumentException>().WithMessage("ToUtc must be greater than FromUtc.*");
    }

    private static AiDecisionAuditRepository CreateRepository(Mock<IMongoCollection<AiDecisionAuditRecord>>? collection = null)
    {
        collection ??= new Mock<IMongoCollection<AiDecisionAuditRecord>>();
        var provider = new Mock<IAiDecisionAuditRecordCollectionProvider>();
        provider.Setup(item => item.GetCollection()).Returns(collection.Object);
        return new AiDecisionAuditRepository(provider.Object);
    }

    private static AiDecisionAuditRecord CreateRecord() => new()
    {
        AuditId = "aud_1",
        EventId = "evt_1",
        DecisionType = AiDecisionAuditType.RetryDecision,
        ConfidenceScore = 0.5,
        CreatedAtUtc = DateTime.UtcNow
    };
}
