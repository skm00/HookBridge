namespace HookBridge.Infrastructure.Configuration;

public sealed class StripeSettings
{
    public string SecretKey { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;

    public string StarterPriceId { get; set; } = string.Empty;

    public string ProPriceId { get; set; } = string.Empty;

    public string EnterprisePriceId { get; set; } = string.Empty;

    public string SuccessUrl { get; set; } = string.Empty;

    public string CancelUrl { get; set; } = string.Empty;
}
