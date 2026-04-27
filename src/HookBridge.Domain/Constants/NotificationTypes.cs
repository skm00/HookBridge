namespace HookBridge.Domain.Constants;

public static class NotificationTypes
{
    public const string WebhookFailure = nameof(WebhookFailure);
    public const string DlqCreated = nameof(DlqCreated);
    public const string BillingPaymentFailed = nameof(BillingPaymentFailed);
    public const string UsageLimitWarning = nameof(UsageLimitWarning);
    public const string UsageLimitExceeded = nameof(UsageLimitExceeded);
}
