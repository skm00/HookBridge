using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.DTOs;
using HookBridge.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace HookBridge.Api.Tests;

public sealed class AiRecommendationApprovalsControllerTests
{
    [Fact]
    public async Task GetPendingAsync_Returns200()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService());

        var result = await controller.GetPendingAsync();

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateAsync_Returns201()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService());

        var result = await controller.CreateAsync(CreateRequest(), CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result.Result);
    }

    [Fact]
    public async Task CreateAsync_Returns400_ForInvalidRequest()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService { ThrowArgument = true });

        var result = await controller.CreateAsync(CreateRequest(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetByIdAsync_Returns404_WhenMissing()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService { ReturnNull = true });

        var result = await controller.GetByIdAsync("missing", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateStatusAsync_Returns409_ForInvalidTransition()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService { ThrowConflict = true });

        var result = await controller.UpdateStatusAsync("approval_1", new AiRecommendationApprovalUpdateRequestDto { ApprovalStatus = AiRecommendationApprovalStatus.Applied }, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    private static AiRecommendationApprovalsController CreateController(IAiRecommendationApprovalService service)
    {
        var controller = new AiRecommendationApprovalsController(service, NullLogger<AiRecommendationApprovalsController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static AiRecommendationApprovalCreateRequestDto CreateRequest() => new()
    {
        RecommendationId = "rec_1",
        RecommendationType = AiRecommendationType.RetryRecommendation,
        RiskLevel = "High",
        Summary = "Summary",
        Recommendation = "Recommendation"
    };

    private sealed class FakeAiRecommendationApprovalService : IAiRecommendationApprovalService
    {
        public bool ThrowArgument { get; init; }
        public bool ThrowConflict { get; init; }
        public bool ReturnNull { get; init; }

        public Task<AiRecommendationApprovalResponseDto> CreateAsync(AiRecommendationApprovalCreateRequestDto request, CancellationToken cancellationToken = default)
        {
            if (ThrowArgument) throw new ArgumentException("Invalid request.");
            return Task.FromResult(CreateResponse());
        }

        public Task<AiRecommendationApprovalResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(ReturnNull ? null : CreateResponse());

        public Task<AiRecommendationApprovalResponseDto?> GetByRecommendationIdAsync(string recommendationId, CancellationToken cancellationToken = default)
            => Task.FromResult<AiRecommendationApprovalResponseDto?>(CreateResponse());

        public Task<IReadOnlyList<AiRecommendationApprovalResponseDto>> GetPendingAsync(int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AiRecommendationApprovalResponseDto>>(new[] { CreateResponse() });

        public Task<IReadOnlyList<AiRecommendationApprovalResponseDto>> SearchAsync(AiRecommendationApprovalSearchRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AiRecommendationApprovalResponseDto>>(new[] { CreateResponse() });

        public Task<AiRecommendationApprovalResponseDto?> UpdateStatusAsync(string id, AiRecommendationApprovalUpdateRequestDto request, CancellationToken cancellationToken = default)
        {
            if (ThrowConflict) throw new AiRecommendationApprovalConflictException("Invalid transition.");
            return Task.FromResult(ReturnNull ? null : CreateResponse(request.ApprovalStatus!.Value));
        }

        private static AiRecommendationApprovalResponseDto CreateResponse(AiRecommendationApprovalStatus status = AiRecommendationApprovalStatus.PendingReview) => new()
        {
            Id = "approval_1",
            RecommendationId = "rec_1",
            RecommendationType = AiRecommendationType.RetryRecommendation,
            ApprovalStatus = status,
            RiskLevel = "High",
            Summary = "Summary",
            Recommendation = "Recommendation",
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
