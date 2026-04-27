namespace HookBridge.Shared.Constants;

public static class ValidationLimits
{
    public const int MaxEventTypeLength = 150;
    public const int MaxEventIdLength = 150;
    public const int MaxPayloadSizeBytes = 1_000_000;
    public const int MaxHeaderNameLength = 100;
    public const int MaxHeaderValueLength = 1_000;
    public const int MaxCustomHeaders = 30;
    public const int MaxTargetUrlLength = 2_048;
    public const int MaxResponseBodyStoredLength = 5_000;
}
