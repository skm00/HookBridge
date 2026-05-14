using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services.FluentValidationRuleGeneration;

public interface IFluentValidationRuleGenerationAgent
{
    Task<FluentValidationRuleGenerationResponseDto> GenerateAsync(
        FluentValidationRuleGenerationRequestDto request,
        CancellationToken cancellationToken = default);
}
