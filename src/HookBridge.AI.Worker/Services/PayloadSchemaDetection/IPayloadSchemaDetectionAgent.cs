using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.PayloadSchemaDetection;

public interface IPayloadSchemaDetectionAgent
{
    Task<PayloadSchemaDetectionResponseDto> DetectAsync(
        PayloadSchemaDetectionRequestDto request,
        CancellationToken cancellationToken = default);
}
