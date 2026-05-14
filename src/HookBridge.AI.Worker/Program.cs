using HookBridge.AI.Worker;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Health;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAiOptions(builder.Configuration);
builder.Services.AddAiKafkaOptions(builder.Configuration);
builder.Services.AddAiMongoOptions(builder.Configuration);
builder.Services.AddSingleton<AiWorkerHealthStatus>();
builder.Services.AddAiKernelServices();
builder.Services.AddAiPromptServices();
builder.Services.AddAiRetryRecommendationServices();
builder.Services.AddAiLogSummarizationServices();
builder.Services.AddPayloadSchemaDetectionServices();
builder.Services.AddJsonToDtoSuggestionServices();
builder.Services.AddFluentValidationRuleGenerationServices();
builder.Services.AddWebhookTransformationRecommendationServices();
builder.Services.AddEndpointHealthScoringServices();
builder.Services.AddAiKafkaServices();
builder.Services.AddAiMongoServices();
builder.Services.AddHostedService<AiProcessingWorker>();
builder.Services.AddHostedService<PayloadSchemaDetectionWorker>();
builder.Services.AddHostedService<JsonToDtoSuggestionWorker>();
builder.Services.AddHostedService<FluentValidationRuleGenerationWorker>();
builder.Services.AddHostedService<WebhookTransformationRecommendationWorker>();

var host = builder.Build();
host.Run();
