using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.SafeMode;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiSafeModeGuardTests
{
    [Theory]
    [InlineData(AiActionType.ReadOnlyQuery)]
    [InlineData(AiActionType.GenerateRecommendation)]
    [InlineData(AiActionType.NotifyOnly)]
    public async Task AdvisoryActions_AreAllowed(AiActionType actionType)
    {
        var response = await CreateGuard().EvaluateAsync(Request(actionType, "production"));
        response.Decision.Should().Be(AiSafeModeDecision.Allowed);
        response.IsAllowed.Should().BeTrue();
    }

    [Theory]
    [InlineData(AiActionType.RetryWebhook)]
    [InlineData(AiActionType.MoveToDeadLetter)]
    [InlineData(AiActionType.ReplayDeadLetter)]
    public async Task ProtectedRetryAndDeadLetterActions_RequireApprovalInProduction(AiActionType actionType)
    {
        var response = await CreateGuard().EvaluateAsync(Request(actionType, "production"));
        response.Decision.Should().Be(AiSafeModeDecision.RequiresApproval);
        response.RequiresApproval.Should().BeTrue();
        response.IsAllowed.Should().BeFalse();
    }

    [Theory]
    [InlineData(AiActionType.PauseEndpoint)]
    [InlineData(AiActionType.ApplyTransformation)]
    [InlineData(AiActionType.UpdateConfiguration)]
    public async Task AlwaysProtectedActions_RequireApprovalInAllEnvironments(AiActionType actionType)
    {
        var response = await CreateGuard().EvaluateAsync(Request(actionType, "qa"));
        response.Decision.Should().Be(AiSafeModeDecision.RequiresApproval);
        response.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public async Task ApprovedApprovalStatus_AllowsProtectedAction()
    {
        var response = await CreateGuard().EvaluateAsync(Request(AiActionType.RetryWebhook, "production", approvalStatus: AiRecommendationApprovalStatus.Approved));
        response.Decision.Should().Be(AiSafeModeDecision.Allowed);
        response.IsAllowed.Should().BeTrue();
    }

    [Theory]
    [InlineData(AiRecommendationApprovalStatus.PendingReview)]
    [InlineData(AiRecommendationApprovalStatus.Rejected)]
    [InlineData(AiRecommendationApprovalStatus.Expired)]
    [InlineData(AiRecommendationApprovalStatus.Applied)]
    public async Task NonApprovedApprovalStatus_BlocksProtectedAction(AiRecommendationApprovalStatus approvalStatus)
    {
        var response = await CreateGuard().EvaluateAsync(Request(AiActionType.RetryWebhook, "production", approvalStatus: approvalStatus));
        response.IsAllowed.Should().BeFalse();
        response.Decision.Should().Be(AiSafeModeDecision.Blocked);
    }

    [Theory]
    [InlineData("High")]
    [InlineData("Critical")]
    public async Task HighAndCriticalRisk_RequireApproval(string riskLevel)
    {
        var response = await CreateGuard().EvaluateAsync(Request(AiActionType.ScaleWorker, "qa", riskLevel));
        response.Decision.Should().Be(AiSafeModeDecision.RequiresApproval);
        response.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public async Task LowConfidence_RequiresManualReview()
    {
        var response = await CreateGuard().EvaluateAsync(Request(AiActionType.RetryWebhook, "production", confidenceScore: 0.59));
        response.Decision.Should().Be(AiSafeModeDecision.RequiresManualReview);
        response.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task NonProductionAction_BlockedWhenAutoApplyDisabled()
    {
        var response = await CreateGuard().EvaluateAsync(Request(AiActionType.ScaleWorker, "qa", riskLevel: "Low"));
        response.Decision.Should().Be(AiSafeModeDecision.Blocked);
    }

    [Fact]
    public async Task BlockedAction_CreatesAuditRecord()
    {
        var repository = new InMemoryAiSafeModeAuditRepository();
        var response = await CreateGuard(repository: repository).EvaluateAsync(Request(AiActionType.RetryWebhook, "production"));

        response.RequiresApproval.Should().BeTrue();
        repository.Records.Should().ContainSingle(record => record.ActionType == AiActionType.RetryWebhook && record.Decision == AiSafeModeDecision.RequiresApproval);
    }


    [Fact]
    public async Task Repository_InsertAndGetByEventId_Succeeds()
    {
        var repository = new InMemoryAiSafeModeAuditRepository();
        await repository.InsertAsync(new AiSafeModeAuditRecord
        {
            ActionType = AiActionType.RetryWebhook,
            Decision = AiSafeModeDecision.RequiresApproval,
            Environment = "production",
            EventId = "evt-repo",
            EvaluatedAtUtc = DateTime.UtcNow
        });

        var records = await repository.GetByEventIdAsync("evt-repo");
        records.Should().ContainSingle(record => record.EventId == "evt-repo");
    }

    [Fact]
    public async Task Repository_SearchByActionTypeAndDecision_Succeeds()
    {
        var repository = new InMemoryAiSafeModeAuditRepository();
        await repository.InsertAsync(new AiSafeModeAuditRecord { ActionType = AiActionType.RetryWebhook, Decision = AiSafeModeDecision.RequiresApproval, Environment = "production", EventId = "evt-1", EvaluatedAtUtc = DateTime.UtcNow });
        await repository.InsertAsync(new AiSafeModeAuditRecord { ActionType = AiActionType.NotifyOnly, Decision = AiSafeModeDecision.Allowed, Environment = "production", EventId = "evt-2", EvaluatedAtUtc = DateTime.UtcNow });

        var byAction = await repository.SearchAsync(new AiSafeModeAuditSearchRequestDto { ActionType = AiActionType.RetryWebhook });
        var byDecision = await repository.SearchAsync(new AiSafeModeAuditSearchRequestDto { Decision = AiSafeModeDecision.Allowed });

        byAction.Should().ContainSingle(record => record.ActionType == AiActionType.RetryWebhook);
        byDecision.Should().ContainSingle(record => record.Decision == AiSafeModeDecision.Allowed);
    }

    [Fact]
    public void IndexCreationLogic_IncludesRequiredSafeModeIndexes()
    {
        var indexes = AiMongoIndexInitializer.CreateAiSafeModeAuditRecordIndexModels();
        indexes.Should().HaveCount(9);
        indexes.Select(index => index.Options.Name).Should().Contain("idx_ai_safe_mode_audit_evaluated_at_utc_desc");
    }

    [Fact]
    public void RequiredServices_AreRegisteredInDi()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiSafeMode:Environment"] = "production"
        }).Build();
        services.AddLogging();
        services.AddAiSafeModeServices(configuration);

        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IAiSafeModeGuard));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IAiSafeModeAuditRepository));
    }

    private static AiSafeModeGuard CreateGuard(AiSafeModeOptions? options = null, IAiSafeModeAuditRepository? repository = null)
        => new(Options.Create(options ?? new AiSafeModeOptions()), NullLogger<AiSafeModeGuard>.Instance, repository);

    private static AiSafeModeEvaluationRequestDto Request(AiActionType actionType, string environment, string riskLevel = "Medium", double confidenceScore = 0.82, AiRecommendationApprovalStatus? approvalStatus = null) => new()
    {
        ActionType = actionType,
        Environment = environment,
        EventId = "evt-1",
        CorrelationId = "corr-1",
        CustomerId = "cust-1",
        SubscriptionId = "sub-1",
        EndpointId = "endpoint-1",
        RiskLevel = riskLevel,
        ConfidenceScore = confidenceScore,
        ApprovalStatus = approvalStatus,
        RequestedBy = "test",
        Reason = "test",
        RequestedAtUtc = DateTime.UtcNow
    };

    private sealed class InMemoryAiSafeModeAuditRepository : IAiSafeModeAuditRepository
    {
        public List<AiSafeModeAuditRecord> Records { get; } = [];
        public Task InsertAsync(AiSafeModeAuditRecord record, CancellationToken cancellationToken = default) { Records.Add(record); return Task.CompletedTask; }
        public Task<IReadOnlyList<AiSafeModeAuditRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiSafeModeAuditRecord>>(Records.Where(record => record.EventId == eventId).ToArray());
        public Task<IReadOnlyList<AiSafeModeAuditRecord>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiSafeModeAuditRecord>>(Records.Where(record => record.CorrelationId == correlationId).ToArray());
        public Task<IReadOnlyList<AiSafeModeAuditRecord>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiSafeModeAuditRecord>>(Records.OrderByDescending(record => record.EvaluatedAtUtc).Take(limit).ToArray());
        public Task<IReadOnlyList<AiSafeModeAuditRecord>> SearchAsync(AiSafeModeAuditSearchRequestDto request, CancellationToken cancellationToken = default)
        {
            IEnumerable<AiSafeModeAuditRecord> query = Records;
            if (request.ActionType is not null) query = query.Where(record => record.ActionType == request.ActionType.Value);
            if (request.Decision is not null) query = query.Where(record => record.Decision == request.Decision.Value);
            return Task.FromResult<IReadOnlyList<AiSafeModeAuditRecord>>(query.ToArray());
        }
    }
}
