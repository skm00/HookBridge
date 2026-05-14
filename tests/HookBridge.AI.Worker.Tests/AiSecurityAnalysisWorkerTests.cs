using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.SecurityAnalysis;
using Microsoft.Extensions.Options;
using Moq;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiSecurityAnalysisWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenTopicMissing_DoesNotConsume()
    {
        var consumer = new Mock<IAiSecurityAnalysisConsumer>();
        var worker = CreateWorker(consumer.Object, Mock.Of<IAiSecurityAnalysisAgent>(), Mock.Of<IAiSecurityAnalysisRepository>(), Mock.Of<IAiAnomalyProducer>(), string.Empty);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        consumer.Verify(client => client.ConsumeAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_PersistsNonSuspiciousResultWithoutPublishingAnomaly()
    {
        var request = CreateRequest();
        var agent = new Mock<IAiSecurityAnalysisAgent>();
        var repository = new Mock<IAiSecurityAnalysisRepository>();
        var producer = new Mock<IAiAnomalyProducer>();
        var acknowledgeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        agent.Setup(client => client.AnalyzeAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(CreateResponse(isSuspicious: false));
        repository.Setup(client => client.InsertAsync(It.IsAny<AiSecurityAnalysisResult>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var worker = CreateWorker(new SingleMessageConsumer(request, acknowledgeSignal), agent.Object, repository.Object, producer.Object, AiKafkaTopics.SecurityAnalysis);

        await worker.StartAsync(CancellationToken.None);
        await acknowledgeSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        repository.Verify(client => client.InsertAsync(It.Is<AiSecurityAnalysisResult>(result => result.EventId == request.EventId && !result.IsSuspicious), It.IsAny<CancellationToken>()), Times.Once);
        producer.Verify(client => client.PublishAsync(It.IsAny<AiAnomalyEventDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_PublishesSuspiciousResultToAnomaliesTopic()
    {
        var request = CreateRequest();
        var response = CreateResponse(isSuspicious: true);
        var agent = new Mock<IAiSecurityAnalysisAgent>();
        var repository = new Mock<IAiSecurityAnalysisRepository>();
        var producer = new Mock<IAiAnomalyProducer>();
        var acknowledgeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publishSignal = new TaskCompletionSource<AiAnomalyEventDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        agent.Setup(client => client.AnalyzeAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(response);
        repository.Setup(client => client.InsertAsync(It.IsAny<AiSecurityAnalysisResult>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        producer.Setup(client => client.PublishAsync(It.IsAny<AiAnomalyEventDto>(), It.IsAny<CancellationToken>()))
            .Callback<AiAnomalyEventDto, CancellationToken>((dto, _) => publishSignal.TrySetResult(dto))
            .ReturnsAsync(AiKafkaPublishResult.Success(AiKafkaTopics.Anomalies, request.CorrelationId, 0, 1, DateTime.UtcNow));

        var worker = CreateWorker(new SingleMessageConsumer(request, acknowledgeSignal), agent.Object, repository.Object, producer.Object, AiKafkaTopics.SecurityAnalysis);

        await worker.StartAsync(CancellationToken.None);
        var published = await publishSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await acknowledgeSignal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        published.AnomalyId.Should().Be("sec_corr-1");
        published.AnomalyType.Should().Be(AiAnomalyType.SuspiciousPayloadSpike);
        published.AnomalyScore.Should().Be(response.SecurityRiskScore);
    }

    private static AiSecurityAnalysisWorker CreateWorker(IAiSecurityAnalysisConsumer consumer, IAiSecurityAnalysisAgent agent, IAiSecurityAnalysisRepository repository, IAiAnomalyProducer producer, string topic)
        => new(
            consumer,
            agent,
            repository,
            producer,
            new TestLogger<AiSecurityAnalysisWorker>(),
            Options.Create(new AiKafkaOptions
            {
                BootstrapServers = "localhost:9092",
                SecurityProtocol = "Plaintext",
                SecurityAnalysisTopic = topic,
                AnomaliesTopic = AiKafkaTopics.Anomalies,
                ConsumerGroupId = "hookbridge-ai-tests"
            }));

    private static AiSecurityAnalysisRequestDto CreateRequest() => new()
    {
        EventId = "evt-1",
        CorrelationId = "corr-1",
        CustomerId = "cust-1",
        SubscriptionId = "sub-1",
        EndpointId = "endpoint-1",
        Environment = "qa",
        EventType = "OrderCreated",
        TargetUrl = "https://customer.example.com/webhook",
        ReceivedAtUtc = new DateTime(2026, 5, 14, 10, 30, 0, DateTimeKind.Utc)
    };

    private static AiSecurityAnalysisResponseDto CreateResponse(bool isSuspicious) => new()
    {
        EventId = "evt-1",
        CorrelationId = "corr-1",
        IsSuspicious = isSuspicious,
        SecurityRiskScore = isSuspicious ? 75 : 5,
        RiskLevel = isSuspicious ? AiRiskLevel.High : AiRiskLevel.Low,
        Summary = "Security analysis complete.",
        Recommendation = "Review if suspicious.",
        SuggestedAction = isSuspicious ? AiSecuritySuggestedAction.Quarantine : AiSecuritySuggestedAction.Allow,
        ConfidenceScore = 0.8,
        GeneratedAtUtc = new DateTime(2026, 5, 14, 10, 31, 0, DateTimeKind.Utc),
        Provider = "Ollama",
        Model = "llama3"
    };

    private sealed class SingleMessageConsumer : IAiSecurityAnalysisConsumer
    {
        private readonly AiSecurityAnalysisRequestDto _request;
        private readonly TaskCompletionSource _acknowledgeSignal;

        public SingleMessageConsumer(AiSecurityAnalysisRequestDto request, TaskCompletionSource acknowledgeSignal)
        {
            _request = request;
            _acknowledgeSignal = acknowledgeSignal;
        }

        public async IAsyncEnumerable<AiSecurityAnalysisRequestDto> ConsumeAsync(CancellationToken cancellationToken = default)
        {
            yield return _request;
            _acknowledgeSignal.TrySetResult();
            await Task.Yield();
        }
    }
}
