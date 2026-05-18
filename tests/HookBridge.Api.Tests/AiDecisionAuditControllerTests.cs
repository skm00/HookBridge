using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using HookBridge.Api.Controllers;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace HookBridge.Api.Tests;

public sealed class AiDecisionAuditControllerTests
{
    [Fact]
    public async Task GetByAuditIdAsync_Returns200WhenRecordExists()
    {
        var controller = new AiDecisionAuditController(new FakeRepository(CreateRecord()), NullLogger<AiDecisionAuditController>.Instance);

        var result = await controller.GetByAuditIdAsync("aud_1", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ApiResponse<AiDecisionAuditResponseDto>>().Subject;
        response.Data!.AuditId.Should().Be("aud_1");
    }

    [Fact]
    public async Task GetByAuditIdAsync_Returns400WhenAuditIdMissing()
    {
        var controller = new AiDecisionAuditController(new FakeRepository(CreateRecord()), NullLogger<AiDecisionAuditController>.Instance);

        var result = await controller.GetByAuditIdAsync(" ", CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetByAuditIdAsync_Returns404WhenMissing()
    {
        var controller = new AiDecisionAuditController(new FakeRepository(null), NullLogger<AiDecisionAuditController>.Instance);

        var result = await controller.GetByAuditIdAsync("aud_missing", CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }


    [Fact]
    public async Task SearchAsync_Returns200ForValidFilters()
    {
        var controller = new AiDecisionAuditController(new FakeRepository(CreateRecord()), NullLogger<AiDecisionAuditController>.Instance);

        var result = await controller.SearchAsync(new AiDecisionAuditSearchRequestDto { EventId = "evt_1", PageNumber = 1, PageSize = 50 }, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ApiResponse<IReadOnlyList<AiDecisionAuditResponseDto>>>().Subject;
        response.Data.Should().ContainSingle(item => item.EventId == "evt_1");
    }

    [Fact]
    public async Task SearchAsync_Returns400ForInvalidFilters()
    {
        var controller = new AiDecisionAuditController(new FakeRepository(CreateRecord()), NullLogger<AiDecisionAuditController>.Instance);

        var result = await controller.SearchAsync(new AiDecisionAuditSearchRequestDto { PageNumber = 0, PageSize = 50 }, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SearchAsync_Returns500ForUnexpectedErrors()
    {
        var controller = new AiDecisionAuditController(new FakeRepository(CreateRecord()) { ThrowOnSearch = true }, NullLogger<AiDecisionAuditController>.Instance);

        var result = await controller.SearchAsync(new AiDecisionAuditSearchRequestDto { PageNumber = 1, PageSize = 50 }, CancellationToken.None);

        result.Result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetByEventIdAsync_Returns200ForMatchingRecords()
    {
        var controller = new AiDecisionAuditController(new FakeRepository(CreateRecord()), NullLogger<AiDecisionAuditController>.Instance);

        var result = await controller.GetByEventIdAsync("evt_1", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ApiResponse<IReadOnlyList<AiDecisionAuditResponseDto>>>().Subject;
        response.Data.Should().ContainSingle(item => item.EventId == "evt_1");
    }

    [Fact]
    public async Task GetByEventIdAsync_Returns400WhenEventIdMissing()
    {
        var controller = new AiDecisionAuditController(new FakeRepository(CreateRecord()), NullLogger<AiDecisionAuditController>.Instance);

        var result = await controller.GetByEventIdAsync(" ", CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetByCorrelationIdAsync_Returns200ForMatchingRecords()
    {
        var controller = new AiDecisionAuditController(new FakeRepository(CreateRecord()), NullLogger<AiDecisionAuditController>.Instance);

        var result = await controller.GetByCorrelationIdAsync("corr_1", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ApiResponse<IReadOnlyList<AiDecisionAuditResponseDto>>>().Subject;
        response.Data.Should().ContainSingle(item => item.CorrelationId == "corr_1");
    }

    [Fact]
    public async Task GetByCorrelationIdAsync_Returns400WhenCorrelationIdMissing()
    {
        var controller = new AiDecisionAuditController(new FakeRepository(CreateRecord()), NullLogger<AiDecisionAuditController>.Instance);

        var result = await controller.GetByCorrelationIdAsync(" ", CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    private static AiDecisionAuditRecord CreateRecord() => new()
    {
        AuditId = "aud_1",
        EventId = "evt_1",
        CorrelationId = "corr_1",
        CustomerId = "cust_1",
        DecisionType = AiDecisionAuditType.RetryDecision,
        CreatedAtUtc = DateTime.UtcNow
    };

    private sealed class FakeRepository(AiDecisionAuditRecord? record) : IAiDecisionAuditRepository
    {
        public bool ThrowOnSearch { get; set; }
        public Task InsertAsync(AiDecisionAuditRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AiDecisionAuditRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(record);
        public Task<AiDecisionAuditRecord?> GetByAuditIdAsync(string auditId, CancellationToken cancellationToken = default) => Task.FromResult(record?.AuditId == auditId ? record : null);
        public Task<IReadOnlyList<AiDecisionAuditRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(record is null ? [] : [record]);
        public Task<IReadOnlyList<AiDecisionAuditRecord>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(record is null ? [] : [record]);
        public Task<IReadOnlyList<AiDecisionAuditRecord>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(record is null ? [] : [record]);
        public Task<IReadOnlyList<AiDecisionAuditRecord>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(record is null ? [] : [record]);
        public Task<IReadOnlyList<AiDecisionAuditRecord>> SearchAsync(AiDecisionAuditSearchRequestDto request, CancellationToken cancellationToken = default)
        {
            if (ThrowOnSearch) throw new InvalidOperationException("search failed");
            return Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(record is null ? [] : [record]);
        }
    }
}
