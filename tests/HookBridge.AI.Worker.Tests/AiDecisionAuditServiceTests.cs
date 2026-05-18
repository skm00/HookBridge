using FluentAssertions;
using HookBridge.AI.Worker.Audit;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiDecisionAuditServiceTests
{
    [Fact]
    public async Task AuditRetryDecisionAsync_CreatesAuditRecord()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        var record = await service.AuditRetryDecisionAsync(CreateRequest(AiDecisionAuditType.RetryDecision));

        record.Should().NotBeNull();
        repository.Inserted.Should().ContainSingle();
        repository.Inserted[0].DecisionType.Should().Be(AiDecisionAuditType.RetryDecision);
        repository.Inserted[0].AuditId.Should().StartWith("aud_");
        repository.Inserted[0].CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData(AiDecisionAuditType.SecurityDecision)]
    [InlineData(AiDecisionAuditType.TransformationDecision)]
    [InlineData(AiDecisionAuditType.ObservabilityDecision)]
    [InlineData(AiDecisionAuditType.OrchestrationDecision)]
    [InlineData(AiDecisionAuditType.AutoRemediationRecommendation)]
    [InlineData(AiDecisionAuditType.HumanApproval)]
    [InlineData(AiDecisionAuditType.SafeModeEvaluation)]
    [InlineData(AiDecisionAuditType.FallbackDecision)]
    public async Task AuditGenericDecisionAsync_CreatesExpectedDecisionType(AiDecisionAuditType type)
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);

        await service.AuditGenericDecisionAsync(CreateRequest(type));

        repository.Inserted.Single().DecisionType.Should().Be(type);
    }

    [Fact]
    public async Task AuditGenericDecisionAsync_MasksSensitiveMetadataAndDropsRawPayloads()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);
        var request = CreateRequest(AiDecisionAuditType.SecurityDecision);
        request.Metadata["Authorization"] = "Bearer secret";
        request.Metadata["rawPayload"] = "{bad:true}";
        request.Metadata["safe"] = "value";

        await service.AuditGenericDecisionAsync(request);

        var metadata = repository.Inserted.Single().Metadata;
        metadata["Authorization"].Should().Be("***MASKED***");
        metadata.Should().NotContainKey("rawPayload");
        metadata["safe"].Should().Be("value");
    }

    [Fact]
    public async Task AuditGenericDecisionAsync_TruncatesMetadata()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository, new AiDecisionAuditOptions { MaxMetadataLength = 40 });
        var request = CreateRequest(AiDecisionAuditType.RetryDecision);
        request.Metadata["safe"] = new string('x', 200);

        await service.AuditGenericDecisionAsync(request);

        repository.Inserted.Single().Metadata["metadataTruncated"].Should().Be("true");
    }

    [Fact]
    public async Task AuditGenericDecisionAsync_DoesNotThrowWhenRepositoryFails()
    {
        var repository = new FakeRepository { ThrowOnInsert = true };
        var service = CreateService(repository);

        Func<Task> act = async () => await service.AuditGenericDecisionAsync(CreateRequest(AiDecisionAuditType.RetryDecision));

        await act.Should().NotThrowAsync();
    }



    [Fact]
    public async Task AuditGenericDecisionAsync_PublishingFailureDoesNotFailAuditFlow()
    {
        var repository = new FakeRepository();
        var producer = new Mock<IAiDecisionEventProducer>();
        producer
            .Setup(item => item.PublishAsync(It.IsAny<AiDecisionEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AiKafkaPublishResult.Failure(AiKafkaTopics.Decisions, "corr_1", "broker unavailable", DateTime.UtcNow));
        var service = CreateService(repository, producer: producer.Object);

        var record = await service.AuditGenericDecisionAsync(CreateRequest(AiDecisionAuditType.RetryDecision));

        record.Should().NotBeNull();
        repository.Inserted.Should().ContainSingle();
        producer.Verify(item => item.PublishAsync(It.Is<AiDecisionEventDto>(dto => dto.DecisionId == record!.DecisionId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuditGenericDecisionAsync_DoesNotInsertWhenAuditDisabled()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository, new AiDecisionAuditOptions { Enabled = false });

        var record = await service.AuditGenericDecisionAsync(CreateRequest(AiDecisionAuditType.RetryDecision));

        record.Should().BeNull();
        repository.Inserted.Should().BeEmpty();
    }

    [Fact]
    public async Task AuditHumanApprovalAsync_RespectsOptionToggle()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository, new AiDecisionAuditOptions { AuditHumanApprovals = false });

        var record = await service.AuditHumanApprovalAsync(CreateRequest(AiDecisionAuditType.HumanApproval));

        record.Should().BeNull();
        repository.Inserted.Should().BeEmpty();
    }

    [Fact]
    public async Task AuditSafeModeEvaluationAsync_RespectsOptionToggle()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository, new AiDecisionAuditOptions { AuditSafeModeEvaluations = false });

        var record = await service.AuditSafeModeEvaluationAsync(CreateRequest(AiDecisionAuditType.SafeModeEvaluation));

        record.Should().BeNull();
        repository.Inserted.Should().BeEmpty();
    }

    [Fact]
    public async Task AuditFallbackDecisionAsync_RespectsOptionToggle()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository, new AiDecisionAuditOptions { AuditFallbackDecisions = false });

        var record = await service.AuditFallbackDecisionAsync(CreateRequest(AiDecisionAuditType.FallbackDecision));

        record.Should().BeNull();
        repository.Inserted.Should().BeEmpty();
    }

    [Fact]
    public async Task AuditGenericDecisionAsync_ExcludesModelMetadataWhenDisabled()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository, new AiDecisionAuditOptions { IncludeModelMetadata = false });
        var request = CreateRequest(AiDecisionAuditType.RetryDecision);
        request.Model = "llama3";
        request.Provider = "Ollama";

        await service.AuditGenericDecisionAsync(request);

        repository.Inserted.Single().Model.Should().BeNull();
        repository.Inserted.Single().Provider.Should().BeNull();
    }

    [Fact]
    public async Task AuditGenericDecisionAsync_IncludesPromptAndModelMetadataWhenEnabled()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);
        var request = CreateRequest(AiDecisionAuditType.TransformationDecision);
        request.PromptName = "prompt";
        request.PromptVersion = "v1.0.0";
        request.PromptHash = "sha256:abc";
        request.Model = "llama3";
        request.Provider = "Ollama";

        await service.AuditGenericDecisionAsync(request);

        var record = repository.Inserted.Single();
        record.PromptName.Should().Be("prompt");
        record.PromptVersion.Should().Be("v1.0.0");
        record.PromptHash.Should().Be("sha256:abc");
        record.Model.Should().Be("llama3");
        record.Provider.Should().Be("Ollama");
    }

    [Fact]
    public async Task AuditGenericDecisionAsync_ExcludesPromptMetadataWhenDisabled()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository, new AiDecisionAuditOptions { IncludePromptMetadata = false });
        var request = CreateRequest(AiDecisionAuditType.TransformationDecision);
        request.PromptName = "prompt";
        request.PromptVersion = "v1.0.0";
        request.PromptHash = "sha256:abc";

        await service.AuditGenericDecisionAsync(request);

        repository.Inserted.Single().PromptName.Should().BeNull();
        repository.Inserted.Single().PromptVersion.Should().BeNull();
        repository.Inserted.Single().PromptHash.Should().BeNull();
    }

    [Fact]
    public void CreateIndexModels_IncludesUniqueAuditIdAndCompoundIndexes()
    {
        var indexes = AiDecisionAuditRepository.CreateIndexModels();

        indexes.Should().HaveCount(21);
        indexes.Should().Contain(index => index.Options != null && index.Options.Name == "ux_ai_decision_audit_records_audit_id" && index.Options.Unique == true);
        indexes.Should().Contain(index => index.Options != null && index.Options.Name == "idx_ai_decision_audit_records_event_id_created_at_desc");
    }

    private static AiDecisionAuditService CreateService(FakeRepository repository, AiDecisionAuditOptions? options = null, IAiDecisionEventProducer? producer = null)
        => new(repository, Options.Create(options ?? new AiDecisionAuditOptions()), NullLogger<AiDecisionAuditService>.Instance, producer);

    private static AiDecisionAuditCreateRequestDto CreateRequest(AiDecisionAuditType type) => new()
    {
        EventId = "evt_1",
        CorrelationId = "corr_1",
        CustomerId = "cust_1",
        DecisionType = type,
        AgentName = "RetryAgent",
        Decision = "RetryWithBackoff",
        ConfidenceScore = 0.82,
        ConfidenceLevel = "High",
        CreatedBy = "test"
    };

    private sealed class FakeRepository : IAiDecisionAuditRepository
    {
        public List<AiDecisionAuditRecord> Inserted { get; } = [];
        public bool ThrowOnInsert { get; set; }
        public Task InsertAsync(AiDecisionAuditRecord record, CancellationToken cancellationToken = default)
        {
            if (ThrowOnInsert) throw new InvalidOperationException("insert failed");
            Inserted.Add(record);
            return Task.CompletedTask;
        }
        public Task<AiDecisionAuditRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<AiDecisionAuditRecord?>(null);
        public Task<AiDecisionAuditRecord?> GetByAuditIdAsync(string auditId, CancellationToken cancellationToken = default) => Task.FromResult<AiDecisionAuditRecord?>(null);
        public Task<IReadOnlyList<AiDecisionAuditRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(Inserted.Where(r => r.EventId == eventId).ToList());
        public Task<IReadOnlyList<AiDecisionAuditRecord>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(Inserted.Where(r => r.CorrelationId == correlationId).ToList());
        public Task<IReadOnlyList<AiDecisionAuditRecord>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(Inserted.Where(r => r.CustomerId == customerId).ToList());
        public Task<IReadOnlyList<AiDecisionAuditRecord>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(Inserted.Take(limit).ToList());
        public Task<IReadOnlyList<AiDecisionAuditRecord>> SearchAsync(AiDecisionAuditSearchRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiDecisionAuditRecord>>(Inserted);
    }
}
