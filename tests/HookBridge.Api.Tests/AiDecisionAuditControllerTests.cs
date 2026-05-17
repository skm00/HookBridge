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
        public Task InsertAsync(AiDecisionAuditRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AiDecisionAuditRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(record);
        public Task<AiDecisionAuditRecord?> GetByAuditIdAsync(string auditId, CancellationToken cancellationToken = default) => Task.FromResult(record?.AuditId == auditId ? record : null);
        public Task<IReadOnlyList<AiDecisionAuditRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(record is null ? [] : [record]);
        public Task<IReadOnlyList<AiDecisionAuditRecord>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(record is null ? [] : [record]);
        public Task<IReadOnlyList<AiDecisionAuditRecord>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(record is null ? [] : [record]);
        public Task<IReadOnlyList<AiDecisionAuditRecord>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(record is null ? [] : [record]);
        public Task<IReadOnlyList<AiDecisionAuditRecord>> SearchAsync(AiDecisionAuditSearchRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(record is null ? [] : [record]);
    }
}
