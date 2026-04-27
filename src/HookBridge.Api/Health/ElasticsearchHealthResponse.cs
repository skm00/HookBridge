namespace HookBridge.Api.Health;

public sealed class ElasticsearchHealthResponse
{
    public string Service { get; init; } = "Elasticsearch";

    public bool IsHealthy { get; init; }

    public string Message { get; init; } = string.Empty;
}
