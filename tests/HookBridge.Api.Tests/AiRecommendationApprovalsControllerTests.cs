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

        Assert.IsType<CreatedResult>(result.Result);
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

        var result = await controller.GetByIdAsync(ValidApprovalId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateStatusAsync_Returns409_ForInvalidTransition()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService { ThrowConflict = true });

        var result = await controller.UpdateStatusAsync(ValidApprovalId, new AiRecommendationApprovalUpdateRequestDto { ApprovalStatus = AiRecommendationApprovalStatus.Applied }, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }


    [Fact]
    public async Task SearchAsync_Returns200()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService());

        var result = await controller.SearchAsync(new AiRecommendationApprovalSearchRequestDto(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task SearchAsync_Returns400_ForInvalidRequest()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService { ThrowArgument = true });

        var result = await controller.SearchAsync(new AiRecommendationApprovalSearchRequestDto(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetPendingAsync_Returns400_ForInvalidLimit()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService { ThrowArgument = true });

        var result = await controller.GetPendingAsync(0, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetByIdAsync_Returns200()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService());

        var result = await controller.GetByIdAsync(ValidApprovalId, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetByIdAsync_Returns400_ForBlankId()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService());

        var result = await controller.GetByIdAsync(" ", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateStatusAsync_Returns200()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService());

        var result = await controller.UpdateStatusAsync(ValidApprovalId, new AiRecommendationApprovalUpdateRequestDto { ApprovalStatus = AiRecommendationApprovalStatus.Approved }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateStatusAsync_Returns404_WhenMissing()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService { ReturnNull = true });

        var result = await controller.UpdateStatusAsync(ValidApprovalId, new AiRecommendationApprovalUpdateRequestDto { ApprovalStatus = AiRecommendationApprovalStatus.Approved }, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateStatusAsync_Returns400_ForBlankId()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService());

        var result = await controller.UpdateStatusAsync(" ", new AiRecommendationApprovalUpdateRequestDto { ApprovalStatus = AiRecommendationApprovalStatus.Approved }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateAsync_Returns500_ForUnexpectedError()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService { ThrowUnexpected = true });

        var result = await controller.CreateAsync(CreateRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }


    [Fact]
    public async Task CreateAsync_Returns409_ForDuplicateRecommendation()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService { ThrowConflict = true });

        var result = await controller.CreateAsync(CreateRequest(), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetByIdAsync_Returns400_ForMalformedObjectId()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService());

        var result = await controller.GetByIdAsync("not-an-object-id", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateStatusAsync_Returns400_ForMalformedObjectId()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService());

        var result = await controller.UpdateStatusAsync("not-an-object-id", new AiRecommendationApprovalUpdateRequestDto { ApprovalStatus = AiRecommendationApprovalStatus.Approved }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }


    [Fact]
    public async Task SearchAsync_Returns500_ForUnexpectedError()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService { ThrowUnexpected = true });

        var result = await controller.SearchAsync(new AiRecommendationApprovalSearchRequestDto(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetPendingAsync_Returns500_ForUnexpectedError()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService { ThrowUnexpected = true });

        var result = await controller.GetPendingAsync(100, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetByIdAsync_Returns500_ForUnexpectedError()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService { ThrowUnexpected = true });

        var result = await controller.GetByIdAsync(ValidApprovalId, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateStatusAsync_Returns500_ForUnexpectedError()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService { ThrowUnexpected = true });

        var result = await controller.UpdateStatusAsync(ValidApprovalId, new AiRecommendationApprovalUpdateRequestDto { ApprovalStatus = AiRecommendationApprovalStatus.Approved }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_Returns400_ForNullRequestBody()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService());

        var result = await controller.CreateAsync(null!, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateStatusAsync_Returns400_ForNullRequestBody()
    {
        var controller = CreateController(new FakeAiRecommendationApprovalService());

        var result = await controller.UpdateStatusAsync(ValidApprovalId, null!, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private const string ValidApprovalId = "507f1f77bcf86cd799439011";

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
        public bool ThrowUnexpected { get; init; }
        public bool ReturnNull { get; init; }

        public Task<AiRecommendationApprovalResponseDto> CreateAsync(AiRecommendationApprovalCreateRequestDto request, CancellationToken cancellationToken = default)
        {
            if (ThrowConflict) throw new AiRecommendationApprovalConflictException("Duplicate recommendation.");
            ThrowIfConfigured();
            return Task.FromResult(CreateResponse());
        }

        public Task<AiRecommendationApprovalResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            ThrowIfConfigured();
            return Task.FromResult(ReturnNull ? null : CreateResponse());
        }

        public Task<AiRecommendationApprovalResponseDto?> GetByRecommendationIdAsync(string recommendationId, CancellationToken cancellationToken = default)
        {
            ThrowIfConfigured();
            return Task.FromResult<AiRecommendationApprovalResponseDto?>(CreateResponse());
        }

        public Task<IReadOnlyList<AiRecommendationApprovalResponseDto>> GetPendingAsync(int limit = 100, CancellationToken cancellationToken = default)
        {
            ThrowIfConfigured();
            return Task.FromResult<IReadOnlyList<AiRecommendationApprovalResponseDto>>(new[] { CreateResponse() });
        }

        public Task<IReadOnlyList<AiRecommendationApprovalResponseDto>> SearchAsync(AiRecommendationApprovalSearchRequestDto request, CancellationToken cancellationToken = default)
        {
            ThrowIfConfigured();
            return Task.FromResult<IReadOnlyList<AiRecommendationApprovalResponseDto>>(new[] { CreateResponse() });
        }

        public Task<AiRecommendationApprovalResponseDto?> UpdateStatusAsync(string id, AiRecommendationApprovalUpdateRequestDto request, CancellationToken cancellationToken = default)
        {
            if (ThrowConflict) throw new AiRecommendationApprovalConflictException("Invalid transition.");
            ThrowIfConfigured();
            return Task.FromResult(ReturnNull ? null : CreateResponse(request.ApprovalStatus!.Value));
        }

        private void ThrowIfConfigured()
        {
            if (ThrowArgument) throw new ArgumentException("Invalid request.");
            if (ThrowUnexpected) throw new InvalidOperationException("Unexpected failure.");
        }

        private static AiRecommendationApprovalResponseDto CreateResponse(AiRecommendationApprovalStatus status = AiRecommendationApprovalStatus.PendingReview) => new()
        {
            Id = AiRecommendationApprovalsControllerTests.ValidApprovalId,
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
