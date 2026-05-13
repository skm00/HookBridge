using HookBridge.AI.Worker.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker;

public sealed class AiProcessingWorker : BackgroundService
{
    private readonly ILogger<AiProcessingWorker> _logger;
    private readonly IOptions<AiOptions> _options;

    public AiProcessingWorker(ILogger<AiProcessingWorker> logger, IOptions<AiOptions> options)
    {
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;
        _logger.LogInformation(
            "HookBridge AI Worker starting. Enabled: {Enabled}, Provider: {Provider}, Model: {Model}, Endpoint: {Endpoint}",
            options.Enabled,
            options.Provider,
            options.Model,
            options.Endpoint);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during graceful shutdown.
        }
        finally
        {
            _logger.LogInformation("HookBridge AI Worker shutting down.");
        }
    }
}
