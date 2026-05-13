using Microsoft.Extensions.Hosting;
using HookBridge.Application.DependencyInjection;
using HookBridge.Application.Interfaces;
using HookBridge.Infrastructure.DependencyInjection;
using Elastic.Apm.DiagnosticSource;
using HookBridge.Infrastructure.Configuration;
using HookBridge.Infrastructure.Logging;
using HookBridge.Worker;
using HookBridge.Worker.KafkaSwapBuffer;
using Serilog;

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"))
    && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")))
{
    Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", Environments.Development);
}

var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment, requireKafkaConsumerGroupId: true);
builder.Services.AddApplicationServices();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, WorkerCurrentUserContext>();
builder.Services.Configure<KafkaConsumerOptions>(builder.Configuration.GetSection("KafkaConsumer"));
builder.Services.PostConfigure<KafkaConsumerOptions>(options =>
{
    var kafkaSettings = builder.Configuration.GetSection("Kafka").Get<KafkaSettings>() ?? new KafkaSettings();
    if (string.IsNullOrWhiteSpace(options.BootstrapServers))
    {
        options.BootstrapServers = kafkaSettings.BootstrapServers;
    }

    if (string.IsNullOrWhiteSpace(options.GroupId))
    {
        options.GroupId = kafkaSettings.ConsumerGroupId;
    }
});
builder.Services.AddSingleton<IKafkaSwapBufferConsumerFactory, KafkaSwapBufferConsumerFactory>();
builder.Services.AddSingleton<IWebhookEventBatchStore, MongoWebhookEventBatchStore>();
builder.Services.AddHostedService<SwapBufferKafkaConsumerWorker>();
builder.Services.AddHostedService<WebhookRetryConsumerWorker>();
builder.Services.AddHostedService<DataCleanupWorker>();
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
