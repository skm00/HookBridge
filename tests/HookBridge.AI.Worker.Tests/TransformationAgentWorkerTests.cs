using FluentAssertions;
using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.TransformationAgent;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class TransformationAgentWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenTopicMissing_DoesNotConsume()
    {
        var consumer = new Mock<ITransformationAgentConsumer>();
        var worker = CreateWorker(consumer.Object, Mock.Of<ITransformationAgent>(), Mock.Of<ITransformationAgentResultRepository>(), Mock.Of<IAiRecommendationApprovalService>(), Mock.Of<IAiAnomalyProducer>(), topic: string.Empty);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        consumer.Verify(client => client.ConsumeAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_StoresLowRiskResultWithoutApprovalOrAnomaly()
    {
        var request = CreateRequest();
        var response = CreateResponse("Low", TransformationAgentDecision.MappingReady, requiresApproval: false);
        var agent = new Mock<ITransformationAgent>();
        var repository = new Mock<ITransformationAgentResultRepository>();
        var approval = new Mock<IAiRecommendationApprovalService>();
        var producer = new Mock<IAiAnomalyProducer>();
        var acknowledgeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        agent.Setup(client => client.AnalyzeAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(response);
        repository.Setup(client => client.InsertAsync(It.IsAny<TransformationAgentResult>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var worker = CreateWorker(new SingleMessageConsumer(request, acknowledgeSignal), agent.Object, repository.Object, approval.Object, producer.Object);

        await worker.StartAsync(CancellationToken.None);
        await acknowledgeSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        repository.Verify(client => client.InsertAsync(It.Is<TransformationAgentResult>(result => result.EventId == request.EventId && result.TransformationDecision == TransformationAgentDecision.MappingReady), It.IsAny<CancellationToken>()), Times.Once);
        approval.Verify(client => client.CreateAsync(It.IsAny<AiRecommendationApprovalCreateRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        producer.Verify(client => client.PublishAsync(It.IsAny<AiAnomalyEventDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("High", 80)]
    [InlineData("Critical", 95)]
    public async Task ExecuteAsync_CreatesApprovalAndPublishesHighOrCriticalRiskAnomaly(string riskLevel, int expectedScore)
    {
        var request = CreateRequest();
        var response = CreateResponse(riskLevel, TransformationAgentDecision.MappingNeedsReview, requiresApproval: true);
        var agent = new Mock<ITransformationAgent>();
        var repository = new Mock<ITransformationAgentResultRepository>();
        var approval = new Mock<IAiRecommendationApprovalService>();
        var producer = new Mock<IAiAnomalyProducer>();
        var acknowledgeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var approvalSignal = new TaskCompletionSource<AiRecommendationApprovalCreateRequestDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var anomalySignal = new TaskCompletionSource<AiAnomalyEventDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        agent.Setup(client => client.AnalyzeAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(response);
        repository.Setup(client => client.InsertAsync(It.IsAny<TransformationAgentResult>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        approval.Setup(client => client.CreateAsync(It.IsAny<AiRecommendationApprovalCreateRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<AiRecommendationApprovalCreateRequestDto, CancellationToken>((dto, _) => approvalSignal.TrySetResult(dto))
            .ReturnsAsync(new AiRecommendationApprovalResponseDto { EventId = request.EventId, RecommendationType = AiRecommendationType.TransformationRecommendation });
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
        approvalRequest.RecommendationType.Should().Be(AiRecommendationType.TransformationRecommendation);
        approvalRequest.SuggestedAction.Should().Be(TransformationAgentDecision.MappingNeedsReview.ToString());
        anomaly.EventId.Should().Be(request.EventId);
        anomaly.RiskLevel.ToString().Should().Be(riskLevel);
        anomaly.AnomalyScore.Should().Be(expectedScore);
        anomaly.Source.Should().Be("HookBridge.AI.TransformationAgent");
    }

    [Fact]
    public void AiKafkaTopics_TransformationAgent_HasExpectedValue()
    {
        AiKafkaTopics.TransformationAgent.Should().Be("hookbridge.ai.transformation-agent");
        new AiKafkaOptions().TransformationAgentTopic.Should().Be(AiKafkaTopics.TransformationAgent);
    }

    private static TransformationAgentWorker CreateWorker(
        ITransformationAgentConsumer consumer,
        ITransformationAgent agent,
        ITransformationAgentResultRepository repository,
        IAiRecommendationApprovalService approval,
        IAiAnomalyProducer producer,
        string topic = AiKafkaTopics.TransformationAgent)
        => new(
            consumer,
            agent,
            repository,
            approval,
            producer,
            new TestLogger<TransformationAgentWorker>(),
            Options.Create(new AiKafkaOptions
            {
                BootstrapServers = "localhost:9092",
                SecurityProtocol = "Plaintext",
                TransformationAgentTopic = topic,
                AnomaliesTopic = AiKafkaTopics.Anomalies,
                ConsumerGroupId = "hookbridge-ai-tests"
            }));

    private static TransformationAgentRequestDto CreateRequest() => new()
    {
        EventId = "evt-1",
        CorrelationId = "corr-1",
        CustomerId = "cust-1",
        CustomerIdType = "Tenant",
        SubscriptionId = "sub-1",
        EndpointId = "endpoint-1",
        Environment = "qa",
        EventType = "OrderCreated",
        Source = "Shopify",
        SourcePayload = new { id = "evt-1" },
        TargetSamplePayload = new { id = "string" },
        ReceivedAtUtc = new DateTime(2026, 5, 14, 10, 30, 0, DateTimeKind.Utc)
    };

    private static TransformationAgentResponseDto CreateResponse(string riskLevel, TransformationAgentDecision decision, bool requiresApproval) => new()
    {
        EventId = "evt-1",
        CorrelationId = "corr-1",
        TransformationDecision = decision,
        RiskLevel = riskLevel,
        RequiresApproval = requiresApproval,
        Summary = "Transformation agent complete.",
        Recommendation = "Review mappings before production use.",
        RecommendedMappings = [new WebhookFieldMappingRecommendationDto { SourceJsonPath = "$.id", TargetJsonPath = "$.id", SourceFieldName = "id", TargetFieldName = "id", TransformationType = WebhookTransformationType.DirectMap, ConfidenceScore = 0.9 }],
        ReasonCodes = [TransformationAgentReasonCode.DirectMappingAvailable],
        ConfidenceScore = 0.9,
        GeneratedAtUtc = new DateTime(2026, 5, 14, 10, 31, 0, DateTimeKind.Utc)
    };

    private sealed class SingleMessageConsumer : ITransformationAgentConsumer
    {
        private readonly TransformationAgentRequestDto _request;
        private readonly TaskCompletionSource _acknowledgeSignal;

        public SingleMessageConsumer(TransformationAgentRequestDto request, TaskCompletionSource acknowledgeSignal)
        {
            _request = request;
            _acknowledgeSignal = acknowledgeSignal;
        }

        public async IAsyncEnumerable<TransformationAgentRequestDto> ConsumeAsync(CancellationToken cancellationToken = default)
        {
            yield return _request;
            _acknowledgeSignal.TrySetResult();
            await Task.Yield();
        }
    }
}
