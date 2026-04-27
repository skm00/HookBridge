using HookBridge.Infrastructure.DependencyInjection;
using HookBridge.Worker;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddHostedService<WebhookEventConsumerWorker>();

var host = builder.Build();
host.Run();
