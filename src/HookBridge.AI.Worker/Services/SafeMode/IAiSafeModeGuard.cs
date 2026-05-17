using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.SafeMode;

public interface IAiSafeModeGuard
{
    Task<AiSafeModeEvaluationResponseDto> EvaluateAsync(AiSafeModeEvaluationRequestDto request, CancellationToken cancellationToken = default);
}
