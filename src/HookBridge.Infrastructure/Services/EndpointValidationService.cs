using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using HookBridge.Application.DTOs.EndpointValidation;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Models.Delivery;

namespace HookBridge.Infrastructure.Services;

public sealed class EndpointValidationService(
    IHttpClientFactory httpClientFactory,
    IWebhookAuthenticationHandler webhookAuthenticationHandler) : IEndpointValidationService
{
    public async Task<EndpointValidationResponseDto> ValidateAsync(EndpointValidationRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new EndpointValidationResponseDto { IsSuccess = false, Message = "TargetUrl must be an absolute HTTP/HTTPS URL." };
        }

        var method = HttpMethod.Post;
        if (!string.IsNullOrWhiteSpace(request.Method))
        {
            try
            {
                method = new HttpMethod(request.Method.Trim().ToUpperInvariant());
            }
            catch (FormatException)
            {
                return new EndpointValidationResponseDto
                {
                    IsSuccess = false,
                    Message = "Method is invalid."
                };
            }
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(request.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var stopWatch = Stopwatch.StartNew();

        try
        {
            using var httpRequest = new HttpRequestMessage(method, uri);
            if (request.SamplePayload is not null)
            {
                var json = JsonSerializer.Serialize(request.SamplePayload);
                httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            foreach (var header in request.Headers ?? [])
            {
                httpRequest.Headers.TryAddWithoutValidation(header.Name, header.Value);
            }

            await webhookAuthenticationHandler.ApplyAsync(httpRequest, new WebhookDeliveryRequest
            {
                Authentication = request.Authentication
            }, linkedCts.Token);

            var client = httpClientFactory.CreateClient();
            using var response = await client.SendAsync(httpRequest, linkedCts.Token);
            var body = await response.Content.ReadAsStringAsync(linkedCts.Token);
            stopWatch.Stop();

            return new EndpointValidationResponseDto
            {
                IsSuccess = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Message = response.IsSuccessStatusCode
                    ? "Endpoint is reachable."
                    : "Endpoint validation failed.",
                DurationMs = stopWatch.ElapsedMilliseconds,
                ResponseBody = body.Length > 2000 ? body[..2000] : body
            };
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            stopWatch.Stop();
            return new EndpointValidationResponseDto
            {
                IsSuccess = false,
                StatusCode = (int)HttpStatusCode.RequestTimeout,
                Message = $"Endpoint validation timed out after {request.TimeoutSeconds} seconds.",
                DurationMs = stopWatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopWatch.Stop();
            return new EndpointValidationResponseDto
            {
                IsSuccess = false,
                Message = $"Endpoint validation failed: {ex.Message}",
                DurationMs = stopWatch.ElapsedMilliseconds
            };
        }
    }
}
