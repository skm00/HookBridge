using FluentAssertions;
using HookBridge.Api.Controllers;
using HookBridge.Api.Security;
using HookBridge.Application.DTOs.Common;
using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Enums;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HookBridge.Api.Tests;

public sealed class SubscriptionsControllerLifecycleTests
{
    [Fact]
    public async Task CreateAsync_WhenSubscriptionRequestIsValid_ShouldReturnCreatedResponse()
    {
        var service = new Mock<ISubscriptionService>();
        var request = BuildCreateRequest();
        service.Setup(x => x.CreateAsync("tenant-1", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildResponse("sub-1", request.EventType!, request.TargetUrl));
        var controller = BuildController(service.Object);

        var result = await controller.CreateAsync(request, CancellationToken.None);

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        var body = created.Value.Should().BeAssignableTo<ApiResponse<SubscriptionResponseDto>>().Subject;
        body.Data!.Id.Should().Be("sub-1");
        service.Verify(x => x.CreateAsync("tenant-1", request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WhenSubscriptionExists_ShouldReturnOkResponse()
    {
        var service = new Mock<ISubscriptionService>();
        service.Setup(x => x.GetByIdAsync("tenant-1", "sub-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildResponse("sub-1", "order.created", "https://webhooks.example.com/orders"));
        var controller = BuildController(service.Object);

        var result = await controller.GetByIdAsync("sub-1", CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeAssignableTo<ApiResponse<SubscriptionResponseDto>>().Subject;
        body.Data!.Id.Should().Be("sub-1");
        service.Verify(x => x.GetByIdAsync("tenant-1", "sub-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WhenSubscriptionDoesNotExist_ShouldReturnNotFound()
    {
        var service = new Mock<ISubscriptionService>();
        service.Setup(x => x.GetByIdAsync("tenant-1", "missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionResponseDto?)null);
        var controller = BuildController(service.Object);

        var result = await controller.GetByIdAsync("missing", CancellationToken.None);

        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task SearchAsync_ForwardsFiltersAndReturnsPagedResponse()
    {
        var service = new Mock<ISubscriptionService>();
        var paged = PagedResponseDto<SubscriptionResponseDto>.Create(
            [BuildResponse("sub-1", "order.created", "https://webhooks.example.com/orders")],
            pageNumber: 2,
            pageSize: 10,
            totalCount: 21);
        service.Setup(x => x.SearchAsync(It.IsAny<SubscriptionSearchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);
        var controller = BuildController(service.Object);

        var result = await controller.SearchAsync(
            eventType: "order.created",
            targetUrl: "https://webhooks.example.com/orders",
            isActive: true,
            pageNumber: 2,
            pageSize: 10,
            sortBy: "createdAt",
            sortDirection: "asc",
            cancellationToken: CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeAssignableTo<ApiResponse<PagedResponseDto<SubscriptionResponseDto>>>().Subject;
        body.Data!.Items.Should().ContainSingle(item => item.Id == "sub-1");
        body.Data.TotalCount.Should().Be(21);
        service.Verify(x => x.SearchAsync(
            It.Is<SubscriptionSearchRequestDto>(request =>
                request.TenantId == "tenant-1" &&
                request.EventType == "order.created" &&
                request.TargetUrl == "https://webhooks.example.com/orders" &&
                request.IsActive == true &&
                request.PageNumber == 2 &&
                request.PageSize == 10 &&
                request.SortBy == "createdAt" &&
                request.SortDirection == "asc"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenSubscriptionExists_ShouldReturnUpdatedResponse()
    {
        var service = new Mock<ISubscriptionService>();
        var request = new UpdateSubscriptionRequestDto { EventType = "order.updated", TargetUrl = "https://webhooks.example.com/updated" };
        service.Setup(x => x.UpdateAsync("tenant-1", "sub-1", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildResponse("sub-1", "order.updated", request.TargetUrl));
        var controller = BuildController(service.Object);

        var result = await controller.UpdateAsync("sub-1", request, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeAssignableTo<ApiResponse<SubscriptionResponseDto>>().Subject;
        body.Data!.EventType.Should().Be("order.updated");
        service.Verify(x => x.UpdateAsync("tenant-1", "sub-1", request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenSubscriptionDoesNotExist_ShouldReturnNotFound()
    {
        var service = new Mock<ISubscriptionService>();
        var request = new UpdateSubscriptionRequestDto { EventType = "order.updated" };
        service.Setup(x => x.UpdateAsync("tenant-1", "missing", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionResponseDto?)null);
        var controller = BuildController(service.Object);

        var result = await controller.UpdateAsync("missing", request, CancellationToken.None);

        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task DeleteAsync_WhenSubscriptionExists_ShouldReturnNoContent()
    {
        var service = new Mock<ISubscriptionService>();
        service.Setup(x => x.DeleteAsync("tenant-1", "sub-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var controller = BuildController(service.Object);

        var result = await controller.DeleteAsync("sub-1", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        service.Verify(x => x.DeleteAsync("tenant-1", "sub-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenSubscriptionDoesNotExist_ShouldReturnNotFound()
    {
        var service = new Mock<ISubscriptionService>();
        service.Setup(x => x.DeleteAsync("tenant-1", "missing", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var controller = BuildController(service.Object);

        var result = await controller.DeleteAsync("missing", CancellationToken.None);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task EnableAsync_WhenSubscriptionExists_ShouldReturnNoContent()
    {
        var service = new Mock<ISubscriptionService>();
        service.Setup(x => x.EnableAsync("tenant-1", "sub-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var controller = BuildController(service.Object);

        var result = await controller.EnableAsync("sub-1", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        service.Verify(x => x.EnableAsync("tenant-1", "sub-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnableAsync_WhenSubscriptionDoesNotExist_ShouldReturnNotFound()
    {
        var service = new Mock<ISubscriptionService>();
        service.Setup(x => x.EnableAsync("tenant-1", "missing", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var controller = BuildController(service.Object);

        var result = await controller.EnableAsync("missing", CancellationToken.None);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task DisableAsync_WhenSubscriptionExists_ShouldReturnNoContent()
    {
        var service = new Mock<ISubscriptionService>();
        service.Setup(x => x.DisableAsync("tenant-1", "sub-1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var controller = BuildController(service.Object);

        var result = await controller.DisableAsync("sub-1", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        service.Verify(x => x.DisableAsync("tenant-1", "sub-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableAsync_WhenSubscriptionDoesNotExist_ShouldReturnNotFound()
    {
        var service = new Mock<ISubscriptionService>();
        service.Setup(x => x.DisableAsync("tenant-1", "missing", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var controller = BuildController(service.Object);

        var result = await controller.DisableAsync("missing", CancellationToken.None);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    private static SubscriptionsController BuildController(ISubscriptionService service)
    {
        var currentUser = new FixedCurrentUserContext();
        var httpContextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        return new SubscriptionsController(
            service,
            currentUser,
            new TenantAccessValidator(currentUser, httpContextAccessor, NullLogger<TenantAccessValidator>.Instance))
        {
            ControllerContext = new ControllerContext { HttpContext = httpContextAccessor.HttpContext },
        };
    }

    private static CreateSubscriptionRequestDto BuildCreateRequest() => new()
    {
        EventType = "order.created",
        TargetUrl = "https://webhooks.example.com/orders",
        Headers = [new KeyValueDto { Name = "x-test", Value = "true" }],
        RetryPolicy = new RetryPolicyDto { MaxAttempts = 3, InitialDelaySeconds = 10, BackoffType = "Exponential" },
        TimeoutSeconds = 30,
    };

    private static SubscriptionResponseDto BuildResponse(string id, string eventType, string targetUrl) => new()
    {
        Id = id,
        EventType = eventType,
        TargetUrl = targetUrl,
        RetryPolicy = new RetryPolicyDto { MaxAttempts = 3, InitialDelaySeconds = 10, BackoffType = "Exponential" },
        TimeoutSeconds = 30,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
    };

    private sealed class FixedCurrentUserContext : ICurrentUserContext
    {
        public string? UserId => "user-1";
        public string? TenantId => "tenant-1";
        public string? Email => "owner@example.com";
        public string? Role => AdminRole.Owner.ToString();
        public bool IsAuthenticated => true;
    }
}
