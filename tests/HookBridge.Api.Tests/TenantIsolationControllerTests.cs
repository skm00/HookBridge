using HookBridge.Api.Controllers;
using HookBridge.Application.DTOs.ApiKeys;
using HookBridge.Application.DTOs.AuditLogs;
using HookBridge.Application.DTOs.DeliveryAttempts;
using HookBridge.Application.DTOs.FailedEvents;
using HookBridge.Application.DTOs.Events;
using HookBridge.Application.DTOs.Notifications;
using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.DTOs.Tenants;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HookBridge.Api.Tests;

public sealed class TenantIsolationControllerTests
{
    private static T WithHttpContext<T>(T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    [Fact]
    public async Task AdminUser_CanAccessOwnTenantApiKeys()
    {
        var service = new FakeApiKeyService();
        var controller = WithHttpContext(new ApiKeysController(
            service,
            TenantIsolationTestHelpers.CreateValidator()));

        var result = await controller.GetByTenantAsync("tenant-1", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("tenant-1", service.LastTenantId);
    }

    [Fact]
    public async Task AdminUser_CannotAccessAnotherTenantApiKeys()
    {
        var controller = WithHttpContext(new ApiKeysController(
            new FakeApiKeyService(),
            TenantIsolationTestHelpers.CreateValidator()));

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.GetByTenantAsync("tenant-2", CancellationToken.None));
    }

    [Fact]
    public async Task SubscriptionSearch_IsForcedToCurrentTenant()
    {
        var service = new FakeSubscriptionService();
        var controller = WithHttpContext(new SubscriptionsController(
            service,
            new TenantIsolationTestHelpers.FakeCurrentUserContext { TenantId = "tenant-1" },
            TenantIsolationTestHelpers.CreateValidator()));

        var result = await controller.SearchAsync(null, null, true, 1, 50, null, "desc", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("tenant-1", service.LastSearchRequest?.TenantId);
    }

    [Fact]
    public async Task SubscriptionGetById_ForAnotherTenant_IsBlocked()
    {
        var service = new FakeSubscriptionService
        {
            GetByIdResult = new SubscriptionResponseDto { Id = "sub-1", TenantId = "tenant-2" },
        };
        var controller = WithHttpContext(new SubscriptionsController(
            service,
            new TenantIsolationTestHelpers.FakeCurrentUserContext { TenantId = "tenant-1" },
            TenantIsolationTestHelpers.CreateValidator()));

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.GetByIdAsync("sub-1", CancellationToken.None));
    }

    [Fact]
    public async Task DeliveryLogsSearch_IsForcedToCurrentTenant()
    {
        var service = new FakeDeliveryAttemptService();
        var controller = WithHttpContext(new DeliveryLogsController(
            service,
            new TenantIsolationTestHelpers.FakeCurrentUserContext { TenantId = "tenant-1" },
            TenantIsolationTestHelpers.CreateValidator(),
            NullLogger<DeliveryLogsController>.Instance));

        var result = await controller.SearchAsync("tenant-2", null, null, null, null, null, null, null, null, 1, 50, null, "desc", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("tenant-1", service.LastSearchRequest?.TenantId);
    }

    [Fact]
    public async Task FailedEventRetry_ForAnotherTenant_IsBlocked()
    {
        var service = new FakeFailedEventService
        {
            Item = new FailedEventResponseDto { Id = "failed-1", TenantId = "tenant-2", Status = "DLQ" },
        };
        var controller = WithHttpContext(new FailedEventsController(
            service,
            new TenantIsolationTestHelpers.FakeCurrentUserContext { TenantId = "tenant-1" },
            TenantIsolationTestHelpers.CreateValidator(),
            NullLogger<FailedEventsController>.Instance));

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.RetryAsync("failed-1", CancellationToken.None));
    }



    [Fact]
    public async Task NotificationGetById_ForAnotherTenant_IsBlocked()
    {
        var service = new FakeNotificationService
        {
            Item = new NotificationResponseDto { Id = "notif-1", TenantId = "tenant-2" },
        };

        var controller = WithHttpContext(new NotificationsController(
            service,
            new TenantIsolationTestHelpers.FakeCurrentUserContext { TenantId = "tenant-1" },
            TenantIsolationTestHelpers.CreateValidator()));

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.GetByIdAsync("notif-1", CancellationToken.None));
    }

    [Fact]
    public async Task IncomingEventGetById_ForAnotherTenant_IsBlocked()
    {
        var service = new FakeIncomingEventQueryService
        {
            Item = new IncomingEventResponseDto { Id = "incoming-1", TenantId = "tenant-2", EventId = "evt-1", EventType = "order.created", Status = "Accepted", ReceivedAt = DateTime.UtcNow, Payload = new { } },
        };

        var controller = WithHttpContext(new IncomingEventsController(
            service,
            new TenantIsolationTestHelpers.FakeCurrentUserContext { TenantId = "tenant-1" },
            TenantIsolationTestHelpers.CreateValidator(),
            NullLogger<IncomingEventsController>.Instance));

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.GetByIdAsync("incoming-1", CancellationToken.None));
    }

    [Fact]
    public async Task TenantUpdate_ForAnotherTenant_IsBlocked()
    {
        var controller = WithHttpContext(new TenantsController(
            new FakeTenantService(),
            new TenantIsolationTestHelpers.FakeCurrentUserContext { TenantId = "tenant-1" },
            TenantIsolationTestHelpers.CreateValidator()));

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.UpdateAsync("tenant-2", new UpdateTenantRequestDto(), CancellationToken.None));
    }

    [Fact]
    public async Task CrossTenantRequest_ReturnsForbiddenException()
    {
        var controller = WithHttpContext(new BillingController(
            new FakeBillingService(),
            TenantIsolationTestHelpers.CreateValidator()));

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.GetStatusAsync("tenant-2", CancellationToken.None));
    }

    [Fact]
    public async Task MissingTenantClaim_ReturnsUnauthorizedException()
    {
        var controller = WithHttpContext(new ApiKeysController(
            new FakeApiKeyService(),
            TenantIsolationTestHelpers.CreateValidator(
                new TenantIsolationTestHelpers.FakeCurrentUserContext { TenantId = null, IsAuthenticated = true })));

        await Assert.ThrowsAsync<UnauthorizedException>(() => controller.GetByTenantAsync("tenant-1", CancellationToken.None));
    }

    [Fact]
    public async Task AuditLogsSearch_IsTenantScoped()
    {
        var service = new FakeAuditLogService();
        var controller = WithHttpContext(new AuditLogsController(
            service,
            new TenantIsolationTestHelpers.FakeCurrentUserContext { TenantId = "tenant-1" },
            TenantIsolationTestHelpers.CreateValidator()));

        var result = await controller.SearchAsync(null, null, null, null, null, null, null, 1, 50, null, "desc", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("tenant-1", service.LastSearchRequest?.TenantId);
    }

    [Fact]
    public async Task AuditLogGetById_ForAnotherTenant_IsBlocked()
    {
        var service = new FakeAuditLogService
        {
            GetByIdResult = new AuditLogResponseDto { Id = "audit-1", TenantId = "tenant-2" },
        };
        var controller = WithHttpContext(new AuditLogsController(
            service,
            new TenantIsolationTestHelpers.FakeCurrentUserContext { TenantId = "tenant-1" },
            TenantIsolationTestHelpers.CreateValidator()));

        await Assert.ThrowsAsync<ForbiddenException>(() => controller.GetByIdAsync("audit-1", CancellationToken.None));
    }

    private sealed class FakeApiKeyService : IApiKeyService
    {
        public string? LastTenantId { get; private set; }

        public Task<CreateApiKeyResponseDto> CreateAsync(string tenantId, CreateApiKeyRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new CreateApiKeyResponseDto());

        public Task<IReadOnlyList<ApiKeyResponseDto>> GetByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            LastTenantId = tenantId;
            return Task.FromResult<IReadOnlyList<ApiKeyResponseDto>>([]);
        }

        public Task<ApiKeyResponseDto?> UpdateAsync(string tenantId, string keyId, UpdateApiKeyRequestDto request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<bool> RevokeAsync(string tenantId, string keyId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<ApiKeyValidationResult> ValidateAsync(string tenantId, string plainApiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new ApiKeyValidationResult { IsValid = false });
    }

    private sealed class FakeSubscriptionService : ISubscriptionService
    {
        public SubscriptionSearchRequestDto? LastSearchRequest { get; private set; }

        public SubscriptionResponseDto? GetByIdResult { get; set; } = new() { Id = "sub-1", TenantId = "tenant-1" };

        public Task<SubscriptionResponseDto> CreateAsync(string tenantId, CreateSubscriptionRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new SubscriptionResponseDto { Id = "sub-1" });

        public Task<SubscriptionResponseDto?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default)
            => Task.FromResult(GetByIdResult);

        public Task<HookBridge.Application.DTOs.Common.PagedResponseDto<SubscriptionResponseDto>> SearchAsync(SubscriptionSearchRequestDto request, CancellationToken cancellationToken = default)
        {
            LastSearchRequest = request;
            return Task.FromResult(HookBridge.Application.DTOs.Common.PagedResponseDto<SubscriptionResponseDto>.Create([], 1, 50, 0));
        }

        public Task<SubscriptionResponseDto?> UpdateAsync(string tenantId, string id, UpdateSubscriptionRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(GetByIdResult);

        public Task<bool> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> EnableAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> DisableAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeDeliveryAttemptService : IDeliveryAttemptService
    {
        public DeliveryAttemptSearchRequestDto? LastSearchRequest { get; private set; }

        public Task<HookBridge.Application.DTOs.Common.PagedResponseDto<DeliveryAttemptResponseDto>> SearchAsync(DeliveryAttemptSearchRequestDto request, CancellationToken cancellationToken = default)
        {
            LastSearchRequest = request;
            return Task.FromResult(HookBridge.Application.DTOs.Common.PagedResponseDto<DeliveryAttemptResponseDto>.Create([], 1, 50, 0));
        }

        public Task<DeliveryAttemptResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult<DeliveryAttemptResponseDto?>(null);
    }

    private sealed class FakeFailedEventService : IFailedEventService
    {
        public FailedEventResponseDto? Item { get; set; }

        public Task CreateAsync(HookBridge.Domain.Entities.FailedEvent failedEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<HookBridge.Application.DTOs.Common.PagedResponseDto<FailedEventResponseDto>> SearchAsync(FailedEventSearchRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(HookBridge.Application.DTOs.Common.PagedResponseDto<FailedEventResponseDto>.Create([], 1, 50, 0));

        public Task<FailedEventResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(Item);

        public Task<bool> RetryAsync(string failedEventId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }



    private sealed class FakeNotificationService : INotificationService
    {
        public NotificationResponseDto? Item { get; set; }
        public NotificationSearchRequestDto? LastSearchRequest { get; private set; }

        public Task CreateAsync(HookBridge.Domain.Entities.Notification notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<HookBridge.Application.DTOs.Common.PagedResponseDto<NotificationResponseDto>> SearchAsync(NotificationSearchRequestDto request, CancellationToken cancellationToken = default)
        {
            LastSearchRequest = request;
            return Task.FromResult(HookBridge.Application.DTOs.Common.PagedResponseDto<NotificationResponseDto>.Create([], 1, 50, 0));
        }

        public Task<NotificationResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(Item);

        public Task<bool> MarkAsReadAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<int> GetUnreadCountAsync(string tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class FakeIncomingEventQueryService : IIncomingEventQueryService
    {
        public IncomingEventResponseDto? Item { get; set; }

        public Task<HookBridge.Application.DTOs.Common.PagedResponseDto<IncomingEventResponseDto>> SearchAsync(IncomingEventSearchRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(HookBridge.Application.DTOs.Common.PagedResponseDto<IncomingEventResponseDto>.Create([], 1, 50, 0));

        public Task<IncomingEventResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(Item);
    }

    private sealed class FakeTenantService : ITenantService
    {
        public Task<TenantResponseDto> CreateAsync(CreateTenantRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(new TenantResponseDto());

        public Task<TenantResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult<TenantResponseDto?>(null);

        public Task<IReadOnlyList<TenantResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TenantResponseDto>>([]);

        public Task<TenantResponseDto?> UpdateAsync(string id, UpdateTenantRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult<TenantResponseDto?>(null);

        public Task<bool> DisableAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FakeBillingService : IBillingService
    {
        public Task<HookBridge.Application.DTOs.Billing.CheckoutSessionResponseDto> CreateCheckoutSessionAsync(string tenantId, HookBridge.Application.DTOs.Billing.CreateCheckoutSessionRequestDto request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<HookBridge.Application.DTOs.Billing.BillingStatusResponseDto?> GetBillingStatusAsync(string tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<HookBridge.Application.DTOs.Billing.BillingStatusResponseDto?>(null);

        public Task HandleStripeWebhookAsync(string jsonPayload, string stripeSignature, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeAuditLogService : IAuditLogService
    {
        public AuditLogSearchRequestDto? LastSearchRequest { get; private set; }

        public AuditLogResponseDto? GetByIdResult { get; set; } = new() { Id = "audit-1", TenantId = "tenant-1" };

        public Task LogAsync(HookBridge.Domain.Entities.AuditLog auditLog, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<HookBridge.Application.DTOs.Common.PagedResponseDto<AuditLogResponseDto>> SearchAsync(AuditLogSearchRequestDto request, CancellationToken cancellationToken = default)
        {
            LastSearchRequest = request;
            return Task.FromResult(HookBridge.Application.DTOs.Common.PagedResponseDto<AuditLogResponseDto>.Create([], 1, 50, 0));
        }

        public Task<AuditLogResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(GetByIdResult);
    }
}
