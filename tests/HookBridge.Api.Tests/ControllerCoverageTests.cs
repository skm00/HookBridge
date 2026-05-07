using FluentValidation;
using FluentValidation.Results;
using HookBridge.Api.Controllers;
using HookBridge.Application.DTOs.AuditLogs;
using HookBridge.Application.DTOs.Common;
using HookBridge.Application.DTOs.EndpointValidation;
using HookBridge.Application.DTOs.Notifications;
using HookBridge.Application.DTOs.System;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace HookBridge.Api.Tests;

public sealed class ControllerCoverageTests
{
    [Fact]
    public async Task ValidateAsync_WhenPayloadIsInvalid_ShouldReturnValidationProblem()
    {
        var validator = new StubValidator<EndpointValidationRequestDto>(new ValidationResult([
            new ValidationFailure(nameof(EndpointValidationRequestDto.TargetUrl), "TargetUrl is invalid."),
        ]));
        var service = new StubEndpointValidationService();
        var currentUser = TestMockFactory.AuthenticatedUser("tenant-1");
        var controller = WithHttpContext(new EndpointValidationController(
            service,
            validator,
            currentUser,
            TenantIsolationTestHelpers.CreateValidator(currentUser)));

        var result = await controller.ValidateAsync(new EndpointValidationRequestDto { TargetUrl = "not-a-url" }, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result.Result);
        var details = Assert.IsAssignableFrom<ValidationProblemDetails>(problem.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, details.Status);
        Assert.Contains(nameof(EndpointValidationRequestDto.TargetUrl), details.Errors.Keys);
        Assert.Equal(0, service.ValidateCallCount);
    }

    [Fact]
    public async Task ValidateAsync_WhenPayloadIsValid_ShouldReturnEndpointValidationResult()
    {
        var validator = new StubValidator<EndpointValidationRequestDto>(new ValidationResult());
        var service = new StubEndpointValidationService
        {
            Response = new EndpointValidationResponseDto
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Endpoint is reachable.",
            },
        };
        var currentUser = TestMockFactory.AuthenticatedUser("tenant-1");
        var controller = WithHttpContext(new EndpointValidationController(
            service,
            validator,
            currentUser,
            TenantIsolationTestHelpers.CreateValidator(currentUser)));

        var result = await controller.ValidateAsync(new EndpointValidationRequestDto { TargetUrl = "https://hooks.example.com/orders" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<ApiResponse<EndpointValidationResponseDto>>(ok.Value);
        Assert.True(payload.Success);
        Assert.True(payload.Data!.IsSuccess);
        Assert.Equal(1, service.ValidateCallCount);
    }

    [Fact]
    public async Task GetProductionReadinessAsync_WhenServiceReturnsReady_ShouldReturnOkResponse()
    {
        var service = new StubProductionReadinessService
        {
            Response = new ProductionReadinessResponseDto
            {
                IsReady = true,
                Checks = [new ProductionReadinessItemDto { Name = "MongoDB", IsReady = true, Message = "OK" }],
            },
        };
        var controller = WithHttpContext(new ProductionController(service, NullLogger<ProductionController>.Instance));

        var result = await controller.GetProductionReadinessAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<ApiResponse<ProductionReadinessResponseDto>>(ok.Value);
        Assert.True(payload.Data!.IsReady);
        Assert.Equal("MongoDB", Assert.Single(payload.Data.Checks).Name);
    }

    [Fact]
    public async Task SearchAsync_WhenNotificationsRequested_ShouldScopeQueryToCurrentTenant()
    {
        var service = new StubNotificationService
        {
            SearchResponse = PagedResponseDto<NotificationResponseDto>.Create([
                new NotificationResponseDto { Id = "notification-1", TenantId = "tenant-1", Title = "Retry exhausted" },
            ], 1, 50, 1),
        };
        var currentUser = TestMockFactory.AuthenticatedUser("tenant-1");
        var controller = WithHttpContext(new NotificationsController(
            service,
            currentUser,
            TenantIsolationTestHelpers.CreateValidator(currentUser)));

        var result = await controller.SearchAsync("webhook", "warning", false, null, null, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<ApiResponse<PagedResponseDto<NotificationResponseDto>>>(ok.Value);
        Assert.Equal("tenant-1", service.LastSearchRequest!.TenantId);
        Assert.Equal("notification-1", Assert.Single(payload.Data!.Items).Id);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenNotificationIsMissing_ShouldReturnNotFound()
    {
        var currentUser = TestMockFactory.AuthenticatedUser("tenant-1");
        var controller = WithHttpContext(new NotificationsController(
            new StubNotificationService(),
            currentUser,
            TenantIsolationTestHelpers.CreateValidator(currentUser)));

        var result = await controller.MarkAsReadAsync("missing", CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var payload = Assert.IsAssignableFrom<ApiErrorResponse>(notFound.Value);
        Assert.False(payload.Success);
        Assert.Equal(StatusCodes.Status404NotFound, payload.StatusCode);
    }

    [Fact]
    public async Task GetUnreadCountAsync_WhenTenantIsAuthenticated_ShouldReturnUnreadCount()
    {
        var service = new StubNotificationService { UnreadCount = 7 };
        var currentUser = TestMockFactory.AuthenticatedUser("tenant-1");
        var controller = WithHttpContext(new NotificationsController(
            service,
            currentUser,
            TenantIsolationTestHelpers.CreateValidator(currentUser)));

        var result = await controller.GetUnreadCountAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<ApiResponse<object>>(ok.Value);
        Assert.Equal("tenant-1", service.LastUnreadTenantId);
        Assert.Contains("7", payload.Data!.ToString());
    }

    [Fact]
    public async Task GetByIdAsync_WhenAuditLogIsMissing_ShouldReturnNotFound()
    {
        var currentUser = TestMockFactory.AuthenticatedUser("tenant-1");
        var controller = new AuditLogsController(
            new StubAuditLogService(),
            currentUser,
            TenantIsolationTestHelpers.CreateValidator(currentUser));

        var result = await controller.GetByIdAsync("missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task SearchAsync_WhenAuditLogsRequested_ShouldScopeQueryToCurrentTenant()
    {
        var service = new StubAuditLogService
        {
            SearchResponse = PagedResponseDto<AuditLogResponseDto>.Create([
                new AuditLogResponseDto { Id = "log-1", TenantId = "tenant-1", Action = "Subscription.Created", ResourceType = "Subscription" },
            ], 1, 50, 1),
        };
        var currentUser = TestMockFactory.AuthenticatedUser("tenant-1");
        var controller = new AuditLogsController(
            service,
            currentUser,
            TenantIsolationTestHelpers.CreateValidator(currentUser));

        var result = await controller.SearchAsync(null, null, "Subscription.Created", null, null, null, null, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<PagedResponseDto<AuditLogResponseDto>>(ok.Value);
        Assert.Equal("tenant-1", service.LastSearchRequest!.TenantId);
        Assert.Equal("log-1", Assert.Single(payload.Items).Id);
    }

    private static T WithHttpContext<T>(T controller) where T : ControllerBase
        => new ControllerTestFixture().AttachHttpContext(controller);

    private sealed class StubValidator<T>(ValidationResult result) : AbstractValidator<T>
    {
        public override Task<ValidationResult> ValidateAsync(ValidationContext<T> context, CancellationToken cancellation = default)
            => Task.FromResult(result);
    }

    private sealed class StubEndpointValidationService : IEndpointValidationService
    {
        public int ValidateCallCount { get; private set; }
        public EndpointValidationResponseDto Response { get; init; } = new() { IsSuccess = false, Message = "not configured" };

        public Task<EndpointValidationResponseDto> ValidateAsync(EndpointValidationRequestDto request, CancellationToken cancellationToken = default)
        {
            ValidateCallCount++;
            return Task.FromResult(Response);
        }
    }

    private sealed class StubProductionReadinessService : IProductionReadinessService
    {
        public ProductionReadinessResponseDto Response { get; init; } = new();
        public Task<ProductionReadinessResponseDto> CheckAsync(CancellationToken cancellationToken = default) => Task.FromResult(Response);
    }

    private sealed class StubNotificationService : INotificationService
    {
        public NotificationSearchRequestDto? LastSearchRequest { get; private set; }
        public string? LastUnreadTenantId { get; private set; }
        public int UnreadCount { get; init; }
        public PagedResponseDto<NotificationResponseDto> SearchResponse { get; init; } = PagedResponseDto<NotificationResponseDto>.Create([], 1, 50, 0);
        public NotificationResponseDto? Notification { get; init; }
        public bool MarkAsReadResult { get; init; }

        public Task CreateAsync(Notification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PagedResponseDto<NotificationResponseDto>> SearchAsync(NotificationSearchRequestDto request, CancellationToken cancellationToken = default)
        {
            LastSearchRequest = request;
            return Task.FromResult(SearchResponse);
        }
        public Task<NotificationResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(Notification);
        public Task<bool> MarkAsReadAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(MarkAsReadResult);
        public Task<int> GetUnreadCountAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            LastUnreadTenantId = tenantId;
            return Task.FromResult(UnreadCount);
        }
    }

    private sealed class StubAuditLogService : IAuditLogService
    {
        public AuditLogSearchRequestDto? LastSearchRequest { get; private set; }
        public PagedResponseDto<AuditLogResponseDto> SearchResponse { get; init; } = PagedResponseDto<AuditLogResponseDto>.Create([], 1, 50, 0);
        public AuditLogResponseDto? AuditLog { get; init; }

        public Task LogAsync(AuditLog auditLog, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PagedResponseDto<AuditLogResponseDto>> SearchAsync(AuditLogSearchRequestDto request, CancellationToken cancellationToken = default)
        {
            LastSearchRequest = request;
            return Task.FromResult(SearchResponse);
        }
        public Task<AuditLogResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(AuditLog);
    }
}
