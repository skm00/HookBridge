using Asp.Versioning;
using HookBridge.Api.RateLimiting;
using HookBridge.Api.Security;
using HookBridge.Application.DTOs.Events;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces.Security;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Shared.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace HookBridge.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/events/{tenantId}")]
[EnableRateLimiting(RateLimitingPolicyNames.EventIngestionPolicy)]
public sealed class EventsController(
    IEventIngestionService eventIngestionService,
    IApiKeyService apiKeyService,
    IWebhookSignatureValidator webhookSignatureValidator,
    IClientIpResolver clientIpResolver,
    IIpAllowlistService ipAllowlistService,
    ILogger<EventsController> logger) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<EventIngestionResponseDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<EventIngestionResponseDto>>> IngestAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("x-api-key", out var apiKeyHeader) || string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            return ErrorResponse(StatusCodes.Status401Unauthorized, "Unauthorized.");
        }

        var rawPayload = await ReadRawPayloadAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Invalid request payload.");
        }

        EventIngestionRequestDto? request;
        try
        {
            request = JsonSerializer.Deserialize<EventIngestionRequestDto>(rawPayload, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (JsonException)
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Invalid request payload.");
        }

        if (request is null)
        {
            return ErrorResponse(StatusCodes.Status400BadRequest, "Invalid request payload.");
        }

        var correlationId = Request.Headers.TryGetValue("x-correlation-id", out var correlationHeader)
            ? correlationHeader.ToString()
            : null;

        var apiKeyValidation = await apiKeyService.ValidateAsync(tenantId, apiKeyHeader.ToString(), cancellationToken);
        if (!apiKeyValidation.IsValid)
        {
            return ErrorResponse(StatusCodes.Status401Unauthorized, "Unauthorized.");
        }

        var clientIp = clientIpResolver.GetClientIp(HttpContext);
        if (!ipAllowlistService.IsAllowed(clientIp, apiKeyValidation.AllowedIpAddresses))
        {
            logger.LogWarning(
                "Blocked event ingestion due to IP allowlist. TenantId={TenantId}, ApiKeyId={ApiKeyId}, ClientIp={ClientIp}, Path={Path}",
                tenantId,
                apiKeyValidation.ApiKeyId,
                clientIp,
                Request.Path.Value);
            return ErrorResponse(StatusCodes.Status403Forbidden, "IP address is not allowed.");
        }

        if (apiKeyValidation.EnableSignatureValidation)
        {
            if (!Request.Headers.TryGetValue(apiKeyValidation.SignatureHeaderName, out var signatureHeader))
            {
                return ErrorResponse(StatusCodes.Status401Unauthorized, "Invalid signature.");
            }

            var isValidSignature = webhookSignatureValidator.Validate(
                rawPayload,
                signatureHeader.ToString(),
                apiKeyValidation.SignatureSecret ?? string.Empty);
            if (!isValidSignature)
            {
                return ErrorResponse(StatusCodes.Status401Unauthorized, "Invalid signature.");
            }
        }

        try
        {
            var response = await eventIngestionService.IngestAsync(
                tenantId,
                apiKeyHeader.ToString(),
                request,
                correlationId,
                cancellationToken);

            return AcceptedResponse(response);
        }
        catch (UnauthorizedException)
        {
            return ErrorResponse(StatusCodes.Status401Unauthorized, "Unauthorized.");
        }
        catch (TooManyRequestsException)
        {
            return ErrorResponse(StatusCodes.Status429TooManyRequests, "Rate limit exceeded. Please try again later.");
        }
    }

    private async Task<string> ReadRawPayloadAsync(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();
        Request.Body.Position = 0;
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync();
        Request.Body.Position = 0;
        return payload;
    }
}
