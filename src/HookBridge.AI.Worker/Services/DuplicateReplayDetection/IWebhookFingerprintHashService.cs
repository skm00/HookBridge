namespace HookBridge.AI.Worker.Services.DuplicateReplayDetection;

public interface IWebhookFingerprintHashService
{
    string? GeneratePayloadHash(object? payload);
    string? GenerateSignatureHash(string? signature);
}
