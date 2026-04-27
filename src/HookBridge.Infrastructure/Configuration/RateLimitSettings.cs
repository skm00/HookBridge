namespace HookBridge.Infrastructure.Configuration;

public sealed class RateLimitSettings
{
    public bool Enabled { get; set; } = true;

    public int EventIngestionPermitLimit { get; set; } = 100;

    public int EventIngestionWindowSeconds { get; set; } = 60;

    public int AdminApiPermitLimit { get; set; } = 300;

    public int AdminApiWindowSeconds { get; set; } = 60;
}
