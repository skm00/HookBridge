using FluentAssertions;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Services.SafeMode;
using HookBridge.Api.Controllers;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace HookBridge.Api.Tests;

public sealed class AiSafeModeControllerTests
{
    [Fact]
    public async Task Evaluate_Returns200()
    {
        var controller = new AiSafeModeController(new StubSafeModeGuard(), NullLogger<AiSafeModeController>.Instance);
        var result = await controller.EvaluateAsync(new AiSafeModeEvaluationRequestDto
        {
            ActionType = AiActionType.RetryWebhook,
            Environment = "production",
            RequestedAtUtc = DateTime.UtcNow,
            ConfidenceScore = 0.9
        }, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeAssignableTo<ApiResponse<AiSafeModeEvaluationResponseDto>>();
    }

    [Fact]
    public async Task Evaluate_Returns400ForInvalidRequest()
    {
        var controller = new AiSafeModeController(new StubSafeModeGuard(), NullLogger<AiSafeModeController>.Instance);
        var result = await controller.EvaluateAsync(new AiSafeModeEvaluationRequestDto
        {
            ActionType = AiActionType.Unknown,
            Environment = string.Empty,
            RequestedAtUtc = DateTime.UtcNow
        }, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    private sealed class StubSafeModeGuard : IAiSafeModeGuard
    {
        public Task<AiSafeModeEvaluationResponseDto> EvaluateAsync(AiSafeModeEvaluationRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new AiSafeModeEvaluationResponseDto
            {
                Decision = AiSafeModeDecision.RequiresApproval,
                IsAllowed = false,
                RequiresApproval = true,
                Reason = "Production retry actions require approved human approval.",
                BlockMessage = "AI recommendation is advisory only. Approve this action before applying it.",
                ActionType = request.ActionType,
                Environment = request.Environment,
                EvaluatedAtUtc = DateTime.UtcNow
            });
    }
}
