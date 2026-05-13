namespace HookBridge.AI.Worker.Services;

public interface ILocalLlmClient
{
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
}
