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
