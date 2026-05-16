using FluentAssertions;
using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.ObservabilityAgent;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class ObservabilityAgentWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_CriticalResultStoresApprovalAndPublishesAnomaly()
    {
        var request = CreateRequest();
        var response = CreateResponse(ObservabilityStatus.Critical, AiRiskLevel.Critical, requiresApproval: true);
        var acknowledgeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var approvalSignal = new TaskCompletionSource<AiRecommendationApprovalCreateRequestDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var anomalySignal = new TaskCompletionSource<AiAnomalyEventDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repositorySignal = new TaskCompletionSource<ObservabilityAgentResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = new Mock<IObservabilityAgent>();
        var repository = new Mock<IObservabilityAgentResultRepository>();
        var approval = new Mock<IAiRecommendationApprovalService>();
        var producer = new Mock<IAiAnomalyProducer>();
        agent.Setup(client => client.AnalyzeAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(response);
        repository.Setup(client => client.InsertAsync(It.IsAny<ObservabilityAgentResult>(), It.IsAny<CancellationToken>()))
            .Callback<ObservabilityAgentResult, CancellationToken>((result, _) => repositorySignal.TrySetResult(result))
            .Returns(Task.CompletedTask);
        approval.Setup(client => client.CreateAsync(It.IsAny<AiRecommendationApprovalCreateRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<AiRecommendationApprovalCreateRequestDto, CancellationToken>((dto, _) => approvalSignal.TrySetResult(dto))
            .ReturnsAsync(new AiRecommendationApprovalResponseDto { EventId = request.EventId, RecommendationType = AiRecommendationType.AnomalyRecommendation });
        producer.Setup(client => client.PublishAsync(It.IsAny<AiAnomalyEventDto>(), It.IsAny<CancellationToken>()))
            .Callback<AiAnomalyEventDto, CancellationToken>((dto, _) => anomalySignal.TrySetResult(dto))
            .ReturnsAsync(AiKafkaPublishResult.Success(AiKafkaTopics.Anomalies, request.CorrelationId, 0, 1, DateTime.UtcNow));

        var worker = CreateWorker(new SingleMessageConsumer(request, acknowledgeSignal), agent.Object, repository.Object, approval.Object, producer.Object);

        await worker.StartAsync(CancellationToken.None);
        var stored = await repositorySignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var approvalRequest = await approvalSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var anomaly = await anomalySignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await acknowledgeSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        stored.EventId.Should().Be(request.EventId);
        approvalRequest.RecommendationType.Should().Be(AiRecommendationType.AnomalyRecommendation);
        anomaly.Source.Should().Be("HookBridge.AI.ObservabilityAgent");
        anomaly.RiskLevel.Should().Be(AiRiskLevel.Critical);
    }

    private static ObservabilityAgentWorker CreateWorker(IObservabilityAgentConsumer consumer, IObservabilityAgent agent, IObservabilityAgentResultRepository repository, IAiRecommendationApprovalService approval, IAiAnomalyProducer producer)
        => new(consumer, agent, repository, approval, producer, new TestLogger<ObservabilityAgentWorker>(), Options.Create(new AiKafkaOptions
        {
            BootstrapServers = "localhost:9092",
            SecurityProtocol = "Plaintext",
            ObservabilityAgentTopic = AiKafkaTopics.ObservabilityAgent,
            AnomaliesTopic = AiKafkaTopics.Anomalies,
            ConsumerGroupId = "hookbridge-ai-tests"
        }));

    private static ObservabilityAgentRequestDto CreateRequest() => new()
    {
        EventId = "evt-1",
        CorrelationId = "corr-1",
        Environment = "qa",
        ServiceName = "HookBridge.AI.Worker",
        CustomerId = "cust-1",
        SubscriptionId = "sub-1",
        EndpointId = "endpoint-1",
        MongoIsHealthy = true,
        EvaluationWindowFromUtc = new DateTime(2026, 5, 14, 10, 0, 0, DateTimeKind.Utc),
        EvaluationWindowToUtc = new DateTime(2026, 5, 14, 10, 15, 0, DateTimeKind.Utc),
        CreatedAtUtc = new DateTime(2026, 5, 14, 10, 16, 0, DateTimeKind.Utc)
    };

    private static ObservabilityAgentResponseDto CreateResponse(ObservabilityStatus status, AiRiskLevel riskLevel, bool requiresApproval) => new()
    {
        EventId = "evt-1",
        CorrelationId = "corr-1",
        Environment = "qa",
        ServiceName = "HookBridge.AI.Worker",
        ObservabilityStatus = status,
        RiskLevel = riskLevel,
        Summary = "Critical telemetry detected.",
        Recommendation = "Investigate.",
        SuggestedActions = [ObservabilitySuggestedAction.RequireManualReview],
        ConfidenceScore = 0.9,
        RequiresApproval = requiresApproval,
        GeneratedAtUtc = new DateTime(2026, 5, 14, 10, 16, 30, DateTimeKind.Utc)
    };

    private sealed class SingleMessageConsumer : IObservabilityAgentConsumer
    {
        private readonly ObservabilityAgentRequestDto _request;
        private readonly TaskCompletionSource _acknowledgeSignal;

        public SingleMessageConsumer(ObservabilityAgentRequestDto request, TaskCompletionSource acknowledgeSignal)
        {
            _request = request;
            _acknowledgeSignal = acknowledgeSignal;
        }

        public async IAsyncEnumerable<ObservabilityAgentRequestDto> ConsumeAsync(CancellationToken cancellationToken = default)
        {
            yield return _request;
            _acknowledgeSignal.TrySetResult();
            await Task.Yield();
        }
    }
}
