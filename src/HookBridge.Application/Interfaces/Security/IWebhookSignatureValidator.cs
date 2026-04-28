namespace HookBridge.Application.Interfaces.Security;

public interface IWebhookSignatureValidator
{
    bool Validate(string payload, string signatureHeader, string secret);
}
