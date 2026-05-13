using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Services;

public interface ILocalLlmClient
{
    Task<LlmResponseResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
}
