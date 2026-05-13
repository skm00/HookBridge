using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Health;

public sealed class AiWorkerHealthStatus
{
    private readonly IOptions<AiOptions> _options;

    public AiWorkerHealthStatus(IOptions<AiOptions> options)
    {
        _options = options;
    }

    public AiHealthStatus GetStatus()
    {
        var options = _options.Value;
        var hasEndpoint = Uri.TryCreate(options.Endpoint, UriKind.Absolute, out _);
        var hasProvider = !string.IsNullOrWhiteSpace(options.Provider);
        var hasModel = !string.IsNullOrWhiteSpace(options.Model);

        return new AiHealthStatus(
            IsHealthy: options.Enabled && hasEndpoint && hasProvider && hasModel,
            Enabled: options.Enabled,
            Provider: options.Provider,
            Model: options.Model,
            Endpoint: options.Endpoint);
    }
}

public sealed record AiHealthStatus(
    bool IsHealthy,
    bool Enabled,
    string Provider,
    string Model,
    string Endpoint);
