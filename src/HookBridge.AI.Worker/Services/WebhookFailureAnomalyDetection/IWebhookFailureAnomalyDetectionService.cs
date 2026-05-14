using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.WebhookFailureAnomalyDetection;

public interface IWebhookFailureAnomalyDetectionService
{
    WebhookFailureAnomalyDetectionResponseDto DetectAnomalies(WebhookFailureAnomalyDetectionRequestDto request, DateTime calculatedAtUtc);
}
