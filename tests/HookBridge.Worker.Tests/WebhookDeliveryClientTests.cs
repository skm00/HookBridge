using System.Net;
using System.Net.Http;
using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.Models.Delivery;
using HookBridge.Infrastructure.Services;
using Moq;

namespace HookBridge.Worker.Tests;

public sealed class WebhookDeliveryClientTests
{
    [Fact]
    public async Task SendAsync_SendsPostRequestToTargetUrl()
    {
        var handler = new DelegatingHandlerStub((request, _) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://example.com/webhook", request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var client = CreateClient(handler);
        var request = CreateRequest();

        await client.SendAsync(request);

        Assert.NotNull(handler.LastRequest);
    }

    [Fact]
    public async Task SendAsync_AddsDefaultHookBridgeHeaders()
    {
        var handler = new DelegatingHandlerStub((request, _) =>
        {
            Assert.True(request.Headers.TryGetValues("x-hookbridge-event-id", out var eventIds));
            Assert.Equal("evt-1", eventIds!.Single());
            Assert.True(request.Headers.TryGetValues("x-hookbridge-event-type", out var eventTypes));
            Assert.Equal("order.created", eventTypes!.Single());
            Assert.True(request.Headers.TryGetValues("x-hookbridge-tenant-id", out var tenantIds));
            Assert.Equal("tenant-1", tenantIds!.Single());
            Assert.True(request.Headers.TryGetValues("x-correlation-id", out var correlationIds));
            Assert.Equal("corr-1", correlationIds!.Single());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var client = CreateClient(handler);

        await client.SendAsync(CreateRequest());
    }

    [Fact]
    public async Task SendAsync_AddsCustomHeaders()
    {
        var handler = new DelegatingHandlerStub((request, _) =>
        {
            Assert.True(request.Headers.TryGetValues("x-custom-a", out var values));
            Assert.Equal("alpha", values!.Single());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var client = CreateClient(handler);
        var request = CreateRequest();
        request.Headers.Add(new KeyValueDto { Name = "x-custom-a", Value = "alpha" });

        await client.SendAsync(request);
    }

    [Fact]
    public async Task SendAsync_SkipsContentTypeFromCustomHeaders()
    {
        var handler = new DelegatingHandlerStub((request, _) =>
        {
            Assert.False(request.Headers.Contains("Content-Type"));
            Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var client = CreateClient(handler);
        var request = CreateRequest();
        request.Headers.Add(new KeyValueDto { Name = "Content-Type", Value = "text/plain" });

        await client.SendAsync(request);
    }

    [Fact]
    public async Task SendAsync_ReturnsSuccessFor2xx()
    {
        var handler = new DelegatingHandlerStub((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)));

        var client = CreateClient(handler);

        var result = await client.SendAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(202, result.HttpStatusCode);
    }

    [Fact]
    public async Task SendAsync_ReturnsFailureFor4xxOr5xx()
    {
        var handler = new DelegatingHandlerStub((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var client = CreateClient(handler);

        var result = await client.SendAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(500, result.HttpStatusCode);
    }

    [Fact]
    public async Task SendAsync_HandlesTimeout()
    {
        var handler = new DelegatingHandlerStub(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var client = CreateClient(handler);
        var request = CreateRequest();
        request.TimeoutSeconds = 1;

        var result = await client.SendAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Contains("timed out", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_HandlesInvalidUrl()
    {
        var handler = new DelegatingHandlerStub((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var client = CreateClient(handler);
        var request = CreateRequest();
        request.TargetUrl = "not-a-url";

        var result = await client.SendAsync(request);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task SendAsync_CapturesResponseBody()
    {
        var handler = new DelegatingHandlerStub((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"bad\"}")
            }));

        var client = CreateClient(handler);

        var result = await client.SendAsync(CreateRequest());

        Assert.Equal("{\"error\":\"bad\"}", result.ResponseBody);
    }

    [Fact]
    public async Task SendAsync_CapturesDuration()
    {
        var handler = new DelegatingHandlerStub(async (_, token) =>
        {
            await Task.Delay(30, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var client = CreateClient(handler);

        var result = await client.SendAsync(CreateRequest());

        Assert.True(result.DurationMs > 0);
    }

    private static WebhookDeliveryClient CreateClient(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        return new WebhookDeliveryClient(factory.Object, new TestLogger<WebhookDeliveryClient>());
    }

    private static WebhookDeliveryRequest CreateRequest()
    {
        return new WebhookDeliveryRequest
        {
            TargetUrl = "https://example.com/webhook",
            EventId = "evt-1",
            TenantId = "tenant-1",
            EventType = "order.created",
            Payload = new { orderId = "1001" },
            TimeoutSeconds = 3,
            CorrelationId = "corr-1",
        };
    }

    private sealed class DelegatingHandlerStub : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public DelegatingHandlerStub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return _handler(request, cancellationToken);
        }
    }
}
