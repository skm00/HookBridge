using HookBridge.AI.Worker;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Health;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAiOptions(builder.Configuration);
builder.Services.AddAiKafkaOptions(builder.Configuration);
builder.Services.AddAiMongoOptions(builder.Configuration);
builder.Services.AddSingleton<AiWorkerHealthStatus>();
builder.Services.AddAiKernelServices();
builder.Services.AddAiKafkaServices();
builder.Services.AddAiMongoServices();
builder.Services.AddHostedService<AiProcessingWorker>();

var host = builder.Build();
host.Run();
