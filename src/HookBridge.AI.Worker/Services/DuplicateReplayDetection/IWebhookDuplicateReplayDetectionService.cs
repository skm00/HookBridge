using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.DuplicateReplayDetection;

public interface IWebhookDuplicateReplayDetectionService
{
    Task<WebhookDuplicateReplayDetectionResponseDto> DetectAsync(WebhookDuplicateReplayDetectionRequestDto request, CancellationToken cancellationToken = default);
}
