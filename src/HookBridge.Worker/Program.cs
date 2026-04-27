using HookBridge.Application.DependencyInjection;
using HookBridge.Infrastructure.DependencyInjection;
using HookBridge.Worker;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddHostedService<WebhookEventConsumerWorker>();
builder.Services.AddHostedService<WebhookRetryConsumerWorker>();

var host = builder.Build();
host.Run();
