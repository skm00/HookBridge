using System.Diagnostics;
using System.Text;
using System.Text.Json;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Models.Delivery;
using Microsoft.Extensions.Logging;

namespace HookBridge.Infrastructure.Services;

public sealed class WebhookDeliveryClient : IWebhookDeliveryClient
{
    private const string JsonContentType = "application/json";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebhookAuthenticationHandler _webhookAuthenticationHandler;
    private readonly ILogger<WebhookDeliveryClient> _logger;

    public WebhookDeliveryClient(
        IHttpClientFactory httpClientFactory,
        IWebhookAuthenticationHandler webhookAuthenticationHandler,
        ILogger<WebhookDeliveryClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _webhookAuthenticationHandler = webhookAuthenticationHandler;
        _logger = logger;
    }

    public async Task<WebhookDeliveryResult> SendAsync(WebhookDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(request.TargetUrl)
            || !Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var targetUri))
        {
            stopwatch.Stop();
            return new WebhookDeliveryResult
            {
                IsSuccess = false,
                ErrorMessage = "Invalid target URL. TargetUrl must be a non-empty absolute URL.",
                DurationMs = stopwatch.ElapsedMilliseconds,
            };
        }

        _logger.LogInformation(
            "Starting webhook delivery. TenantId: {TenantId}, EventId: {EventId}, EventType: {EventType}, TargetUrl: {TargetUrl}, CorrelationId: {CorrelationId}",
            request.TenantId,
            request.EventId,
            request.EventType,
            request.TargetUrl,
            request.CorrelationId);

        var timeoutSeconds = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 30;

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            using var message = new HttpRequestMessage(HttpMethod.Post, targetUri);
            var payloadJson = JsonSerializer.Serialize(request.Payload);
            message.Content = new StringContent(payloadJson, Encoding.UTF8, JsonContentType);

            AddHeaderIfPresent(message, "x-hookbridge-event-id", request.EventId);
            AddHeaderIfPresent(message, "x-hookbridge-event-type", request.EventType);
            AddHeaderIfPresent(message, "x-hookbridge-tenant-id", request.TenantId);
            AddHeaderIfPresent(message, "x-correlation-id", request.CorrelationId);

            foreach (var header in request.Headers)
            {
                if (string.IsNullOrWhiteSpace(header.Name) || string.Equals(header.Name, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                message.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }

            await _webhookAuthenticationHandler.ApplyAsync(message, request, timeoutCts.Token);

            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(message, timeoutCts.Token);
            var responseBody = await response.Content.ReadAsStringAsync(timeoutCts.Token);

            stopwatch.Stop();

            var result = new WebhookDeliveryResult
            {
                IsSuccess = (int)response.StatusCode is >= 200 and < 300,
                HttpStatusCode = (int)response.StatusCode,
                ResponseBody = responseBody,
                DurationMs = stopwatch.ElapsedMilliseconds,
            };

            _logger.LogInformation(
                "Webhook delivery completed. TenantId: {TenantId}, EventId: {EventId}, EventType: {EventType}, TargetUrl: {TargetUrl}, HttpStatusCode: {HttpStatusCode}, DurationMs: {DurationMs}, CorrelationId: {CorrelationId}",
                request.TenantId,
                request.EventId,
                request.EventType,
                request.TargetUrl,
                result.HttpStatusCode,
                result.DurationMs,
                request.CorrelationId);

            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            var result = new WebhookDeliveryResult
            {
                IsSuccess = false,
                ErrorMessage = $"Webhook delivery timed out after {timeoutSeconds} seconds.",
                DurationMs = stopwatch.ElapsedMilliseconds,
            };

            _logger.LogWarning(
                "Webhook delivery timed out. TenantId: {TenantId}, EventId: {EventId}, EventType: {EventType}, TargetUrl: {TargetUrl}, DurationMs: {DurationMs}, CorrelationId: {CorrelationId}",
                request.TenantId,
                request.EventId,
                request.EventType,
                request.TargetUrl,
                result.DurationMs,
                request.CorrelationId);

            return result;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            var result = new WebhookDeliveryResult
            {
                IsSuccess = false,
                ErrorMessage = $"HTTP request failed: {ex.Message}",
                DurationMs = stopwatch.ElapsedMilliseconds,
            };

            _logger.LogWarning(
                ex,
                "Webhook delivery request failed. TenantId: {TenantId}, EventId: {EventId}, EventType: {EventType}, TargetUrl: {TargetUrl}, DurationMs: {DurationMs}, CorrelationId: {CorrelationId}",
                request.TenantId,
                request.EventId,
                request.EventType,
                request.TargetUrl,
                result.DurationMs,
                request.CorrelationId);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var result = new WebhookDeliveryResult
            {
                IsSuccess = false,
                ErrorMessage = $"Unexpected error during webhook delivery: {ex.Message}",
                DurationMs = stopwatch.ElapsedMilliseconds,
            };

            _logger.LogError(
                ex,
                "Unexpected webhook delivery error. TenantId: {TenantId}, EventId: {EventId}, EventType: {EventType}, TargetUrl: {TargetUrl}, DurationMs: {DurationMs}, CorrelationId: {CorrelationId}",
                request.TenantId,
                request.EventId,
                request.EventType,
                request.TargetUrl,
                result.DurationMs,
                request.CorrelationId);

            return result;
        }
    }

    private static void AddHeaderIfPresent(HttpRequestMessage message, string headerName, string? headerValue)
    {
        if (!string.IsNullOrWhiteSpace(headerValue))
        {
            message.Headers.TryAddWithoutValidation(headerName, headerValue);
        }
    }
}
