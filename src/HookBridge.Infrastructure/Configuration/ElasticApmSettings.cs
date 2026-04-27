namespace HookBridge.Infrastructure.Configuration;

public sealed class ElasticApmSettings
{
    public string ServerUrl { get; set; } = "http://localhost:8200";
    public string ServiceName { get; set; } = "hookbridge";
    public string Environment { get; set; } = "Development";
    public bool Enabled { get; set; }
}
