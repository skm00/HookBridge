using HookBridge.Application.DependencyInjection;
using HookBridge.Infrastructure.DependencyInjection;
using HookBridge.Infrastructure.Logging;
using HookBridge.Worker;
using Serilog;

var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddHostedService<WebhookEventConsumerWorker>();
builder.Services.AddHostedService<WebhookRetryConsumerWorker>();

builder.Services.AddSerilog((services, loggerConfiguration) =>
    loggerConfiguration.ConfigureHookBridgeEcsLogging(builder.Configuration, "hookbridge-worker"));

var host = builder.Build();
host.Run();
