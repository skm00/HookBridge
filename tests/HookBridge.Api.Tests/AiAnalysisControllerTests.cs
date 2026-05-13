using HookBridge.AI.Worker.Mongo;
using HookBridge.Api.Controllers;
using HookBridge.Api.Mappers;
using HookBridge.Application.DTOs.AiAnalysis;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HookBridge.Api.Tests;

public sealed class AiAnalysisControllerTests
{
    [Fact]
    public async Task GetByEventIdAsync_ReturnsOk_WhenAnalysisExists()
    {
        var repository = new FakeAiAnalysisResultRepository(CreateResult());
        var controller = CreateController(repository);

        var result = await controller.GetByEventIdAsync("evt_12345", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<AiAnalysisResultResponseDto>>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal("evt_12345", response.Data!.EventId);
        Assert.Equal("RetryWithBackoff", response.Data.SuggestedRetryAction);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByEventIdAsync_ReturnsBadRequest_WhenEventIdIsEmpty(string eventId)
    {
        var controller = CreateController(new FakeAiAnalysisResultRepository(null));

        var result = await controller.GetByEventIdAsync(eventId, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task GetByEventIdAsync_ReturnsNotFound_WhenNoResultExists()
    {
        var controller = CreateController(new FakeAiAnalysisResultRepository(null));

        var result = await controller.GetByEventIdAsync("evt_missing", CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task GetByEventIdAsync_ReturnsInternalServerError_WhenRepositoryThrows()
    {
        var controller = CreateController(new FakeAiAnalysisResultRepository(null, throwOnLookup: true));

        var result = await controller.GetByEventIdAsync("evt_error", CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, error.StatusCode);
    }

    [Fact]
    public void ToResponseDto_ConvertsAiAnalysisResult()
    {
        var source = CreateResult();

        var dto = AiAnalysisResultMapper.ToResponseDto(source);

        Assert.Equal(source.Id, dto.Id);
        Assert.Equal(source.EventId, dto.EventId);
        Assert.Equal(source.CorrelationId, dto.CorrelationId);
        Assert.Equal(source.Source, dto.Source);
        Assert.Equal(source.EventType, dto.EventType);
        Assert.Equal(source.FailureReason, dto.FailureReason);
        Assert.Equal(source.AiSummary, dto.AiSummary);
        Assert.Equal(source.RootCause, dto.RootCause);
        Assert.Equal(source.AiRecommendation, dto.AiRecommendation);
        Assert.Equal(source.RiskLevel, dto.RiskLevel);
        Assert.Equal(source.ConfidenceScore, dto.ConfidenceScore);
        Assert.Equal(source.SuggestedRetryAction, dto.SuggestedRetryAction);
        Assert.Equal(source.IsRetryRecommended, dto.IsRetryRecommended);
        Assert.Equal(source.Model, dto.Model);
        Assert.Equal(source.Provider, dto.Provider);
        Assert.Equal(DateTimeKind.Utc, dto.CreatedAtUtc.Kind);
    }

    [Fact]
    public async Task GetByEventIdAsync_LogsDoNotExposePayloadData()
    {
        const string sensitivePayload = "secret-card-number-4111111111111111";
        var logger = new CapturingLogger<AiAnalysisController>();
        var controller = CreateController(new FakeAiAnalysisResultRepository(CreateResult(aiSummary: sensitivePayload)), logger);

        await controller.GetByEventIdAsync("evt_12345", CancellationToken.None);

        Assert.DoesNotContain(logger.Records, record => record.Message.Contains(sensitivePayload, StringComparison.Ordinal));
    }

    private static AiAnalysisController CreateController(
        IAiAnalysisResultRepository repository,
        ILogger<AiAnalysisController>? logger = null)
    {
        var controller = new AiAnalysisController(repository, logger ?? new CapturingLogger<AiAnalysisController>());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static AiAnalysisResult CreateResult(string aiSummary = "The endpoint is rate limiting requests.") => new()
    {
        Id = "663f0c7a9f1e2a5a12345678",
        EventId = "evt_12345",
        CorrelationId = "corr_789",
        Source = "HookBridge.Worker",
        EventType = "WebhookDeliveryFailed",
        FailureReason = "Too Many Requests",
        AiSummary = aiSummary,
        RootCause = "HTTP 429 indicates rate limiting.",
        AiRecommendation = "Retry using exponential backoff.",
        RiskLevel = "Medium",
        ConfidenceScore = 0.86,
        SuggestedRetryAction = "RetryWithBackoff",
        IsRetryRecommended = true,
        Model = "llama3",
        Provider = "Ollama",
        CreatedAtUtc = new DateTime(2026, 5, 13, 10, 30, 0, DateTimeKind.Utc),
    };

    private sealed class FakeAiAnalysisResultRepository(AiAnalysisResult? result, bool throwOnLookup = false) : IAiAnalysisResultRepository
    {
        public Task InsertAsync(AiAnalysisResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AiAnalysisResult?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<AiAnalysisResult?>(null);

        public Task<AiAnalysisResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
        {
            if (throwOnLookup)
            {
                throw new InvalidOperationException("Repository failed.");
            }

            return Task.FromResult(result is not null && result.EventId == eventId ? result : null);
        }

        public Task<IReadOnlyList<AiAnalysisResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnalysisResult>>([]);
        public Task<IReadOnlyList<AiAnalysisResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnalysisResult>>([]);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Records { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Records.Add((logLevel, formatter(state, exception), exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
