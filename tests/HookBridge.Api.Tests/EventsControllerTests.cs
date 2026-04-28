using HookBridge.Api.Controllers;
using HookBridge.Api.Security;
using HookBridge.Application.DTOs.ApiKeys;
using HookBridge.Application.DTOs.Events;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces.Security;
using HookBridge.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using Xunit;

namespace HookBridge.Api.Tests;

public sealed class EventsControllerTests
{
    [Fact]
    public async Task MissingApiKey_Returns401()
    {
        var controller = BuildController(new FakeEventIngestionService(), new FakeApiKeyService());
        SetRequestBody(controller, BuildRequestJson());

        var result = await controller.IngestAsync("tenant-1", CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task InvalidApiKey_Returns401()
    {
        var controller = BuildController(new FakeEventIngestionService(), new FakeApiKeyService(isValid: false));
        controller.Request.Headers["x-api-key"] = "bad-key";
        SetRequestBody(controller, BuildRequestJson());

        var result = await controller.IngestAsync("tenant-1", CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task LimitExceeded_Returns429()
    {
        var controller = BuildController(new FakeEventIngestionService(throwTooManyRequests: true), new FakeApiKeyService());
        controller.Request.Headers["x-api-key"] = "hb_live_key";
        SetRequestBody(controller, BuildRequestJson());

        var result = await controller.IngestAsync("tenant-1", CancellationToken.None);

        var tooMany = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, tooMany.StatusCode);
    }

    [Fact]
    public async Task SignatureRequired_ValidSignature_Passes()
    {
        var controller = BuildController(
            new FakeEventIngestionService(),
            new FakeApiKeyService(requireSignature: true, signatureSecret: "sig-secret"),
            new FakeWebhookSignatureValidator());

        controller.Request.Headers["x-api-key"] = "hb_live_key";
        controller.Request.Headers["x-hookbridge-signature"] = "sha256=valid";
        SetRequestBody(controller, BuildRequestJson());

        var result = await controller.IngestAsync("tenant-1", CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result.Result);
        Assert.Equal(StatusCodes.Status202Accepted, accepted.StatusCode);
    }

    [Fact]
    public async Task SignatureRequired_InvalidSignature_Returns401()
    {
        var controller = BuildController(
            new FakeEventIngestionService(),
            new FakeApiKeyService(requireSignature: true, signatureSecret: "sig-secret"),
            new FakeWebhookSignatureValidator());

        controller.Request.Headers["x-api-key"] = "hb_live_key";
        controller.Request.Headers["x-hookbridge-signature"] = "sha256=bad";
        SetRequestBody(controller, BuildRequestJson());

        var result = await controller.IngestAsync("tenant-1", CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task SignatureRequired_MissingHeader_Returns401()
    {
        var controller = BuildController(
            new FakeEventIngestionService(),
            new FakeApiKeyService(requireSignature: true, signatureSecret: "sig-secret"));

        controller.Request.Headers["x-api-key"] = "hb_live_key";
        SetRequestBody(controller, BuildRequestJson());

        var result = await controller.IngestAsync("tenant-1", CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task SignatureNotRequired_AllowsRequest()
    {
        var controller = BuildController(new FakeEventIngestionService(), new FakeApiKeyService(requireSignature: false));
        controller.Request.Headers["x-api-key"] = "hb_live_key";
        SetRequestBody(controller, BuildRequestJson());

        var result = await controller.IngestAsync("tenant-1", CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result.Result);
        Assert.Equal(StatusCodes.Status202Accepted, accepted.StatusCode);
    }

    [Fact]
    public async Task AllowlistBlocked_Returns403()
    {
        var controller = BuildController(
            new FakeEventIngestionService(),
            new FakeApiKeyService(allowedIpAddresses: ["10.0.0.0/24"]));
        controller.Request.Headers["x-api-key"] = "hb_live_key";
        controller.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.10");
        SetRequestBody(controller, BuildRequestJson());

        var result = await controller.IngestAsync("tenant-1", CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    private static EventsController BuildController(IEventIngestionService service, IApiKeyService apiKeyService, IWebhookSignatureValidator? validator = null)
    {
        var controller = new EventsController(
            service,
            apiKeyService,
            validator ?? new FakeWebhookSignatureValidator(),
            new ClientIpResolver(),
            new IpAllowlistService(),
            NullLogger<EventsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

        return controller;
    }

    private static void SetRequestBody(EventsController controller, string body)
    {
        controller.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        controller.Request.ContentType = "application/json";
    }

    private static EventIngestionRequestDto BuildRequest() => new()
    {
        EventType = "order.created",
        EventId = "evt_123",
        Data = new { orderId = "1001" },
      };

    private static string BuildRequestJson() =>
        """
        {
          "eventType": "order.created",
          "eventId": "evt_123",
          "data": { "orderId": "1001" }
        }
        """;

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

    private sealed class FakeApiKeyService(bool isValid = true, bool requireSignature = false, string? signatureSecret = null, List<string>? allowedIpAddresses = null) : IApiKeyService
    {
        public Task<CreateApiKeyResponseDto> CreateAsync(string tenantId, CreateApiKeyRequestDto request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IReadOnlyList<ApiKeyResponseDto>> GetByTenantAsync(string tenantId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<ApiKeyResponseDto?> UpdateAsync(string tenantId, string keyId, UpdateApiKeyRequestDto request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<bool> RevokeAsync(string tenantId, string keyId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<ApiKeyValidationResult> ValidateAsync(string tenantId, string plainApiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new ApiKeyValidationResult
            {
                IsValid = isValid,
                TenantId = tenantId,
                ApiKeyId = "key-1",
                EnableSignatureValidation = requireSignature,
                SignatureHeaderName = "x-hookbridge-signature",
                SignatureSecret = signatureSecret,
                AllowedIpAddresses = allowedIpAddresses,
                FailureReason = isValid ? null : "api_key_invalid",
            });
    }

    private sealed class FakeWebhookSignatureValidator : IWebhookSignatureValidator
    {
        public bool Validate(string payload, string signatureHeader, string secret)
            => signatureHeader == "sha256=valid" && secret == "sig-secret";
    }
}
