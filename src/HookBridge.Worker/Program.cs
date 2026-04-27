using HookBridge.Application.DependencyInjection;
using HookBridge.Infrastructure.DependencyInjection;
using Elastic.Apm.DiagnosticSource;
using HookBridge.Infrastructure.Configuration;
using HookBridge.Infrastructure.Logging;
using HookBridge.Worker;
using Serilog;

var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddHostedService<WebhookEventConsumerWorker>();
builder.Services.AddHostedService<WebhookRetryConsumerWorker>();
builder.Services.AddSingleton<WorkerTransactionRunner>();

builder.Services.AddSerilog((services, loggerConfiguration) =>
    loggerConfiguration.ConfigureHookBridgeEcsLogging(builder.Configuration, "hookbridge-worker"));


var apmSettings = builder.Configuration.GetSection("ElasticApm").Get<ElasticApmSettings>() ?? new ElasticApmSettings();
if (apmSettings.Enabled)
{
    Environment.SetEnvironmentVariable("ELASTIC_APM_SERVER_URL", apmSettings.ServerUrl);
    Environment.SetEnvironmentVariable("ELASTIC_APM_SERVICE_NAME", apmSettings.ServiceName);
    Environment.SetEnvironmentVariable("ELASTIC_APM_ENVIRONMENT", apmSettings.Environment);
    Environment.SetEnvironmentVariable("ELASTIC_APM_ENABLED", "true");
    Elastic.Apm.Agent.Subscribe(new HttpDiagnosticsSubscriber());
}

var host = builder.Build();
host.Run();
