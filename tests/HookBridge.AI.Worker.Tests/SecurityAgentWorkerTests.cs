using FluentAssertions;
using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.SecurityAgent;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class SecurityAgentWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenTopicMissing_DoesNotConsume()
    {
        var consumer = new Mock<ISecurityAgentConsumer>();
        var worker = CreateWorker(consumer.Object, Mock.Of<ISecurityAgent>(), Mock.Of<ISecurityAgentResultRepository>(), Mock.Of<IAiRecommendationApprovalService>(), Mock.Of<IAiAnomalyProducer>(), topic: string.Empty);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        consumer.Verify(client => client.ConsumeAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_StoresLowRiskResultWithoutApprovalOrAnomaly()
    {
        var request = CreateRequest();
        var response = CreateResponse(AiRiskLevel.Low, SecurityAgentDecision.Allow, requiresApproval: false, score: 5);
        var agent = new Mock<ISecurityAgent>();
        var repository = new Mock<ISecurityAgentResultRepository>();
        var approval = new Mock<IAiRecommendationApprovalService>();
        var producer = new Mock<IAiAnomalyProducer>();
        var acknowledgeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        agent.Setup(client => client.AnalyzeAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(response);
        repository.Setup(client => client.InsertAsync(It.IsAny<SecurityAgentResult>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var worker = CreateWorker(new SingleMessageConsumer(request, acknowledgeSignal), agent.Object, repository.Object, approval.Object, producer.Object);

        await worker.StartAsync(CancellationToken.None);
        await acknowledgeSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        repository.Verify(client => client.InsertAsync(It.Is<SecurityAgentResult>(result => result.EventId == request.EventId && result.SecurityDecision == SecurityAgentDecision.Allow), It.IsAny<CancellationToken>()), Times.Once);
        approval.Verify(client => client.CreateAsync(It.IsAny<AiRecommendationApprovalCreateRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        producer.Verify(client => client.PublishAsync(It.IsAny<AiAnomalyEventDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesApprovalAndPublishesHighRiskAnomaly()
    {
        var request = CreateRequest();
        var response = CreateResponse(AiRiskLevel.High, SecurityAgentDecision.Quarantine, requiresApproval: true, score: 75);
        var agent = new Mock<ISecurityAgent>();
        var repository = new Mock<ISecurityAgentResultRepository>();
        var approval = new Mock<IAiRecommendationApprovalService>();
        var producer = new Mock<IAiAnomalyProducer>();
        var acknowledgeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var approvalSignal = new TaskCompletionSource<AiRecommendationApprovalCreateRequestDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var anomalySignal = new TaskCompletionSource<AiAnomalyEventDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        agent.Setup(client => client.AnalyzeAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(response);
        repository.Setup(client => client.InsertAsync(It.IsAny<SecurityAgentResult>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        approval.Setup(client => client.CreateAsync(It.IsAny<AiRecommendationApprovalCreateRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<AiRecommendationApprovalCreateRequestDto, CancellationToken>((dto, _) => approvalSignal.TrySetResult(dto))
            .ReturnsAsync(new AiRecommendationApprovalResponseDto { EventId = request.EventId, RecommendationType = AiRecommendationType.SecurityRecommendation });
        producer.Setup(client => client.PublishAsync(It.IsAny<AiAnomalyEventDto>(), It.IsAny<CancellationToken>()))
            .Callback<AiAnomalyEventDto, CancellationToken>((dto, _) => anomalySignal.TrySetResult(dto))
            .ReturnsAsync(AiKafkaPublishResult.Success(AiKafkaTopics.Anomalies, request.CorrelationId, 0, 1, DateTime.UtcNow));

        var worker = CreateWorker(new SingleMessageConsumer(request, acknowledgeSignal), agent.Object, repository.Object, approval.Object, producer.Object);

        await worker.StartAsync(CancellationToken.None);
        var approvalRequest = await approvalSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var anomaly = await anomalySignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await acknowledgeSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        approvalRequest.EventId.Should().Be(request.EventId);
        approvalRequest.RecommendationType.Should().Be(AiRecommendationType.SecurityRecommendation);
        approvalRequest.SuggestedAction.Should().Be(SecurityAgentDecision.Quarantine.ToString());
        anomaly.EventId.Should().Be(request.EventId);
        anomaly.RiskLevel.Should().Be(AiRiskLevel.High);
        anomaly.AnomalyScore.Should().Be(75);
        anomaly.Source.Should().Be("HookBridge.AI.SecurityAgent");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequestDoesNotPersist()
    {
        var request = CreateRequest();
        var agent = new Mock<ISecurityAgent>();
        var repository = new Mock<ISecurityAgentResultRepository>();
        var acknowledgeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        agent.Setup(client => client.AnalyzeAsync(request, It.IsAny<CancellationToken>())).ThrowsAsync(new ArgumentException("invalid request"));

        var worker = CreateWorker(new SingleMessageConsumer(request, acknowledgeSignal), agent.Object, repository.Object, Mock.Of<IAiRecommendationApprovalService>(), Mock.Of<IAiAnomalyProducer>());

        await worker.StartAsync(CancellationToken.None);
        await acknowledgeSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        repository.Verify(client => client.InsertAsync(It.IsAny<SecurityAgentResult>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static SecurityAgentWorker CreateWorker(
        ISecurityAgentConsumer consumer,
        ISecurityAgent agent,
        ISecurityAgentResultRepository repository,
        IAiRecommendationApprovalService approval,
        IAiAnomalyProducer producer,
        string topic = AiKafkaTopics.SecurityAgent,
        bool publishHighRisk = true,
        bool publishCriticalRisk = true)
        => new(
            consumer,
            agent,
            repository,
            approval,
            producer,
            new TestLogger<SecurityAgentWorker>(),
            Options.Create(new AiKafkaOptions
            {
                BootstrapServers = "localhost:9092",
                SecurityProtocol = "Plaintext",
                SecurityAgentTopic = topic,
                AnomaliesTopic = AiKafkaTopics.Anomalies,
                ConsumerGroupId = "hookbridge-ai-tests"
            }),
            Options.Create(new SecurityAgentOptions
            {
                PublishAnomalyForHighRisk = publishHighRisk,
                PublishAnomalyForCriticalRisk = publishCriticalRisk
            }));

    private static SecurityAgentRequestDto CreateRequest() => new()
    {
        EventId = "evt-1",
        CorrelationId = "corr-1",
        CustomerId = "cust-1",
        CustomerIdType = "Tenant",
        SubscriptionId = "sub-1",
        EndpointId = "endpoint-1",
        Environment = "qa",
        EventType = "OrderCreated",
        TargetUrl = "https://customer.example.com/webhook",
        HttpMethod = "POST",
        ReceivedAtUtc = new DateTime(2026, 5, 14, 10, 30, 0, DateTimeKind.Utc)
    };

    private static SecurityAgentResponseDto CreateResponse(AiRiskLevel riskLevel, SecurityAgentDecision decision, bool requiresApproval, int score) => new()
    {
        EventId = "evt-1",
        CorrelationId = "corr-1",
        IsSuspicious = riskLevel >= AiRiskLevel.High,
        SecurityDecision = decision,
        SecurityRiskScore = score,
        RiskLevel = riskLevel,
        RequiresApproval = requiresApproval,
        Summary = "Security agent complete.",
        Recommendation = "Review if suspicious.",
        ConfidenceScore = 0.8,
        GeneratedAtUtc = new DateTime(2026, 5, 14, 10, 31, 0, DateTimeKind.Utc)
    };

    private sealed class SingleMessageConsumer : ISecurityAgentConsumer
    {
        private readonly SecurityAgentRequestDto _request;
        private readonly TaskCompletionSource _acknowledgeSignal;

        public SingleMessageConsumer(SecurityAgentRequestDto request, TaskCompletionSource acknowledgeSignal)
        {
            _request = request;
            _acknowledgeSignal = acknowledgeSignal;
        }

        public async IAsyncEnumerable<SecurityAgentRequestDto> ConsumeAsync(CancellationToken cancellationToken = default)
        {
            yield return _request;
            _acknowledgeSignal.TrySetResult();
            await Task.Yield();
        }
    }
}
