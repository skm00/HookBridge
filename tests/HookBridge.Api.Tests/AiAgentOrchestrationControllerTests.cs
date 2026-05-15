using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using HookBridge.Api.Controllers;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace HookBridge.Api.Tests;

public sealed class AiAgentOrchestrationControllerTests
{
    [Fact]
    public async Task GetByEventIdAsync_ReturnsOk_WhenResultExists()
    {
        var controller = CreateController(new FakeRepository(CreateResult()));

        var result = await controller.GetByEventIdAsync("evt_12345", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<AiAgentOrchestrationResponseDto>>(ok.Value);
        Assert.True(response.Success);
        Assert.Equal("evt_12345", response.Data!.EventId);
        Assert.Equal(AiOrchestrationRecommendedAction.RetryWithBackoff, response.Data.RecommendedAction);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByEventIdAsync_ReturnsBadRequest_WhenEventIdIsEmpty(string eventId)
    {
        var controller = CreateController(new FakeRepository(null));

        var result = await controller.GetByEventIdAsync(eventId, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task GetByEventIdAsync_ReturnsNotFound_WhenNoResultExists()
    {
        var controller = CreateController(new FakeRepository(null));

        var result = await controller.GetByEventIdAsync("evt_missing", CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    private static AiAgentOrchestrationController CreateController(IAiAgentOrchestrationRepository repository)
    {
        var controller = new AiAgentOrchestrationController(repository, NullLogger<AiAgentOrchestrationController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static AiAgentOrchestrationResult CreateResult() => new()
    {
        EventId = "evt_12345",
        CorrelationId = "corr_789",
        OverallSummary = "Webhook delivery failed due to rate limiting.",
        OverallRiskLevel = AiRiskLevel.Medium.ToString(),
        RecommendedAction = AiOrchestrationRecommendedAction.RetryWithBackoff.ToString(),
        ConfidenceScore = 0.82,
        RequiresApproval = false,
        GeneratedAtUtc = DateTime.UtcNow,
        AgentResults =
        [
            new AiAgentResultDto
            {
                AgentName = AiAgentName.RetryRecommendationAgent,
                IsSuccessful = true,
                Summary = "HTTP 429 indicates rate limiting.",
                RiskLevel = AiRiskLevel.Medium,
                SuggestedAction = "RetryWithBackoff",
                ConfidenceScore = 0.86
            }
        ]
    };

    private sealed class FakeRepository(AiAgentOrchestrationResult? result) : IAiAgentOrchestrationRepository
    {
        public Task InsertAsync(AiAgentOrchestrationResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AiAgentOrchestrationResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task<IReadOnlyList<AiAgentOrchestrationResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAgentOrchestrationResult>>(Array.Empty<AiAgentOrchestrationResult>());
        public Task<IReadOnlyList<AiAgentOrchestrationResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAgentOrchestrationResult>>(Array.Empty<AiAgentOrchestrationResult>());
        public Task<IReadOnlyList<AiAgentOrchestrationResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAgentOrchestrationResult>>(Array.Empty<AiAgentOrchestrationResult>());
        public Task<IReadOnlyList<AiAgentOrchestrationResult>> SearchAsync(AiAgentOrchestrationSearchRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAgentOrchestrationResult>>(Array.Empty<AiAgentOrchestrationResult>());
    }
}
