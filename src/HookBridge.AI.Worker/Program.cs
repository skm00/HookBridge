using HookBridge.AI.Worker;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Health;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAiOptions(builder.Configuration);
builder.Services.AddSingleton<AiWorkerHealthStatus>();
builder.Services.AddAiKernelServices();
builder.Services.AddHostedService<AiProcessingWorker>();

var host = builder.Build();
host.Run();
