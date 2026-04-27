using HookBridge.Api.Controllers;
using HookBridge.Application.DTOs.FailedEvents;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HookBridge.Api.Tests;

public sealed class FailedEventsControllerTests
{
    [Fact]
    public async Task SearchAsync_ReturnsOk()
    {
        var service = new FakeFailedEventService();
        var controller = new FailedEventsController(service, new TenantIsolationTestHelpers.FakeCurrentUserContext(), TenantIsolationTestHelpers.CreateValidator(), NullLogger<FailedEventsController>.Instance);

        var result = await controller.SearchAsync("tenant-1", null, null, null, "DLQ", null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<FailedEventResponseDto>>(ok.Value);
        Assert.Single(payload);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsOk_WhenFound()
    {
        var service = new FakeFailedEventService();
        var controller = new FailedEventsController(service, new TenantIsolationTestHelpers.FakeCurrentUserContext(), TenantIsolationTestHelpers.CreateValidator(), NullLogger<FailedEventsController>.Instance);

        var result = await controller.GetByIdAsync("failed-1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<FailedEventResponseDto>(ok.Value);
        Assert.Equal("failed-1", payload.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNotFound_WhenMissing()
    {
        var service = new FakeFailedEventService();
        var controller = new FailedEventsController(service, new TenantIsolationTestHelpers.FakeCurrentUserContext(), TenantIsolationTestHelpers.CreateValidator(), NullLogger<FailedEventsController>.Instance);

        var result = await controller.GetByIdAsync("missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task RetryAsync_ReturnsAccepted_WhenRetryRequested()
    {
        var service = new FakeFailedEventService();
        var controller = new FailedEventsController(service, new TenantIsolationTestHelpers.FakeCurrentUserContext(), TenantIsolationTestHelpers.CreateValidator(), NullLogger<FailedEventsController>.Instance);

        var result = await controller.RetryAsync("failed-1", CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task RetryAsync_ReturnsNotFound_WhenMissing()
    {
        var service = new FakeFailedEventService();
        var controller = new FailedEventsController(service, new TenantIsolationTestHelpers.FakeCurrentUserContext(), TenantIsolationTestHelpers.CreateValidator(), NullLogger<FailedEventsController>.Instance);

        var result = await controller.RetryAsync("missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RetryAsync_ReturnsBadRequest_WhenNotRetryable()
    {
        var service = new FakeFailedEventService();
        service.SetStatus("failed-1", "RetryRequested");
        var controller = new FailedEventsController(service, new TenantIsolationTestHelpers.FakeCurrentUserContext(), TenantIsolationTestHelpers.CreateValidator(), NullLogger<FailedEventsController>.Instance);

        var result = await controller.RetryAsync("failed-1", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private sealed class FakeFailedEventService : IFailedEventService
    {
        private readonly List<FailedEventResponseDto> _items =
        [
            new FailedEventResponseDto
            {
                Id = "failed-1",
                TenantId = "tenant-1",
                EventId = "evt-1",
                SubscriptionId = "sub-1",
                EventType = "order.created",
                TargetUrl = "https://example.com/orders",
                Reason = "Retry attempts exhausted",
                FinalAttemptNumber = 3,
                Status = "DLQ",
                FailedAt = new DateTime(2026, 4, 27, 11, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 4, 27, 11, 0, 0, DateTimeKind.Utc),
            },
        ];

        public Task CreateAsync(HookBridge.Domain.Entities.FailedEvent failedEvent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<FailedEventResponseDto>> SearchAsync(FailedEventSearchRequestDto request, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<FailedEventResponseDto> result = _items
                .Where(x => string.IsNullOrWhiteSpace(request.TenantId) || x.TenantId == request.TenantId)
                .Where(x => string.IsNullOrWhiteSpace(request.Status) || x.Status == request.Status)
                .ToList();

            return Task.FromResult(result);
        }

        public Task<FailedEventResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        }

        public Task<bool> RetryAsync(string failedEventId, CancellationToken cancellationToken = default)
        {
            var item = _items.FirstOrDefault(x => x.Id == failedEventId);
            if (item is null || !string.Equals(item.Status, "DLQ", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(false);
            }

            item.Status = "RetryRequested";
            return Task.FromResult(true);
        }

        public void SetStatus(string id, string status)
        {
            var item = _items.First(x => x.Id == id);
            item.Status = status;
        }
    }
}
