using FluentAssertions;
using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Mongo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Tests;

public sealed class HumanApprovalWorkflowServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesPendingApprovalWorkflowWithExpiry()
    {
        var repository = new InMemoryRepository();
        var service = CreateService(repository);

        var response = await service.CreateAsync(CreateRequest(riskLevel: "High"));

        response.ApprovalId.Should().Be("approval_1");
        response.ApprovalStatus.Should().Be(AiRecommendationApprovalStatus.PendingReview);
        response.RequiresApproval.Should().BeTrue();
        response.CanApply.Should().BeFalse();
        response.ExpiresAtUtc.Should().Be(new DateTime(2026, 5, 17, 10, 30, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData(AiRecommendationType.RetryRecommendation, "High", "RetryWithBackoff")]
    [InlineData(AiRecommendationType.RetryRecommendation, "Critical", "RetryWithBackoff")]
    [InlineData(AiRecommendationType.SecurityRecommendation, "Low", "BlockEndpoint")]
    [InlineData(AiRecommendationType.TransformationRecommendation, "Low", "UpdateMapping")]
    [InlineData(AiRecommendationType.DtoSuggestion, "Low", "Generate code")]
    public void RequiresApproval_ReturnsTrueForUnsafeRules(AiRecommendationType type, string riskLevel, string suggestedAction)
    {
        HumanApprovalWorkflowRules.RequiresApproval(type, riskLevel, suggestedAction, new HumanApprovalWorkflowOptions()).Should().BeTrue();
    }

    [Fact]
    public void RequiresApproval_LowRiskRetryRequiresApprovalByDefault()
    {
        HumanApprovalWorkflowRules.RequiresApproval(AiRecommendationType.RetryRecommendation, "Low", "Retry", new HumanApprovalWorkflowOptions()).Should().BeTrue();
    }

    [Fact]
    public void RequiresApproval_LowRiskRetryCanSkipApprovalWhenConfigured()
    {
        HumanApprovalWorkflowRules.RequiresApproval(
            AiRecommendationType.RetryRecommendation,
            "Low",
            "Retry",
            new HumanApprovalWorkflowOptions { AllowLowRiskAutoApproval = true }).Should().BeFalse();
    }

    [Theory]
    [InlineData(AiRecommendationApprovalStatus.PendingReview, AiRecommendationApprovalStatus.Approved)]
    [InlineData(AiRecommendationApprovalStatus.PendingReview, AiRecommendationApprovalStatus.Rejected)]
    [InlineData(AiRecommendationApprovalStatus.PendingReview, AiRecommendationApprovalStatus.NeedsMoreInfo)]
    [InlineData(AiRecommendationApprovalStatus.NeedsMoreInfo, AiRecommendationApprovalStatus.Approved)]
    [InlineData(AiRecommendationApprovalStatus.NeedsMoreInfo, AiRecommendationApprovalStatus.Rejected)]
    [InlineData(AiRecommendationApprovalStatus.Approved, AiRecommendationApprovalStatus.Applied)]
    public void CanTransition_AllowsValidTransitions(AiRecommendationApprovalStatus from, AiRecommendationApprovalStatus to)
    {
        HumanApprovalWorkflowRules.CanTransition(from, to).Should().BeTrue();
    }

    [Theory]
    [InlineData(AiRecommendationApprovalStatus.PendingReview, AiRecommendationApprovalStatus.Applied)]
    [InlineData(AiRecommendationApprovalStatus.NeedsMoreInfo, AiRecommendationApprovalStatus.Applied)]
    [InlineData(AiRecommendationApprovalStatus.Rejected, AiRecommendationApprovalStatus.Applied)]
    [InlineData(AiRecommendationApprovalStatus.Applied, AiRecommendationApprovalStatus.Approved)]
    [InlineData(AiRecommendationApprovalStatus.Expired, AiRecommendationApprovalStatus.Approved)]
    public void CanTransition_RejectsInvalidTransitions(AiRecommendationApprovalStatus from, AiRecommendationApprovalStatus to)
    {
        HumanApprovalWorkflowRules.CanTransition(from, to).Should().BeFalse();
    }

    [Fact]
    public async Task ReviewAsync_PendingReviewToApproved_AllowsApply()
    {
        var repository = new InMemoryRepository();
        var service = CreateService(repository);
        var created = await service.CreateAsync(CreateRequest());

        var reviewed = await service.ReviewAsync(created.ApprovalId!, new HumanApprovalWorkflowReviewRequestDto { ApprovalStatus = AiRecommendationApprovalStatus.Approved, ReviewedBy = "admin", ReviewComment = "ok" });

        reviewed!.ApprovalStatus.Should().Be(AiRecommendationApprovalStatus.Approved);
        reviewed.CanApply.Should().BeTrue();
        reviewed.ReviewedBy.Should().Be("admin");
    }

    [Fact]
    public async Task ReviewAsync_PendingReviewToApplied_IsRejected()
    {
        var service = CreateService(new InMemoryRepository());
        var created = await service.CreateAsync(CreateRequest());

        Func<Task> act = async () => await service.ReviewAsync(created.ApprovalId!, new HumanApprovalWorkflowReviewRequestDto { ApprovalStatus = AiRecommendationApprovalStatus.Applied, ReviewedBy = "admin" });

        await act.Should().ThrowAsync<AiRecommendationApprovalConflictException>();
    }

    [Fact]
    public async Task ApplyAsync_ApprovedToApplied_StoresAuditFields()
    {
        var repository = new InMemoryRepository();
        var service = CreateService(repository);
        var created = await service.CreateAsync(CreateRequest());
        await service.ReviewAsync(created.ApprovalId!, new HumanApprovalWorkflowReviewRequestDto { ApprovalStatus = AiRecommendationApprovalStatus.Approved, ReviewedBy = "admin" });

        var applied = await service.ApplyAsync(created.ApprovalId!, new HumanApprovalWorkflowApplyRequestDto { AppliedBy = "operator", ApplyComment = "done" });

        applied!.ApprovalStatus.Should().Be(AiRecommendationApprovalStatus.Applied);
        applied.CanApply.Should().BeFalse();
        applied.AppliedBy.Should().Be("operator");
        applied.ApplyComment.Should().Be("done");
        applied.AppliedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ApplyAsync_RejectedToApplied_IsRejected()
    {
        var repository = new InMemoryRepository();
        var service = CreateService(repository);
        var created = await service.CreateAsync(CreateRequest());
        await service.ReviewAsync(created.ApprovalId!, new HumanApprovalWorkflowReviewRequestDto { ApprovalStatus = AiRecommendationApprovalStatus.Rejected, ReviewedBy = "admin" });

        Func<Task> act = async () => await service.ApplyAsync(created.ApprovalId!, new HumanApprovalWorkflowApplyRequestDto { AppliedBy = "operator" });

        await act.Should().ThrowAsync<AiRecommendationApprovalConflictException>();
    }

    [Fact]
    public async Task ExpireAsync_PendingReviewToExpired_PreventsFurtherChange()
    {
        var service = CreateService(new InMemoryRepository());
        var created = await service.CreateAsync(CreateRequest());
        await service.ExpireAsync(created.ApprovalId!);

        Func<Task> act = async () => await service.ReviewAsync(created.ApprovalId!, new HumanApprovalWorkflowReviewRequestDto { ApprovalStatus = AiRecommendationApprovalStatus.Approved, ReviewedBy = "admin" });

        await act.Should().ThrowAsync<AiRecommendationApprovalConflictException>();
    }

    [Theory]
    [InlineData(AiRecommendationApprovalStatus.PendingReview, false)]
    [InlineData(AiRecommendationApprovalStatus.Approved, true)]
    [InlineData(AiRecommendationApprovalStatus.Rejected, false)]
    [InlineData(AiRecommendationApprovalStatus.NeedsMoreInfo, false)]
    [InlineData(AiRecommendationApprovalStatus.Expired, false)]
    [InlineData(AiRecommendationApprovalStatus.Applied, false)]
    public void CanApply_IsTrueOnlyForApproved(AiRecommendationApprovalStatus status, bool expected)
    {
        HumanApprovalWorkflowRules.CanApply(status).Should().Be(expected);
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidRequiredFields()
    {
        var service = CreateService(new InMemoryRepository());
        var request = CreateRequest();
        request.RequestedBy = " ";

        Func<Task> act = async () => await service.CreateAsync(request);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*RequestedBy is required*");
    }

    [Fact]
    public async Task ReviewAsync_RequiresReviewedBy()
    {
        var service = CreateService(new InMemoryRepository());
        var created = await service.CreateAsync(CreateRequest());

        Func<Task> act = async () => await service.ReviewAsync(created.ApprovalId!, new HumanApprovalWorkflowReviewRequestDto { ApprovalStatus = AiRecommendationApprovalStatus.Approved });

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*ReviewedBy is required*");
    }

    [Fact]
    public async Task ApplyAsync_RequiresAppliedBy()
    {
        var service = CreateService(new InMemoryRepository());
        var created = await service.CreateAsync(CreateRequest());

        Func<Task> act = async () => await service.ApplyAsync(created.ApprovalId!, new HumanApprovalWorkflowApplyRequestDto());

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*AppliedBy is required*");
    }

    [Fact]
    public void AddAiRecommendationApprovalServices_RegistersHumanWorkflowServices()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        services.AddAiRecommendationApprovalServices(configuration);
        services.AddSingleton<IAiRecommendationApprovalRepository, InMemoryRepository>();
        services.AddLogging();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IHumanApprovalWorkflowService>().Should().NotBeNull();
        provider.GetRequiredService<IOptions<HumanApprovalWorkflowOptions>>().Value.AllowApplyOnlyAfterApproval.Should().BeTrue();
    }

    private static HumanApprovalWorkflowService CreateService(InMemoryRepository repository, HumanApprovalWorkflowOptions? options = null)
        => new(repository, Options.Create(options ?? new HumanApprovalWorkflowOptions()), NullLogger<HumanApprovalWorkflowService>.Instance);

    private static HumanApprovalWorkflowCreateRequestDto CreateRequest(string recommendationId = "rec_1", string riskLevel = "High")
        => new()
        {
            RecommendationId = recommendationId,
            RecommendationType = AiRecommendationType.RetryRecommendation,
            EventId = "evt_12345",
            CorrelationId = "corr_789",
            CustomerId = "cust_123",
            CustomerIdType = "External",
            SubscriptionId = "sub_456",
            EndpointId = "endpoint_789",
            Environment = "qa",
            RiskLevel = riskLevel,
            SuggestedAction = "RetryWithBackoff",
            Summary = "Endpoint is failing due to HTTP 429 rate limiting.",
            Recommendation = "Retry with exponential backoff and reduce delivery concurrency.",
            RequestedBy = "HookBridge.AI.Worker",
            CreatedAtUtc = new DateTime(2026, 5, 14, 10, 30, 0, DateTimeKind.Utc)
        };

    private sealed class InMemoryRepository : IAiRecommendationApprovalRepository
    {
        private readonly List<AiRecommendationApproval> _items = [];

        public Task InsertAsync(AiRecommendationApproval approval, CancellationToken cancellationToken = default)
        {
            approval.Id ??= $"approval_{_items.Count + 1}";
            _items.Add(approval);
            return Task.CompletedTask;
        }

        public Task<AiRecommendationApproval?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(item => item.Id == id));

        public Task<AiRecommendationApproval?> GetByRecommendationIdAsync(string recommendationId, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(item => item.RecommendationId == recommendationId));

        public Task<IReadOnlyList<AiRecommendationApproval>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AiRecommendationApproval>>(_items.Where(item => item.EventId == eventId).ToArray());

        public Task<IReadOnlyList<AiRecommendationApproval>> GetPendingAsync(int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AiRecommendationApproval>>(_items.Where(item => item.ApprovalStatus == AiRecommendationApprovalStatus.PendingReview).Take(limit).ToArray());

        public Task<IReadOnlyList<AiRecommendationApproval>> SearchAsync(AiRecommendationApprovalSearchRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AiRecommendationApproval>>(_items.ToArray());

        public Task<AiRecommendationApproval?> UpdateStatusAsync(string id, AiRecommendationApprovalStatusUpdate update, CancellationToken cancellationToken = default)
        {
            var approval = _items.FirstOrDefault(item => item.Id == id);
            if (approval is null) return Task.FromResult<AiRecommendationApproval?>(null);
            approval.ApprovalStatus = update.ApprovalStatus;
            approval.ReviewedBy = update.ReviewedBy;
            approval.ReviewComment = update.ReviewComment;
            approval.AppliedBy = update.AppliedBy;
            approval.ApplyComment = update.ApplyComment;
            approval.ReviewedAtUtc = update.ReviewedAtUtc;
            approval.AppliedAtUtc = update.AppliedAtUtc;
            return Task.FromResult<AiRecommendationApproval?>(approval);
        }
    }
}
