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

public sealed class AiRecommendationApprovalServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesApprovalRecordWithExpiry()
    {
        var repository = new InMemoryAiRecommendationApprovalRepository();
        var service = CreateService(repository);

        var response = await service.CreateAsync(CreateRequest(riskLevel: "High"));

        response.RecommendationId.Should().Be("rec_1");
        response.ApprovalStatus.Should().Be(AiRecommendationApprovalStatus.PendingReview);
        response.ExpiresAtUtc.Should().BeCloseTo(response.CreatedAtUtc.AddHours(72), TimeSpan.FromSeconds(5));
        repository.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task GetByRecommendationIdAsync_ReturnsMatchingApproval()
    {
        var repository = new InMemoryAiRecommendationApprovalRepository();
        var service = CreateService(repository);
        await service.CreateAsync(CreateRequest(recommendationId: "rec_lookup"));

        var response = await service.GetByRecommendationIdAsync("rec_lookup");

        response.Should().NotBeNull();
        response!.RecommendationId.Should().Be("rec_lookup");
    }

    [Fact]
    public async Task GetPendingAsync_ReturnsPendingApprovals()
    {
        var repository = new InMemoryAiRecommendationApprovalRepository();
        var service = CreateService(repository);
        await service.CreateAsync(CreateRequest(recommendationId: "rec_pending", riskLevel: "High"));
        await service.CreateAsync(CreateRequest(recommendationId: "rec_low", riskLevel: "Low"), CancellationToken.None);

        var pending = await service.GetPendingAsync();

        pending.Should().OnlyContain(approval => approval.ApprovalStatus == AiRecommendationApprovalStatus.PendingReview);
    }

    [Fact]
    public async Task SearchAsync_FiltersByStatusCustomerAndRecommendationType()
    {
        var repository = new InMemoryAiRecommendationApprovalRepository();
        var service = CreateService(repository);
        await service.CreateAsync(CreateRequest(recommendationId: "rec_a", customerId: "cust_a", type: AiRecommendationType.SecurityRecommendation));
        await service.CreateAsync(CreateRequest(recommendationId: "rec_b", customerId: "cust_b", type: AiRecommendationType.TransformationRecommendation));

        var byCustomer = await service.SearchAsync(new AiRecommendationApprovalSearchRequestDto { CustomerId = "cust_a" });
        var byType = await service.SearchAsync(new AiRecommendationApprovalSearchRequestDto { RecommendationType = AiRecommendationType.TransformationRecommendation });
        var byStatus = await service.SearchAsync(new AiRecommendationApprovalSearchRequestDto { ApprovalStatus = AiRecommendationApprovalStatus.PendingReview });

        byCustomer.Should().ContainSingle().Which.CustomerId.Should().Be("cust_a");
        byType.Should().ContainSingle().Which.RecommendationType.Should().Be(AiRecommendationType.TransformationRecommendation);
        byStatus.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(AiRecommendationApprovalStatus.PendingReview, AiRecommendationApprovalStatus.Approved)]
    [InlineData(AiRecommendationApprovalStatus.PendingReview, AiRecommendationApprovalStatus.Rejected)]
    [InlineData(AiRecommendationApprovalStatus.Approved, AiRecommendationApprovalStatus.Applied)]
    public async Task UpdateStatusAsync_AllowsValidTransitions(AiRecommendationApprovalStatus startStatus, AiRecommendationApprovalStatus targetStatus)
    {
        var repository = new InMemoryAiRecommendationApprovalRepository();
        var service = CreateService(repository);
        var created = await service.CreateAsync(CreateRequest());
        repository.Items.Single().ApprovalStatus = startStatus;

        var updated = await service.UpdateStatusAsync(created.Id!, new AiRecommendationApprovalUpdateRequestDto { ApprovalStatus = targetStatus, ReviewedBy = "admin" });

        updated!.ApprovalStatus.Should().Be(targetStatus);
    }

    [Theory]
    [InlineData(AiRecommendationApprovalStatus.Rejected, AiRecommendationApprovalStatus.Applied)]
    [InlineData(AiRecommendationApprovalStatus.Applied, AiRecommendationApprovalStatus.Rejected)]
    public async Task UpdateStatusAsync_RejectsInvalidTransitions(AiRecommendationApprovalStatus startStatus, AiRecommendationApprovalStatus targetStatus)
    {
        var repository = new InMemoryAiRecommendationApprovalRepository();
        var service = CreateService(repository);
        var created = await service.CreateAsync(CreateRequest());
        repository.Items.Single().ApprovalStatus = startStatus;

        Func<Task> act = async () => await service.UpdateStatusAsync(created.Id!, new AiRecommendationApprovalUpdateRequestDto { ApprovalStatus = targetStatus });

        await act.Should().ThrowAsync<AiRecommendationApprovalConflictException>();
    }

    [Theory]
    [InlineData(AiRecommendationType.RetryRecommendation, "High")]
    [InlineData(AiRecommendationType.RetryRecommendation, "Critical")]
    [InlineData(AiRecommendationType.SecurityRecommendation, "Low")]
    [InlineData(AiRecommendationType.TransformationRecommendation, "Low")]
    [InlineData(AiRecommendationType.RetryRecommendation, "Low")]
    public void RequiresApproval_DefaultRulesRequireExpectedRecommendations(AiRecommendationType type, string riskLevel)
    {
        AiRecommendationApprovalRules.RequiresApproval(type, riskLevel, new AiRecommendationApprovalOptions()).Should().BeTrue();
    }


    [Theory]
    [InlineData(AiRecommendationApprovalStatus.NeedsMoreInfo, AiRecommendationApprovalStatus.Approved, true)]
    [InlineData(AiRecommendationApprovalStatus.NeedsMoreInfo, AiRecommendationApprovalStatus.Rejected, true)]
    [InlineData(AiRecommendationApprovalStatus.PendingReview, AiRecommendationApprovalStatus.Expired, true)]
    [InlineData(AiRecommendationApprovalStatus.Approved, AiRecommendationApprovalStatus.Expired, true)]
    [InlineData(AiRecommendationApprovalStatus.Rejected, AiRecommendationApprovalStatus.Approved, false)]
    [InlineData(AiRecommendationApprovalStatus.Expired, AiRecommendationApprovalStatus.Approved, false)]
    public void CanTransition_ReturnsExpectedResult(AiRecommendationApprovalStatus from, AiRecommendationApprovalStatus to, bool expected)
    {
        AiRecommendationApprovalRules.CanTransition(from, to).Should().Be(expected);
    }

    [Fact]
    public void RequiresApproval_AllowsLowRiskRetryAutoApprovalWhenConfigured()
    {
        var options = new AiRecommendationApprovalOptions { AllowLowRiskAutoApproval = true };

        var requiresApproval = AiRecommendationApprovalRules.RequiresApproval(
            AiRecommendationType.RetryRecommendation,
            "Low",
            options);

        requiresApproval.Should().BeFalse();
    }

    [Fact]
    public void Mapper_ToResponseDto_MapsFields()
    {
        var entity = new AiRecommendationApproval
        {
            Id = "approval_1",
            RecommendationId = "rec_1",
            RecommendationType = AiRecommendationType.RetryRecommendation,
            ApprovalStatus = AiRecommendationApprovalStatus.PendingReview,
            RiskLevel = "High",
            CreatedAtUtc = DateTime.UtcNow
        };

        var dto = AiRecommendationApprovalMapper.ToResponseDto(entity);

        dto.Id.Should().Be(entity.Id);
        dto.RecommendationId.Should().Be(entity.RecommendationId);
        dto.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public void CreateAiRecommendationApprovalIndexModels_ReturnsRequiredIndexes()
    {
        var indexes = AiMongoIndexInitializer.CreateAiRecommendationApprovalIndexModels();

        indexes.Should().HaveCount(11);
        indexes.Should().Contain(index => index.Options.Name == "idx_ai_recommendation_approvals_recommendation_id_unique" && index.Options.Unique == true);
        indexes.Select(index => index.Options.Name).Should().Contain("idx_ai_recommendation_approvals_expires_at_utc");
    }

    [Fact]
    public void AddAiRecommendationApprovalServices_RegistersRequiredServices()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var services = new ServiceCollection();

        services.AddAiRecommendationApprovalServices(configuration);
        services.AddSingleton<IAiRecommendationApprovalRepository, InMemoryAiRecommendationApprovalRepository>();
        services.AddLogging();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAiRecommendationApprovalService>().Should().NotBeNull();
        provider.GetRequiredService<IOptions<AiRecommendationApprovalOptions>>().Value.ApprovalExpiryHours.Should().Be(72);
    }



    [Fact]
    public async Task CreateAsync_RejectsDuplicateRecommendationId()
    {
        var repository = new InMemoryAiRecommendationApprovalRepository();
        var service = CreateService(repository);
        await service.CreateAsync(CreateRequest(recommendationId: "rec_duplicate"));

        Func<Task> act = async () => await service.CreateAsync(CreateRequest(recommendationId: "rec_duplicate"));

        await act.Should().ThrowAsync<AiRecommendationApprovalConflictException>()
            .WithMessage("*already exists*");
    }

    [Theory]
    [InlineData(AiRecommendationApprovalStatus.PendingReview, AiRecommendationApprovalStatus.Approved)]
    [InlineData(AiRecommendationApprovalStatus.Approved, AiRecommendationApprovalStatus.Applied)]
    public async Task UpdateStatusAsync_RejectsActionableTransitionWhenApprovalIsExpired(AiRecommendationApprovalStatus startStatus, AiRecommendationApprovalStatus targetStatus)
    {
        var repository = new InMemoryAiRecommendationApprovalRepository();
        var service = CreateService(repository);
        var created = await service.CreateAsync(CreateRequest());
        var approval = repository.Items.Single();
        approval.ApprovalStatus = startStatus;
        approval.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);

        Func<Task> act = async () => await service.UpdateStatusAsync(created.Id!, new AiRecommendationApprovalUpdateRequestDto { ApprovalStatus = targetStatus });

        await act.Should().ThrowAsync<AiRecommendationApprovalConflictException>()
            .WithMessage("*expired*");
    }

    [Fact]
    public async Task UpdateStatusAsync_AllowsExpiredStatusWhenApprovalIsPastExpiry()
    {
        var repository = new InMemoryAiRecommendationApprovalRepository();
        var service = CreateService(repository);
        var created = await service.CreateAsync(CreateRequest());
        var approval = repository.Items.Single();
        approval.ApprovalStatus = AiRecommendationApprovalStatus.PendingReview;
        approval.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);

        var updated = await service.UpdateStatusAsync(created.Id!, new AiRecommendationApprovalUpdateRequestDto { ApprovalStatus = AiRecommendationApprovalStatus.Expired });

        updated!.ApprovalStatus.Should().Be(AiRecommendationApprovalStatus.Expired);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenApprovalDoesNotExist()
    {
        var service = CreateService(new InMemoryAiRecommendationApprovalRepository());

        var response = await service.GetByIdAsync("missing");

        response.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task GetPendingAsync_RejectsInvalidLimit(int limit)
    {
        var service = CreateService(new InMemoryAiRecommendationApprovalRepository());

        Func<Task> act = async () => await service.GetPendingAsync(limit);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 501)]
    public async Task SearchAsync_RejectsInvalidPaging(int pageNumber, int pageSize)
    {
        var service = CreateService(new InMemoryAiRecommendationApprovalRepository());
        var request = new AiRecommendationApprovalSearchRequestDto { PageNumber = pageNumber, PageSize = pageSize };

        Func<Task> act = async () => await service.SearchAsync(request);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SearchAsync_RejectsNonUtcDateRange()
    {
        var service = CreateService(new InMemoryAiRecommendationApprovalRepository());
        var request = new AiRecommendationApprovalSearchRequestDto
        {
            FromUtc = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Local)
        };

        Func<Task> act = async () => await service.SearchAsync(request);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*FromUtc must be UTC*");
    }

    [Theory]
    [InlineData("", AiRecommendationType.RetryRecommendation, "High", "RecommendationId is required")]
    [InlineData("rec_invalid", null, "High", "RecommendationType is required")]
    [InlineData("rec_invalid", AiRecommendationType.RetryRecommendation, "", "RiskLevel is required")]
    public async Task CreateAsync_RejectsInvalidRequiredFields(string recommendationId, AiRecommendationType? recommendationType, string riskLevel, string expectedMessage)
    {
        var service = CreateService(new InMemoryAiRecommendationApprovalRepository());
        var request = CreateRequest(recommendationId: string.IsNullOrEmpty(recommendationId) ? "rec_placeholder" : recommendationId, riskLevel: string.IsNullOrEmpty(riskLevel) ? "High" : riskLevel);
        request.RecommendationId = recommendationId;
        request.RecommendationType = recommendationType;
        request.RiskLevel = riskLevel;

        Func<Task> act = async () => await service.CreateAsync(request);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage($"*{expectedMessage}*");
    }

    [Fact]
    public async Task UpdateStatusAsync_ReturnsNull_WhenApprovalDoesNotExist()
    {
        var service = CreateService(new InMemoryAiRecommendationApprovalRepository());

        var response = await service.UpdateStatusAsync("missing", new AiRecommendationApprovalUpdateRequestDto { ApprovalStatus = AiRecommendationApprovalStatus.Approved });

        response.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStatusAsync_RejectsMissingApprovalStatus()
    {
        var service = CreateService(new InMemoryAiRecommendationApprovalRepository());

        Func<Task> act = async () => await service.UpdateStatusAsync("approval_1", new AiRecommendationApprovalUpdateRequestDto());

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*ApprovalStatus is required*");
    }

    [Fact]
    public void Mapper_ToEntity_AutoApprovesLowRiskRetryWhenConfigured()
    {
        var request = CreateRequest(riskLevel: "Low");
        var options = new AiRecommendationApprovalOptions { AllowLowRiskAutoApproval = true };

        var entity = AiRecommendationApprovalMapper.ToEntity(request, options, new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc));

        entity.ApprovalStatus.Should().Be(AiRecommendationApprovalStatus.Approved);
        entity.ExpiresAtUtc.Should().Be(new DateTime(2026, 5, 18, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Mapper_ToStatusUpdate_SetsAppliedTimestampOnlyForAppliedStatus()
    {
        var now = new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc);

        var update = AiRecommendationApprovalMapper.ToStatusUpdate(
            new AiRecommendationApprovalUpdateRequestDto { ApprovalStatus = AiRecommendationApprovalStatus.Applied, ReviewedBy = " admin ", ReviewComment = " applied " },
            now);

        update.ApprovalStatus.Should().Be(AiRecommendationApprovalStatus.Applied);
        update.AppliedAtUtc.Should().Be(now);
        update.ReviewedAtUtc.Should().BeNull();
        update.ReviewedBy.Should().Be("admin");
        update.ReviewComment.Should().Be("applied");
    }


    [Fact]
    public async Task GetByRecommendationIdAsync_ReturnsNull_WhenApprovalDoesNotExist()
    {
        var service = CreateService(new InMemoryAiRecommendationApprovalRepository());

        var response = await service.GetByRecommendationIdAsync("missing");

        response.Should().BeNull();
    }

    [Fact]
    public void AiRecommendationApprovalOptions_DefaultsMatchSafetyRequirements()
    {
        var options = new AiRecommendationApprovalOptions();

        options.RequireApprovalForHighRisk.Should().BeTrue();
        options.RequireApprovalForCriticalRisk.Should().BeTrue();
        options.RequireApprovalForSecurityActions.Should().BeTrue();
        options.RequireApprovalForTransformations.Should().BeTrue();
        options.AllowLowRiskAutoApproval.Should().BeFalse();
        options.ApprovalExpiryHours.Should().Be(72);
    }

    [Fact]
    public void Mapper_ToResponseDto_MapsNullableTimestampsAndCompletedStatus()
    {
        var reviewedAt = new DateTime(2026, 5, 15, 10, 0, 0, DateTimeKind.Utc);
        var appliedAt = new DateTime(2026, 5, 15, 11, 0, 0, DateTimeKind.Utc);
        var expiresAt = new DateTime(2026, 5, 18, 10, 0, 0, DateTimeKind.Utc);
        var entity = new AiRecommendationApproval
        {
            Id = "approval_2",
            RecommendationId = "rec_2",
            EventId = "evt_2",
            CorrelationId = "corr_2",
            CustomerId = "cust_2",
            SubscriptionId = "sub_2",
            EndpointId = "endpoint_2",
            RecommendationType = AiRecommendationType.SecurityRecommendation,
            ApprovalStatus = AiRecommendationApprovalStatus.Applied,
            RiskLevel = "Critical",
            SuggestedAction = "ReviewSecuritySignals",
            Summary = "Summary",
            Recommendation = "Recommendation",
            RequestedBy = "worker",
            ReviewedBy = "admin",
            ReviewComment = "approved",
            RequiresApproval = false,
            CreatedAtUtc = reviewedAt,
            ReviewedAtUtc = reviewedAt,
            AppliedAtUtc = appliedAt,
            ExpiresAtUtc = expiresAt
        };

        var dto = AiRecommendationApprovalMapper.ToResponseDto(entity);

        dto.RequiresApproval.Should().BeFalse();
        dto.ReviewedAtUtc.Should().Be(reviewedAt);
        dto.AppliedAtUtc.Should().Be(appliedAt);
        dto.ExpiresAtUtc.Should().Be(expiresAt);
        dto.ReviewedBy.Should().Be("admin");
        dto.ReviewComment.Should().Be("approved");
    }

    [Fact]
    public void Mapper_ToEntity_TrimsOptionalStringFields()
    {
        var request = CreateRequest(recommendationId: " rec_trim ");
        request.EventId = " evt_trim ";
        request.CorrelationId = " corr_trim ";
        request.CustomerId = " cust_trim ";
        request.SubscriptionId = " sub_trim ";
        request.EndpointId = " endpoint_trim ";
        request.SuggestedAction = " action ";
        request.RequestedBy = " worker ";

        var entity = AiRecommendationApprovalMapper.ToEntity(request, new AiRecommendationApprovalOptions(), DateTime.UtcNow);

        entity.RecommendationId.Should().Be("rec_trim");
        entity.EventId.Should().Be("evt_trim");
        entity.CorrelationId.Should().Be("corr_trim");
        entity.CustomerId.Should().Be("cust_trim");
        entity.SubscriptionId.Should().Be("sub_trim");
        entity.EndpointId.Should().Be("endpoint_trim");
        entity.SuggestedAction.Should().Be("action");
        entity.RequestedBy.Should().Be("worker");
    }

    [Fact]
    public void ConflictException_CanWrapInnerException()
    {
        var inner = new InvalidOperationException("duplicate key");

        var exception = new AiRecommendationApprovalConflictException("duplicate", inner);

        exception.Message.Should().Be("duplicate");
        exception.InnerException.Should().BeSameAs(inner);
    }

    private static AiRecommendationApprovalService CreateService(InMemoryAiRecommendationApprovalRepository repository, AiRecommendationApprovalOptions? options = null)
        => new(repository, Options.Create(options ?? new AiRecommendationApprovalOptions()), NullLogger<AiRecommendationApprovalService>.Instance);

    private static AiRecommendationApprovalCreateRequestDto CreateRequest(
        string recommendationId = "rec_1",
        string riskLevel = "High",
        string customerId = "cust_1",
        AiRecommendationType type = AiRecommendationType.RetryRecommendation)
        => new()
        {
            RecommendationId = recommendationId,
            EventId = "evt_1",
            CorrelationId = "corr_1",
            CustomerId = customerId,
            SubscriptionId = "sub_1",
            EndpointId = "endpoint_1",
            RecommendationType = type,
            RiskLevel = riskLevel,
            SuggestedAction = "RetryWithBackoff",
            Summary = "Summary",
            Recommendation = "Recommendation",
            RequestedBy = "tests"
        };

    private sealed class InMemoryAiRecommendationApprovalRepository : IAiRecommendationApprovalRepository
    {
        public List<AiRecommendationApproval> Items { get; } = [];

        public Task InsertAsync(AiRecommendationApproval approval, CancellationToken cancellationToken = default)
        {
            approval.Id ??= $"approval_{Items.Count + 1}";
            Items.Add(approval);
            return Task.CompletedTask;
        }

        public Task<AiRecommendationApproval?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(item => item.Id == id));

        public Task<AiRecommendationApproval?> GetByRecommendationIdAsync(string recommendationId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(item => item.RecommendationId == recommendationId));

        public Task<IReadOnlyList<AiRecommendationApproval>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AiRecommendationApproval>>(Items.Where(item => item.EventId == eventId).ToArray());

        public Task<IReadOnlyList<AiRecommendationApproval>> GetPendingAsync(int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AiRecommendationApproval>>(Items.Where(item => item.ApprovalStatus == AiRecommendationApprovalStatus.PendingReview).Take(limit).ToArray());

        public Task<IReadOnlyList<AiRecommendationApproval>> SearchAsync(AiRecommendationApprovalSearchRequestDto request, CancellationToken cancellationToken = default)
        {
            var query = Items.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(request.CustomerId)) query = query.Where(item => item.CustomerId == request.CustomerId);
            if (!string.IsNullOrWhiteSpace(request.SubscriptionId)) query = query.Where(item => item.SubscriptionId == request.SubscriptionId);
            if (!string.IsNullOrWhiteSpace(request.EndpointId)) query = query.Where(item => item.EndpointId == request.EndpointId);
            if (request.RecommendationType.HasValue) query = query.Where(item => item.RecommendationType == request.RecommendationType.Value);
            if (request.ApprovalStatus.HasValue) query = query.Where(item => item.ApprovalStatus == request.ApprovalStatus.Value);
            if (!string.IsNullOrWhiteSpace(request.RiskLevel)) query = query.Where(item => item.RiskLevel == request.RiskLevel);
            return Task.FromResult<IReadOnlyList<AiRecommendationApproval>>(query.Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToArray());
        }

        public Task<AiRecommendationApproval?> UpdateStatusAsync(string id, AiRecommendationApprovalStatusUpdate update, CancellationToken cancellationToken = default)
        {
            var approval = Items.FirstOrDefault(item => item.Id == id);
            if (approval is null) return Task.FromResult<AiRecommendationApproval?>(null);
            approval.ApprovalStatus = update.ApprovalStatus;
            approval.ReviewedBy = update.ReviewedBy;
            approval.ReviewComment = update.ReviewComment;
            approval.ReviewedAtUtc = update.ReviewedAtUtc;
            approval.AppliedAtUtc = update.AppliedAtUtc;
            return Task.FromResult<AiRecommendationApproval?>(approval);
        }
    }
}
