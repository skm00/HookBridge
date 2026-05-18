using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.Audit;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.SafeMode;
using HookBridge.Api.Controllers;
using HookBridge.Api.DTOs;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HookBridge.Api.Tests;

public sealed class AdminAiActionsControllerTests
{
    [Fact]
    public async Task GetPendingActions_Returns200()
    {
        var workflow = new FakeWorkflowService(CreateApproval());
        var controller = CreateController(workflow: workflow);

        var result = await controller.GetPendingAsync(new AdminAiActionSearchRequestDto(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(AiRecommendationApprovalStatus.PendingReview, workflow.LastSearchRequest?.ApprovalStatus);
    }

    [Fact]
    public async Task GetByApprovalId_Returns200()
    {
        var controller = CreateController(workflow: new FakeWorkflowService(CreateApproval()));

        var result = await controller.GetByApprovalIdAsync("appr_1001", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetByMissingApprovalId_Returns404()
    {
        var controller = CreateController(workflow: new FakeWorkflowService(null));

        var result = await controller.GetByApprovalIdAsync("missing", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task ApprovePendingReview_Returns200()
    {
        var workflow = new FakeWorkflowService(CreateApproval(status: AiRecommendationApprovalStatus.PendingReview));
        var controller = CreateController(workflow: workflow);

        var result = await controller.ApproveAsync("appr_1001", ReviewRequest(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(AiRecommendationApprovalStatus.Approved, workflow.Current!.ApprovalStatus);
    }

    [Fact]
    public async Task RejectPendingReview_Returns200()
    {
        var workflow = new FakeWorkflowService(CreateApproval(status: AiRecommendationApprovalStatus.PendingReview));
        var controller = CreateController(workflow: workflow);

        var result = await controller.RejectAsync("appr_1001", ReviewRequest(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(AiRecommendationApprovalStatus.Rejected, workflow.Current!.ApprovalStatus);
    }

    [Fact]
    public async Task NeedsMoreInfoPendingReview_Returns200()
    {
        var workflow = new FakeWorkflowService(CreateApproval(status: AiRecommendationApprovalStatus.PendingReview));
        var controller = CreateController(workflow: workflow);

        var result = await controller.NeedsMoreInfoAsync("appr_1001", ReviewRequest(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(AiRecommendationApprovalStatus.NeedsMoreInfo, workflow.Current!.ApprovalStatus);
    }

    [Fact]
    public async Task ApproveNeedsMoreInfo_Returns200()
    {
        var workflow = new FakeWorkflowService(CreateApproval(status: AiRecommendationApprovalStatus.NeedsMoreInfo));
        var controller = CreateController(workflow: workflow);

        var result = await controller.ApproveAsync("appr_1001", ReviewRequest(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(AiRecommendationApprovalStatus.Approved, workflow.Current!.ApprovalStatus);
    }

    [Fact]
    public async Task RejectNeedsMoreInfo_Returns200()
    {
        var workflow = new FakeWorkflowService(CreateApproval(status: AiRecommendationApprovalStatus.NeedsMoreInfo));
        var controller = CreateController(workflow: workflow);

        var result = await controller.RejectAsync("appr_1001", ReviewRequest(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(AiRecommendationApprovalStatus.Rejected, workflow.Current!.ApprovalStatus);
    }

    [Fact]
    public async Task ApplyApproved_Returns200_WhenSafeModeAllows()
    {
        var workflow = new FakeWorkflowService(CreateApproval(status: AiRecommendationApprovalStatus.Approved));
        var controller = CreateController(workflow: workflow, safeMode: new FakeSafeModeGuard(true));

        var result = await controller.ApplyAsync("appr_1001", ApplyRequest(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(AiRecommendationApprovalStatus.Applied, workflow.Current!.ApprovalStatus);
    }

    [Theory]
    [InlineData(AiRecommendationApprovalStatus.PendingReview)]
    [InlineData(AiRecommendationApprovalStatus.Rejected)]
    [InlineData(AiRecommendationApprovalStatus.Expired)]
    public async Task ApplyInvalidStatuses_Returns409(AiRecommendationApprovalStatus status)
    {
        var workflow = new FakeWorkflowService(CreateApproval(status: status));
        var controller = CreateController(workflow: workflow);

        var result = await controller.ApplyAsync("appr_1001", ApplyRequest(), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task ApplyApproved_Returns409_WhenSafeModeBlocks()
    {
        var workflow = new FakeWorkflowService(CreateApproval(status: AiRecommendationApprovalStatus.Approved));
        var controller = CreateController(workflow: workflow, safeMode: new FakeSafeModeGuard(false));

        var result = await controller.ApplyAsync("appr_1001", ApplyRequest(), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task ExpirePendingReview_Returns200()
    {
        var workflow = new FakeWorkflowService(CreateApproval(status: AiRecommendationApprovalStatus.PendingReview));
        var controller = CreateController(workflow: workflow);

        var result = await controller.ExpireAsync("appr_1001", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(AiRecommendationApprovalStatus.Expired, workflow.Current!.ApprovalStatus);
    }

    [Fact]
    public async Task InvalidTransition_Returns409()
    {
        var workflow = new FakeWorkflowService(CreateApproval(status: AiRecommendationApprovalStatus.Applied));
        var controller = CreateController(workflow: workflow);

        var result = await controller.RejectAsync("appr_1001", ReviewRequest(), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task MissingReviewedBy_Returns400()
    {
        var controller = CreateController();

        var result = await controller.ApproveAsync("appr_1001", new AdminAiActionReviewRequestDto(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task MissingAppliedBy_Returns400()
    {
        var controller = CreateController();

        var result = await controller.ApplyAsync("appr_1001", new AdminAiActionApplyRequestDto(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(AiRecommendationApprovalStatus.Approved)]
    [InlineData(AiRecommendationApprovalStatus.Rejected)]
    [InlineData(AiRecommendationApprovalStatus.Applied)]
    public async Task AuditRecord_IsCreated_ForMutations(AiRecommendationApprovalStatus expectedStatus)
    {
        var workflow = new FakeWorkflowService(CreateApproval(status: expectedStatus == AiRecommendationApprovalStatus.Applied ? AiRecommendationApprovalStatus.Approved : AiRecommendationApprovalStatus.PendingReview));
        var audit = new FakeAuditService();
        var controller = CreateController(workflow: workflow, audit: audit);

        if (expectedStatus == AiRecommendationApprovalStatus.Approved)
            await controller.ApproveAsync("appr_1001", ReviewRequest(), CancellationToken.None);
        else if (expectedStatus == AiRecommendationApprovalStatus.Rejected)
            await controller.RejectAsync("appr_1001", ReviewRequest(), CancellationToken.None);
        else
            await controller.ApplyAsync("appr_1001", ApplyRequest(), CancellationToken.None);

        Assert.Equal(1, audit.HumanApprovalAuditCount);
        Assert.Equal(expectedStatus.ToString(), audit.LastRequest?.ApprovalStatus);
    }

    [Fact]
    public async Task DecisionEvent_IsPublished_AfterApproveRejectApply()
    {
        var producer = new FakeDecisionEventProducer();
        var controller = CreateController(workflow: new FakeWorkflowService(CreateApproval(status: AiRecommendationApprovalStatus.PendingReview)), producer: producer);
        await controller.ApproveAsync("appr_1001", ReviewRequest(), CancellationToken.None);

        controller = CreateController(workflow: new FakeWorkflowService(CreateApproval(status: AiRecommendationApprovalStatus.PendingReview)), producer: producer);
        await controller.RejectAsync("appr_1001", ReviewRequest(), CancellationToken.None);

        controller = CreateController(workflow: new FakeWorkflowService(CreateApproval(status: AiRecommendationApprovalStatus.Approved)), producer: producer);
        await controller.ApplyAsync("appr_1001", ApplyRequest(), CancellationToken.None);

        Assert.Equal(3, producer.PublishCount);
    }

    [Fact]
    public async Task DecisionEventPublishFailure_DoesNotFailAdminAction()
    {
        var producer = new FakeDecisionEventProducer { ThrowOnPublish = true };
        var controller = CreateController(workflow: new FakeWorkflowService(CreateApproval(status: AiRecommendationApprovalStatus.PendingReview)), producer: producer);

        var result = await controller.ApproveAsync("appr_1001", ReviewRequest(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Controller_DoesNotExposeMongoEntityDirectly()
    {
        var controller = CreateController(workflow: new FakeWorkflowService(CreateApproval()));

        var result = await controller.GetByApprovalIdAsync("appr_1001", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsAssignableFrom<ApiResponse<AdminAiActionResponseDto>>(ok.Value);
        Assert.IsNotType<AiRecommendationApproval>(response.Data);
    }

    [Fact]
    public void RequiredServices_AreRegisteredInDI()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HumanApprovalWorkflow:ApprovalExpiryHours"] = "72",
                ["AiRecommendationApproval:ApprovalExpiryHours"] = "72",
                ["AiSafeMode:Environment"] = "Development",
                ["AiKafka:BootstrapServers"] = "localhost:9092",
                ["AiKafka:SecurityProtocol"] = "Plaintext",
                ["AiKafka:ConsumerGroupId"] = "tests"
            })
            .Build();

        HookBridge.AI.Worker.Extensions.ServiceCollectionExtensions.AddAiRecommendationApprovalServices(services, configuration);
        HookBridge.AI.Worker.Extensions.ServiceCollectionExtensions.AddAiSafeModeServices(services, configuration);
        HookBridge.AI.Worker.Extensions.ServiceCollectionExtensions.AddAiKafkaOptions(services, configuration);
        HookBridge.AI.Worker.Extensions.ServiceCollectionExtensions.AddAiKafkaServices(services);
        services.AddSingleton<IAiDecisionAuditService, AiDecisionAuditService>();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IHumanApprovalWorkflowService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAiSafeModeGuard));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAiDecisionAuditService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAiDecisionEventProducer));
    }

    private static AdminAiActionsController CreateController(
        IHumanApprovalWorkflowService? workflow = null,
        IAiSafeModeGuard? safeMode = null,
        IAiDecisionAuditService? audit = null,
        IAiDecisionEventProducer? producer = null)
    {
        var controller = new AdminAiActionsController(
            workflow ?? new FakeWorkflowService(CreateApproval()),
            safeMode ?? new FakeSafeModeGuard(true),
            audit ?? new FakeAuditService(),
            producer ?? new FakeDecisionEventProducer(),
            NullLogger<AdminAiActionsController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static AdminAiActionReviewRequestDto ReviewRequest() => new()
    {
        ReviewedBy = "admin@hookbridge.local",
        ReviewComment = "reviewed"
    };

    private static AdminAiActionApplyRequestDto ApplyRequest() => new()
    {
        AppliedBy = "admin@hookbridge.local",
        ApplyComment = "marked applied"
    };

    private static HumanApprovalWorkflowResponseDto CreateApproval(AiRecommendationApprovalStatus status = AiRecommendationApprovalStatus.PendingReview) => new()
    {
        ApprovalId = "appr_1001",
        RecommendationId = "rec_1001",
        RecommendationType = AiRecommendationType.RetryRecommendation,
        ApprovalStatus = status,
        RiskLevel = "High",
        SuggestedAction = "NotifyOnly",
        RequiresApproval = true,
        CanApply = status == AiRecommendationApprovalStatus.Approved,
        Summary = "summary",
        Recommendation = "recommendation",
        RequestedBy = "HookBridge.AI.Worker",
        CreatedAtUtc = DateTime.UtcNow,
        ExpiresAtUtc = DateTime.UtcNow.AddDays(1)
    };

    private sealed class FakeWorkflowService(HumanApprovalWorkflowResponseDto? current) : IHumanApprovalWorkflowService
    {
        public HumanApprovalWorkflowResponseDto? Current { get; private set; } = current;
        public AiRecommendationApprovalSearchRequestDto? LastSearchRequest { get; private set; }

        public Task<HumanApprovalWorkflowResponseDto> CreateAsync(HumanApprovalWorkflowCreateRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(Current!);

        public Task<HumanApprovalWorkflowResponseDto?> GetByIdAsync(string approvalId, CancellationToken cancellationToken = default)
            => Task.FromResult(Current);

        public Task<IReadOnlyList<HumanApprovalWorkflowResponseDto>> GetPendingAsync(int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HumanApprovalWorkflowResponseDto>>(Current is null ? [] : [Current]);

        public Task<IReadOnlyList<HumanApprovalWorkflowResponseDto>> SearchPendingAsync(AiRecommendationApprovalSearchRequestDto request, CancellationToken cancellationToken = default)
        {
            LastSearchRequest = request;
            return Task.FromResult<IReadOnlyList<HumanApprovalWorkflowResponseDto>>(Current is null ? [] : [Current]);
        }

        public Task<HumanApprovalWorkflowResponseDto?> ReviewAsync(string approvalId, HumanApprovalWorkflowReviewRequestDto request, CancellationToken cancellationToken = default)
        {
            if (Current is null) return Task.FromResult<HumanApprovalWorkflowResponseDto?>(null);
            if (!HumanApprovalWorkflowRules.CanTransition(Current.ApprovalStatus, request.ApprovalStatus!.Value))
                throw new AiRecommendationApprovalConflictException("Invalid approval status transition.");
            Current.ApprovalStatus = request.ApprovalStatus.Value;
            Current.ReviewedBy = request.ReviewedBy;
            Current.ReviewComment = request.ReviewComment;
            Current.ReviewedAtUtc = DateTime.UtcNow;
            Current.CanApply = Current.ApprovalStatus == AiRecommendationApprovalStatus.Approved;
            return Task.FromResult<HumanApprovalWorkflowResponseDto?>(Current);
        }

        public Task<HumanApprovalWorkflowResponseDto?> ApplyAsync(string approvalId, HumanApprovalWorkflowApplyRequestDto request, CancellationToken cancellationToken = default)
        {
            if (Current is null) return Task.FromResult<HumanApprovalWorkflowResponseDto?>(null);
            if (!HumanApprovalWorkflowRules.CanTransition(Current.ApprovalStatus, AiRecommendationApprovalStatus.Applied))
                throw new AiRecommendationApprovalConflictException("Invalid approval status transition.");
            Current.ApprovalStatus = AiRecommendationApprovalStatus.Applied;
            Current.AppliedBy = request.AppliedBy;
            Current.ApplyComment = request.ApplyComment;
            Current.AppliedAtUtc = DateTime.UtcNow;
            Current.CanApply = false;
            return Task.FromResult<HumanApprovalWorkflowResponseDto?>(Current);
        }

        public Task<HumanApprovalWorkflowResponseDto?> ExpireAsync(string approvalId, CancellationToken cancellationToken = default)
        {
            if (Current is null) return Task.FromResult<HumanApprovalWorkflowResponseDto?>(null);
            if (!HumanApprovalWorkflowRules.CanTransition(Current.ApprovalStatus, AiRecommendationApprovalStatus.Expired))
                throw new AiRecommendationApprovalConflictException("Invalid approval status transition.");
            Current.ApprovalStatus = AiRecommendationApprovalStatus.Expired;
            Current.CanApply = false;
            return Task.FromResult<HumanApprovalWorkflowResponseDto?>(Current);
        }
    }

    private sealed class FakeSafeModeGuard(bool allowed) : IAiSafeModeGuard
    {
        public Task<AiSafeModeEvaluationResponseDto> EvaluateAsync(AiSafeModeEvaluationRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new AiSafeModeEvaluationResponseDto
            {
                Decision = allowed ? AiSafeModeDecision.Allowed : AiSafeModeDecision.Blocked,
                IsAllowed = allowed,
                RequiresApproval = !allowed,
                Reason = allowed ? "allowed" : "blocked",
                ActionType = request.ActionType,
                Environment = request.Environment,
                EvaluatedAtUtc = DateTime.UtcNow
            });
    }

    private sealed class FakeAuditService : IAiDecisionAuditService
    {
        public int HumanApprovalAuditCount { get; private set; }
        public AiDecisionAuditCreateRequestDto? LastRequest { get; private set; }
        public Task<AiDecisionAuditRecord?> AuditHumanApprovalAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default)
        {
            HumanApprovalAuditCount++;
            LastRequest = request;
            return Task.FromResult<AiDecisionAuditRecord?>(new AiDecisionAuditRecord { AuditId = "aud_1", DecisionId = request.DecisionId ?? "dec_1" });
        }
        public Task<AiDecisionAuditRecord?> AuditRetryDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<AiDecisionAuditRecord?>(null);
        public Task<AiDecisionAuditRecord?> AuditSecurityDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<AiDecisionAuditRecord?>(null);
        public Task<AiDecisionAuditRecord?> AuditTransformationDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<AiDecisionAuditRecord?>(null);
        public Task<AiDecisionAuditRecord?> AuditObservabilityDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<AiDecisionAuditRecord?>(null);
        public Task<AiDecisionAuditRecord?> AuditOrchestrationDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<AiDecisionAuditRecord?>(null);
        public Task<AiDecisionAuditRecord?> AuditAutoRemediationRecommendationAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<AiDecisionAuditRecord?>(null);
        public Task<AiDecisionAuditRecord?> AuditSafeModeEvaluationAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<AiDecisionAuditRecord?>(null);
        public Task<AiDecisionAuditRecord?> AuditFallbackDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<AiDecisionAuditRecord?>(null);
        public Task<AiDecisionAuditRecord?> AuditGenericDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<AiDecisionAuditRecord?>(null);
    }

    private sealed class FakeDecisionEventProducer : IAiDecisionEventProducer
    {
        public int PublishCount { get; private set; }
        public bool ThrowOnPublish { get; set; }
        public Task<AiKafkaPublishResult> PublishAsync(AiDecisionEventDto decisionEvent, CancellationToken cancellationToken = default)
        {
            PublishCount++;
            if (ThrowOnPublish) throw new InvalidOperationException("publish failed");
            return Task.FromResult(AiKafkaPublishResult.Success("hookbridge.ai.decisions", decisionEvent.DecisionId, 0, 1, DateTime.UtcNow));
        }
    }
}
