using FluentAssertions;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services;
using HookBridge.AI.Worker.Services.RetryRecommendations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace HookBridge.AI.Worker.Tests;

public sealed class AiProcessingWorkerTests
{
    [Fact]
    public async Task StartAsync_LogsStartupMessage()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var kernelFactory = new TestKernelFactory();
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()), kernelFactory, new TestAiAnalysisConsumer(), new TestAiAnalysisResultRepository(), new TestAiRetryRecommendationService());

        await worker.StartAsync(CancellationToken.None);
        await WaitForLogAsync(logger, "HookBridge AI Worker starting");
        await worker.StopAsync(CancellationToken.None);

        logger.Records.Should().Contain(record =>
            record.Level == LogLevel.Information &&
            record.Message.Contains("HookBridge AI Worker starting", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StopAsync_AfterStart_LogsShutdownMessage()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var kernelFactory = new TestKernelFactory();
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()), kernelFactory, new TestAiAnalysisConsumer(), new TestAiAnalysisResultRepository(), new TestAiRetryRecommendationService());

        await worker.StartAsync(CancellationToken.None);
        await WaitForLogAsync(logger, "HookBridge AI Worker starting");
        await worker.StopAsync(CancellationToken.None);
        await WaitForLogAsync(logger, "HookBridge AI Worker shutting down");

        logger.Records.Should().Contain(record =>
            record.Level == LogLevel.Information &&
            record.Message.Contains("HookBridge AI Worker shutting down", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartAsync_RunsUntilCancellation()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var kernelFactory = new TestKernelFactory();
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()), kernelFactory, new TestAiAnalysisConsumer(), new TestAiAnalysisResultRepository(), new TestAiRetryRecommendationService());

        using var cancellation = new CancellationTokenSource();
        await worker.StartAsync(cancellation.Token);
        await WaitForLogAsync(logger, "HookBridge AI Worker starting");

        logger.Records.Should().NotContain(record =>
            record.Message.Contains("shutting down", StringComparison.OrdinalIgnoreCase));

        await worker.StopAsync(CancellationToken.None);
        await WaitForLogAsync(logger, "HookBridge AI Worker shutting down");

        logger.Records.Should().Contain(record =>
            record.Message.Contains("shutting down", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StartAsync_WhenAiDisabled_DoesNotInitializeSemanticKernel()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var kernelFactory = new TestKernelFactory();
        var worker = new AiProcessingWorker(
            logger,
            Options.Create(new AiOptions { Enabled = false }),
            kernelFactory,
            new TestAiAnalysisConsumer(), new TestAiAnalysisResultRepository(), new TestAiRetryRecommendationService());

        await worker.StartAsync(CancellationToken.None);
        await WaitForLogAsync(logger, "AI is disabled");
        await worker.StopAsync(CancellationToken.None);

        kernelFactory.CreateKernelCallCount.Should().Be(0);
        logger.Records.Should().Contain(record =>
            record.Level == LogLevel.Information &&
            record.Message.Contains("AI is disabled", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartAsync_WhenAiEnabled_VerifiesSemanticKernelCreation()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var kernelFactory = new TestKernelFactory();
        var worker = new AiProcessingWorker(logger, Options.Create(new AiOptions()), kernelFactory, new TestAiAnalysisConsumer(), new TestAiAnalysisResultRepository(), new TestAiRetryRecommendationService());

        await worker.StartAsync(CancellationToken.None);
        await kernelFactory.WaitForCreateKernelAsync();
        await WaitForLogAsync(logger, "Semantic Kernel startup verification completed");
        await worker.StopAsync(CancellationToken.None);

        kernelFactory.CreateKernelCallCount.Should().Be(1);
        logger.Records.Should().Contain(record =>
            record.Level == LogLevel.Information &&
            record.Message.Contains("Semantic Kernel startup verification completed", StringComparison.Ordinal));
    }


    [Fact]
    public async Task StartAsync_WhenEventConsumed_StoresAiRetryRecommendationResult()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var kernelFactory = new TestKernelFactory();
        var repository = new TestAiAnalysisResultRepository();
        var analysisEvent = new AiAnalysisEventDto
        {
            EventId = "evt-123",
            CorrelationId = "corr-123",
            Source = "unit-test",
            EventType = "webhook.delivery.failed",
            FailureReason = "HTTP 500",
            Payload = "{}",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var consumer = new TestAiAnalysisConsumer(analysisEvent);
        var worker = new AiProcessingWorker(
            logger,
            Options.Create(new AiOptions { Provider = "Ollama", Model = "llama3.1" }),
            kernelFactory,
            consumer,
            repository,
            new TestAiRetryRecommendationService());

        await worker.StartAsync(CancellationToken.None);
        await repository.WaitForInsertAsync();
        await worker.StopAsync(CancellationToken.None);

        repository.InsertedResults.Should().ContainSingle();
        var result = repository.InsertedResults.Single();
        result.EventId.Should().Be("evt-123");
        result.CorrelationId.Should().Be("corr-123");
        result.Source.Should().Be("unit-test");
        result.EventType.Should().Be("webhook.delivery.failed");
        result.FailureReason.Should().Be("HTTP 500");
        result.AiSummary.Should().Contain("test recommendation");
        result.AiRecommendation.Should().NotBeNullOrWhiteSpace();
        result.RiskLevel.Should().Be("Medium");
        result.ConfidenceScore.Should().Be(0.8);
        result.Provider.Should().Be("Ollama");
        result.Model.Should().Be("llama3.1");
        result.CreatedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
    }


    [Fact]
    public async Task StartAsync_WhenAiEnabled_LogsProviderAndModel()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var worker = new AiProcessingWorker(
            logger,
            Options.Create(new AiOptions { Enabled = true, Provider = "Ollama", Model = "llama3" }),
            new TestKernelFactory(),
            new TestAiAnalysisConsumer(),
            new TestAiAnalysisResultRepository(),
            new TestAiRetryRecommendationService(),
            Options.Create(new AiKafkaOptions { AiAnalysisTopic = AiKafkaTopics.Analysis, ConsumerGroupId = "hookbridge-ai-tests" }));

        await worker.StartAsync(CancellationToken.None);
        await WaitForLogAsync(logger, "HookBridge AI Worker AI enabled");
        await worker.StopAsync(CancellationToken.None);

        logger.Records.Any(record =>
            record.Level == LogLevel.Information &&
            record.Properties.TryGetValue("Provider", out var provider) && provider?.ToString() == "Ollama" &&
            record.Properties.TryGetValue("Model", out var model) && model?.ToString() == "llama3")
            .Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WhenAiDisabled_LogsDisabledStatus()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var worker = new AiProcessingWorker(
            logger,
            Options.Create(new AiOptions { Enabled = false, Provider = "Ollama", Model = "llama3" }),
            new TestKernelFactory(),
            new TestAiAnalysisConsumer(),
            new TestAiAnalysisResultRepository(),
            new TestAiRetryRecommendationService(),
            Options.Create(new AiKafkaOptions { AiAnalysisTopic = AiKafkaTopics.Analysis, ConsumerGroupId = "hookbridge-ai-tests" }));

        await worker.StartAsync(CancellationToken.None);
        await WaitForLogAsync(logger, "HookBridge AI Worker AI is disabled");
        await worker.StopAsync(CancellationToken.None);

        logger.Records.Should().Contain(record =>
            record.Level == LogLevel.Information &&
            record.Message.Contains("AI is disabled", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartAsync_WhenFallbackUsed_LogsWarning()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var repository = new TestAiAnalysisResultRepository();
        var worker = new AiProcessingWorker(
            logger,
            Options.Create(new AiOptions { Provider = "Ollama", Model = "llama3" }),
            new TestKernelFactory(),
            new TestAiAnalysisConsumer(CreateAnalysisEvent()),
            repository,
            new TestAiRetryRecommendationService(fallbackUsed: true),
            Options.Create(new AiKafkaOptions { AiAnalysisTopic = AiKafkaTopics.Analysis, ConsumerGroupId = "hookbridge-ai-tests" }));

        await worker.StartAsync(CancellationToken.None);
        await repository.WaitForInsertAsync();
        await worker.StopAsync(CancellationToken.None);

        logger.Records.Any(record =>
            record.Level == LogLevel.Warning &&
            record.Message.Contains("AI fallback used", StringComparison.Ordinal) &&
            record.Properties.TryGetValue("FallbackUsed", out var fallbackUsed) && fallbackUsed is true &&
            record.Properties.TryGetValue("FallbackReason", out var fallbackReason) && fallbackReason?.ToString() == AiFallbackReason.ProviderUnavailable.ToString())
            .Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WhenProcessingFails_LogsErrorWithExceptionAndMetadata()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var worker = new AiProcessingWorker(
            logger,
            Options.Create(new AiOptions { Provider = "Ollama", Model = "llama3" }),
            new TestKernelFactory(),
            new TestAiAnalysisConsumer(CreateAnalysisEvent()),
            new TestAiAnalysisResultRepository(),
            new TestAiRetryRecommendationService(exception: new InvalidOperationException("boom")),
            Options.Create(new AiKafkaOptions { AiAnalysisTopic = AiKafkaTopics.Analysis, ConsumerGroupId = "hookbridge-ai-tests" }));

        await worker.StartAsync(CancellationToken.None);
        await WaitForLogAsync(logger, "AI analysis processing failed");
        await worker.StopAsync(CancellationToken.None);

        logger.Records.Any(record =>
            record.Level == LogLevel.Error &&
            record.Exception is InvalidOperationException &&
            record.Properties.TryGetValue("Operation", out var operation) && operation?.ToString() == "MessageProcessing" &&
            record.Properties.TryGetValue("EventId", out var eventId) && eventId?.ToString() == "evt-123" &&
            record.Properties.TryGetValue("CorrelationId", out var correlationId) && correlationId?.ToString() == "corr-123")
            .Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WhenProcessingSucceeds_LogsDurationAndScope()
    {
        var logger = new TestLogger<AiProcessingWorker>();
        var repository = new TestAiAnalysisResultRepository();
        var worker = new AiProcessingWorker(
            logger,
            Options.Create(new AiOptions { Provider = "Ollama", Model = "llama3" }),
            new TestKernelFactory(),
            new TestAiAnalysisConsumer(CreateAnalysisEvent()),
            repository,
            new TestAiRetryRecommendationService(),
            Options.Create(new AiKafkaOptions { AiAnalysisTopic = AiKafkaTopics.Analysis, ConsumerGroupId = "hookbridge-ai-tests" }));

        await worker.StartAsync(CancellationToken.None);
        await repository.WaitForInsertAsync();
        await WaitForLogAsync(logger, "AI analysis processing completed");
        await worker.StopAsync(CancellationToken.None);

        logger.Records.Any(record =>
            record.Level == LogLevel.Information &&
            record.Message.Contains("AI analysis processing completed", StringComparison.Ordinal) &&
            record.Properties.TryGetValue("DurationMs", out var durationMs) &&
            long.TryParse(durationMs?.ToString(), out var parsedDuration) && parsedDuration >= 0)
            .Should().BeTrue();
        logger.Scopes.Any(scope =>
            scope.TryGetValue("EventId", out var eventId) && eventId?.ToString() == "evt-123" &&
            scope.TryGetValue("CorrelationId", out var correlationId) && correlationId?.ToString() == "corr-123")
            .Should().BeTrue();
    }

    private static AiAnalysisEventDto CreateAnalysisEvent() => new()
    {
        EventId = "evt-123",
        CorrelationId = "corr-123",
        Source = "unit-test",
        EventType = "webhook.delivery.failed",
        FailureReason = "HTTP 500",
        Payload = "{\"authorization\":\"secret-token\"}",
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    private static async Task WaitForLogAsync<T>(TestLogger<T> logger, string message)
    {
        await WaitForConditionAsync(() => logger.Records.Any(record =>
            record.Message.Contains(message, StringComparison.Ordinal)));
    }

    private static async Task WaitForConditionAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (!predicate())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
        }
    }

    private sealed class TestAiRetryRecommendationService : IAiRetryRecommendationService
    {
        private readonly bool _fallbackUsed;
        private readonly Exception? _exception;

        public TestAiRetryRecommendationService(bool fallbackUsed = false, Exception? exception = null)
        {
            _fallbackUsed = fallbackUsed;
            _exception = exception;
        }

        public Task<WebhookFailureAnalysisResponseDto> AnalyzeAsync(
            WebhookFailureAnalysisRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(new WebhookFailureAnalysisResponseDto
            {
                EventId = request.EventId,
                CorrelationId = request.CorrelationId,
                AiSummary = $"test recommendation for {request.FailureReason}",
                RootCause = request.FailureReason ?? string.Empty,
                AiRecommendation = "Retry with exponential backoff after checking endpoint health.",
                RiskLevel = AiRiskLevel.Medium,
                ConfidenceScore = 0.8,
                SuggestedRetryAction = SuggestedRetryAction.RetryWithBackoff,
                IsRetryRecommended = true,
                GeneratedAtUtc = DateTime.UtcNow,
                Model = "llama3.1",
                Provider = "Ollama",
                Fallback = _fallbackUsed
                    ? new AiFallbackMetadataDto
                    {
                        UsedFallback = true,
                        FallbackReason = AiFallbackReason.ProviderUnavailable,
                        FallbackMessage = "Provider unavailable.",
                        Provider = "Ollama",
                        Model = "llama3.1"
                    }
                    : null
            });
        }
    }

    private sealed class TestAiAnalysisConsumer : IAiAnalysisConsumer
    {
        private readonly AiAnalysisEventDto? _analysisEvent;

        public TestAiAnalysisConsumer(AiAnalysisEventDto? analysisEvent = null)
        {
            _analysisEvent = analysisEvent;
        }

        public async IAsyncEnumerable<AiAnalysisEventDto> ConsumeAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_analysisEvent is not null)
            {
                yield return _analysisEvent;
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            yield break;
        }
    }

    private sealed class TestAiAnalysisResultRepository : IAiAnalysisResultRepository
    {
        private readonly TaskCompletionSource _insertCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<AiAnalysisResult> InsertedResults { get; } = new();

        public Task InsertAsync(AiAnalysisResult result, CancellationToken cancellationToken = default)
        {
            InsertedResults.Add(result);
            _insertCalled.TrySetResult();
            return Task.CompletedTask;
        }

        public Task<AiAnalysisResult?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<AiAnalysisResult?>(null);

        public Task<AiAnalysisResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default) => Task.FromResult<AiAnalysisResult?>(null);

        public Task<IReadOnlyList<AiAnalysisResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnalysisResult>>(Array.Empty<AiAnalysisResult>());

        public Task<IReadOnlyList<AiAnalysisResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnalysisResult>>(Array.Empty<AiAnalysisResult>());

        public Task WaitForInsertAsync() => _insertCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class TestKernelFactory : IKernelFactory
    {
        private readonly TaskCompletionSource _createKernelCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _createKernelCallCount;

        public int CreateKernelCallCount => Volatile.Read(ref _createKernelCallCount);

        public Kernel CreateKernel()
        {
            Interlocked.Increment(ref _createKernelCallCount);
            _createKernelCalled.TrySetResult();
            return Kernel.CreateBuilder().Build();
        }

        public Task WaitForCreateKernelAsync() => _createKernelCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
