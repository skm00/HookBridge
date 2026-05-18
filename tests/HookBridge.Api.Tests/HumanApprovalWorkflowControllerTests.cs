using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.DTOs;
using HookBridge.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace HookBridge.Api.Tests;

public sealed class HumanApprovalWorkflowControllerTests
{
    private const string ValidApprovalId = "6650f3f7b8f6c2a5b4d6e7f8";

    [Fact]
    public async Task CreateAsync_Returns201()
    {
        var controller = CreateController(new FakeWorkflowService());

        var result = await controller.CreateAsync(CreateRequest(), CancellationToken.None);

        Assert.IsType<CreatedResult>(result.Result);
    }

    [Fact]
    public async Task GetByIdAsync_Returns200()
    {
        var controller = CreateController(new FakeWorkflowService());

        var result = await controller.GetByIdAsync(ValidApprovalId, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateAsync_Returns400_ForInvalidInput()
    {
        var controller = CreateController(new FakeWorkflowService { ThrowArgument = true });

        var result = await controller.CreateAsync(CreateRequest(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetByIdAsync_Returns404_WhenMissing()
    {
        var controller = CreateController(new FakeWorkflowService { ReturnNull = true });

        var result = await controller.GetByIdAsync(ValidApprovalId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task ApplyAsync_Returns409_ForInvalidTransition()
    {
        var controller = CreateController(new FakeWorkflowService { ThrowConflict = true });

        var result = await controller.ApplyAsync(ValidApprovalId, new HumanApprovalWorkflowApplyRequestDto { AppliedBy = "operator" }, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    private static HumanApprovalWorkflowController CreateController(IHumanApprovalWorkflowService service)
    {
        var controller = new HumanApprovalWorkflowController(service, NullLogger<HumanApprovalWorkflowController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        return controller;
    }

    private static HumanApprovalWorkflowCreateRequestDto CreateRequest() => new()
    {
        RecommendationId = "rec_1001",
        RecommendationType = AiRecommendationType.RetryRecommendation,
        RiskLevel = "High",
        RequestedBy = "tests",
        CreatedAtUtc = DateTime.UtcNow
    };

    private sealed class FakeWorkflowService : IHumanApprovalWorkflowService
    {
        public bool ReturnNull { get; set; }
        public bool ThrowArgument { get; set; }
        public bool ThrowConflict { get; set; }

        public Task<HumanApprovalWorkflowResponseDto> CreateAsync(HumanApprovalWorkflowCreateRequestDto request, CancellationToken cancellationToken = default)
        {
            if (ThrowArgument) throw new ArgumentException("Invalid input.");
            if (ThrowConflict) throw new AiRecommendationApprovalConflictException("Invalid transition.");
            return Task.FromResult(CreateResponse());
        }

        public Task<HumanApprovalWorkflowResponseDto?> GetByIdAsync(string approvalId, CancellationToken cancellationToken = default)
            => Task.FromResult(ReturnNull ? null : CreateResponse());

        public Task<IReadOnlyList<HumanApprovalWorkflowResponseDto>> GetPendingAsync(int limit = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HumanApprovalWorkflowResponseDto>>(new[] { CreateResponse() });

        public Task<IReadOnlyList<HumanApprovalWorkflowResponseDto>> SearchPendingAsync(AiRecommendationApprovalSearchRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HumanApprovalWorkflowResponseDto>>(new[] { CreateResponse() });

        public Task<HumanApprovalWorkflowResponseDto?> ReviewAsync(string approvalId, HumanApprovalWorkflowReviewRequestDto request, CancellationToken cancellationToken = default)
            => MutateAsync();

        public Task<HumanApprovalWorkflowResponseDto?> ApplyAsync(string approvalId, HumanApprovalWorkflowApplyRequestDto request, CancellationToken cancellationToken = default)
            => MutateAsync();

        public Task<HumanApprovalWorkflowResponseDto?> ExpireAsync(string approvalId, CancellationToken cancellationToken = default)
            => MutateAsync();

        private Task<HumanApprovalWorkflowResponseDto?> MutateAsync()
        {
            if (ThrowArgument) throw new ArgumentException("Invalid input.");
            if (ThrowConflict) throw new AiRecommendationApprovalConflictException("Invalid transition.");
            return Task.FromResult<HumanApprovalWorkflowResponseDto?>(ReturnNull ? null : CreateResponse());
        }

        private static HumanApprovalWorkflowResponseDto CreateResponse() => new()
        {
            ApprovalId = ValidApprovalId,
            RecommendationId = "rec_1001",
            RecommendationType = AiRecommendationType.RetryRecommendation,
            ApprovalStatus = AiRecommendationApprovalStatus.PendingReview,
            RiskLevel = "High",
            RequiresApproval = true,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
