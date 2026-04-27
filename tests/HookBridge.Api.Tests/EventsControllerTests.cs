using HookBridge.Api.Controllers;
using HookBridge.Application.DTOs.Events;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HookBridge.Api.Tests;

public sealed class EventsControllerTests
{
    [Fact]
    public async Task MissingApiKey_Returns401()
    {
        var controller = BuildController(new FakeEventIngestionService());

        var result = await controller.IngestAsync("tenant-1", BuildRequest(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task InvalidApiKey_Returns401()
    {
        var controller = BuildController(new FakeEventIngestionService(throwUnauthorized: true));
        controller.Request.Headers["x-api-key"] = "bad-key";

        var result = await controller.IngestAsync("tenant-1", BuildRequest(), CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task LimitExceeded_Returns429()
    {
        var controller = BuildController(new FakeEventIngestionService(throwTooManyRequests: true));
        controller.Request.Headers["x-api-key"] = "hb_live_key";

        var result = await controller.IngestAsync("tenant-1", BuildRequest(), CancellationToken.None);

        var tooMany = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, tooMany.StatusCode);
    }

    private static EventsController BuildController(IEventIngestionService service)
    {
        var controller = new EventsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

        return controller;
    }

    private static EventIngestionRequestDto BuildRequest() => new()
    {
        EventType = "order.created",
        EventId = "evt_123",
        Data = new { orderId = "1001" },
    };

    private sealed class FakeEventIngestionService(bool throwUnauthorized = false, bool throwTooManyRequests = false) : IEventIngestionService
    {
        public Task<EventIngestionResponseDto> IngestAsync(
            string tenantId,
            string apiKey,
            EventIngestionRequestDto request,
            string? correlationId,
            CancellationToken cancellationToken = default)
        {
            if (throwUnauthorized)
            {
                throw new UnauthorizedException("Invalid API key.");
            }
            if (throwTooManyRequests)
            {
                throw new TooManyRequestsException("Monthly event limit exceeded for the current billing plan.");
            }

            return Task.FromResult(new EventIngestionResponseDto
            {
                Status = "accepted",
                EventId = request.EventId,
                Message = "Event accepted for delivery.",
            });
        }
    }
}
