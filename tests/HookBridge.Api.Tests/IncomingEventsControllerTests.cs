using HookBridge.Api.Controllers;
using HookBridge.Application.DTOs.Events;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HookBridge.Api.Tests;

public sealed class IncomingEventsControllerTests
{
    [Fact]
    public async Task SearchAsync_ReturnsOk()
    {
        var service = new FakeIncomingEventQueryService();
        var controller = new IncomingEventsController(service, new TenantIsolationTestHelpers.FakeCurrentUserContext(), TenantIsolationTestHelpers.CreateValidator(), NullLogger<IncomingEventsController>.Instance);

        var result = await controller.SearchAsync("tenant-1", null, null, null, null, null, null, 1, 50, null, "desc", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<HookBridge.Application.DTOs.Common.PagedResponseDto<IncomingEventResponseDto>>(ok.Value);
        Assert.Single(payload.Items);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsOk_WhenFound()
    {
        var service = new FakeIncomingEventQueryService();
        var controller = new IncomingEventsController(service, new TenantIsolationTestHelpers.FakeCurrentUserContext(), TenantIsolationTestHelpers.CreateValidator(), NullLogger<IncomingEventsController>.Instance);

        var result = await controller.GetByIdAsync("incoming-1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<IncomingEventResponseDto>(ok.Value);
        Assert.Equal("incoming-1", payload.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNotFound_WhenMissing()
    {
        var service = new FakeIncomingEventQueryService();
        var controller = new IncomingEventsController(service, new TenantIsolationTestHelpers.FakeCurrentUserContext(), TenantIsolationTestHelpers.CreateValidator(), NullLogger<IncomingEventsController>.Instance);

        var result = await controller.GetByIdAsync("missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private sealed class FakeIncomingEventQueryService : IIncomingEventQueryService
    {
        private readonly List<IncomingEventResponseDto> _items =
        [
            new IncomingEventResponseDto
            {
                Id = "incoming-1",
                TenantId = "tenant-1",
                EventId = "evt-1",
                EventType = "order.created",
                Status = "Accepted",
                ReceivedAt = new DateTime(2026, 4, 27, 10, 0, 0, DateTimeKind.Utc),
                Payload = new { orderId = "1001" },
            },
        ];

        public Task<HookBridge.Application.DTOs.Common.PagedResponseDto<IncomingEventResponseDto>> SearchAsync(IncomingEventSearchRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(HookBridge.Application.DTOs.Common.PagedResponseDto<IncomingEventResponseDto>.Create(_items.Where(x => x.TenantId == request.TenantId).ToList(), request.NormalizedPageNumber, request.NormalizedPageSize, _items.LongCount(x => x.TenantId == request.TenantId)));

        public Task<IncomingEventResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
    }
}
