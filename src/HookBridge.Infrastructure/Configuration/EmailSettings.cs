namespace HookBridge.Infrastructure.Configuration;

public sealed class EmailSettings
{
    public bool Enabled { get; set; }

    public string Provider { get; set; } = "Smtp";

    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public string SmtpUsername { get; set; } = string.Empty;

    public string SmtpPassword { get; set; } = string.Empty;

    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;

    public bool UseSsl { get; set; } = true;
}
