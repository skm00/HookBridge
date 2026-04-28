using Elastic.CommonSchema.Serilog;
using Elastic.Serilog.Sinks;
using HookBridge.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace HookBridge.Infrastructure.Logging;

public static class SerilogConfigurationExtensions
{
    public static LoggerConfiguration ConfigureHookBridgeEcsLogging(
        this LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        string defaultServiceName)
    {
        var elasticSettings = configuration.GetSection("Elastic").Get<ElasticSettings>() ?? new ElasticSettings();
        var serviceName = string.IsNullOrWhiteSpace(elasticSettings.ServiceName)
            ? defaultServiceName
            : elasticSettings.ServiceName;
        var environment = string.IsNullOrWhiteSpace(elasticSettings.Environment)
            ? "Development"
            : elasticSettings.Environment;

        loggerConfiguration
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service.name", serviceName)
            .Enrich.WithProperty("service.environment", environment)
            .Enrich.WithProperty("application", "HookBridge")
            .WriteTo.Console(new EcsTextFormatter());

        if (elasticSettings.EnableElasticsearchSink && Uri.TryCreate(elasticSettings.ElasticsearchUrl, UriKind.Absolute, out var endpoint))
        {
            loggerConfiguration.WriteTo.Elasticsearch(
                [endpoint],
                options =>
                {
                    // Keep sink configuration minimal to remain compatible across Elastic.Serilog.Sinks versions.
                    // Data stream and bootstrapping use package defaults unless explicitly required.
                });
        }

        return loggerConfiguration;
    }
}
