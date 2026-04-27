namespace HookBridge.Infrastructure.Configuration;

public sealed class ElasticSettings
{
    public string ElasticsearchUrl { get; set; } = string.Empty;

    public string Environment { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public bool EnableElasticsearchSink { get; set; }
}
