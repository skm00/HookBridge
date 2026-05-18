using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services.DeadLetterAiAnalysis;
using HookBridge.Api.Controllers;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace HookBridge.Api.Tests;

public sealed class DeadLetterAiAnalysisControllerTests
{
    [Fact]
    public async Task GetByEventIdAsync_ReturnsOk_WhenAnalysisExists()
    {
        var controller = CreateController(new FakeRepository(CreateResult()), new FakeService());

        var result = await controller.GetByEventIdAsync("evt_1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<DeadLetterAiAnalysisResponseDto>>(ok.Value);
        Assert.Equal("evt_1", response.Data!.EventId);
    }

    [Fact]
    public async Task GetByDeadLetterIdAsync_ReturnsNotFound_WhenMissing()
    {
        var result = await CreateController(new FakeRepository(null), new FakeService()).GetByDeadLetterIdAsync("dlq_missing", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsBadRequest_ForInvalidInput()
    {
        var result = await CreateController(new FakeRepository(null), new FakeService()).AnalyzeAsync(new DeadLetterAiAnalysisRequestDto(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private static DeadLetterAiAnalysisController CreateController(IDeadLetterAiAnalysisRepository repository, IDeadLetterAiAnalysisService service)
    {
        var controller = new DeadLetterAiAnalysisController(repository, service, NullLogger<DeadLetterAiAnalysisController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static DeadLetterAiAnalysisResult CreateResult() => new()
    {
        DeadLetterId = "dlq_1",
        EventId = "evt_1",
        Summary = "summary",
        Recommendation = "recommendation",
        ReplaySafety = DeadLetterReplaySafety.ReplayWithCaution,
        SuggestedAction = DeadLetterSuggestedAction.ReplayWithBackoff,
        RiskLevel = "Medium",
        ConfidenceScore = 0.8,
        GeneratedAtUtc = DateTime.UtcNow,
        CreatedAtUtc = DateTime.UtcNow
    };

    private sealed class FakeService : IDeadLetterAiAnalysisService
    {
        public Task<DeadLetterAiAnalysisResponseDto> AnalyzeAsync(DeadLetterAiAnalysisRequestDto request, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.DeadLetterId)) throw new System.ComponentModel.DataAnnotations.ValidationException("DeadLetterId is required.");
            return Task.FromResult(CreateResult().ToResponseDto());
        }
    }

    private sealed class FakeRepository(DeadLetterAiAnalysisResult? result) : IDeadLetterAiAnalysisRepository
    {
        public Task InsertAsync(DeadLetterAiAnalysisResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<DeadLetterAiAnalysisResult?> GetByDeadLetterIdAsync(string deadLetterId, CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task<DeadLetterAiAnalysisResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task<IReadOnlyList<DeadLetterAiAnalysisResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DeadLetterAiAnalysisResult>>(Array.Empty<DeadLetterAiAnalysisResult>());
        public Task<IReadOnlyList<DeadLetterAiAnalysisResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DeadLetterAiAnalysisResult>>(Array.Empty<DeadLetterAiAnalysisResult>());
        public Task<IReadOnlyList<DeadLetterAiAnalysisResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DeadLetterAiAnalysisResult>>(Array.Empty<DeadLetterAiAnalysisResult>());
        public Task<IReadOnlyList<DeadLetterAiAnalysisResult>> SearchAsync(DeadLetterAiAnalysisSearchRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DeadLetterAiAnalysisResult>>(Array.Empty<DeadLetterAiAnalysisResult>());
    }
}
