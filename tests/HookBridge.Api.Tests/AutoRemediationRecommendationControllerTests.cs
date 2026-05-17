using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using HookBridge.Api.Controllers;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace HookBridge.Api.Tests;

public sealed class AutoRemediationRecommendationControllerTests
{
    [Fact]
    public async Task GetByEventIdAsync_ReturnsOk_WhenRecommendationExists()
    {
        var controller = CreateController(new FakeRepository(CreateResult()));

        var result = await controller.GetByEventIdAsync("evt_123", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<AutoRemediationRecommendationResponseDto>>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal("evt_123", response.Data!.EventId);
        Assert.Equal(AutoRemediationRecommendedAction.RetryWithBackoff, response.Data.RecommendedAction);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByEventIdAsync_ReturnsBadRequest_WhenEventIdIsEmpty(string eventId)
    {
        var result = await CreateController(new FakeRepository(null)).GetByEventIdAsync(eventId, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task GetByEventIdAsync_ReturnsNotFound_WhenRecommendationDoesNotExist()
    {
        var result = await CreateController(new FakeRepository(null)).GetByEventIdAsync("evt_missing", CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task GetByEventIdAsync_ReturnsInternalServerError_WhenRepositoryThrows()
    {
        var result = await CreateController(new FakeRepository(null, throwOnLookup: true)).GetByEventIdAsync("evt_error", CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, error.StatusCode);
    }

    private static AutoRemediationRecommendationController CreateController(IAutoRemediationRecommendationRepository repository)
    {
        var controller = new AutoRemediationRecommendationController(repository, NullLogger<AutoRemediationRecommendationController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static AutoRemediationRecommendationResult CreateResult() => new()
    {
        EventId = "evt_123",
        CorrelationId = "corr_123",
        RemediationType = AutoRemediationType.RetryTuning,
        RecommendedAction = AutoRemediationRecommendedAction.RetryWithBackoff,
        RiskLevel = "Medium",
        ConfidenceScore = 0.82,
        Summary = "Rate limited.",
        Recommendation = "Retry with backoff.",
        ReasonCodes = [AutoRemediationReasonCode.RateLimited],
        GeneratedAtUtc = DateTime.UtcNow,
        CreatedAtUtc = DateTime.UtcNow
    };

    private sealed class FakeRepository(AutoRemediationRecommendationResult? result, bool throwOnLookup = false) : IAutoRemediationRecommendationRepository
    {
        public Task InsertAsync(AutoRemediationRecommendationResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AutoRemediationRecommendationResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
        {
            if (throwOnLookup)
            {
                throw new InvalidOperationException("Repository failed.");
            }

            return Task.FromResult(result);
        }
        public Task<IReadOnlyList<AutoRemediationRecommendationResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AutoRemediationRecommendationResult>>(Array.Empty<AutoRemediationRecommendationResult>());
        public Task<IReadOnlyList<AutoRemediationRecommendationResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AutoRemediationRecommendationResult>>(Array.Empty<AutoRemediationRecommendationResult>());
        public Task<IReadOnlyList<AutoRemediationRecommendationResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AutoRemediationRecommendationResult>>(Array.Empty<AutoRemediationRecommendationResult>());
        public Task<IReadOnlyList<AutoRemediationRecommendationResult>> SearchAsync(AutoRemediationRecommendationSearchRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AutoRemediationRecommendationResult>>(Array.Empty<AutoRemediationRecommendationResult>());
    }
}
