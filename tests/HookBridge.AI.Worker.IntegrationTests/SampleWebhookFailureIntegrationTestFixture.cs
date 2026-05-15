using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using HookBridge.AI.Worker;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Extensions;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services;
using HookBridge.Application.Interfaces;
using HookBridge.Api.Configuration;
using HookBridge.Api.Services.AiNaturalLanguageQuery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Testcontainers.Kafka;
using Testcontainers.MongoDb;

namespace HookBridge.AI.Worker.IntegrationTests;

public sealed class SampleWebhookFailureIntegrationTestFixture : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private KafkaContainer? _kafkaContainer;
    private MongoDbContainer? _mongoContainer;

    private IHost? _host;
    private IMongoDatabase? _database;

    public FakeLocalLlmClient FakeLocalLlmClient { get; } = new();

    public bool IsSkipped => string.Equals(Environment.GetEnvironmentVariable("SKIP_INTEGRATION_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

    public string SkipReason => "Integration tests are disabled because SKIP_INTEGRATION_TESTS=true.";

    public string DatabaseName { get; } = $"hookbridge_ai_integration_{Guid.NewGuid():N}";

    public async Task InitializeAsync()
    {
        if (IsSkipped)
        {
            return;
        }

        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.6.1")
            .Build();
        _mongoContainer = new MongoDbBuilder()
            .WithImage("mongo:7.0")
            .Build();

        await _kafkaContainer.StartAsync();
        await _mongoContainer.StartAsync();
        await CreateTopicsAsync();

        var configuration = BuildConfiguration();
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddConfiguration(configuration);
        builder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Information));
        builder.Services.AddSingleton<ILocalLlmClient>(FakeLocalLlmClient);
        builder.Services.AddSingleton<IKernelFactory, FakeKernelFactory>();
        builder.Services.AddAiOptions(builder.Configuration);
        builder.Services.AddAiRecommendationApprovalServices(builder.Configuration);
        builder.Services.AddAiKafkaOptions(builder.Configuration);
        builder.Services.AddDuplicateReplayDetectionOptions(builder.Configuration);
        builder.Services.AddAiMongoOptions(builder.Configuration);
        builder.Services.AddAiPromptServices();
        builder.Services.AddAiRetryRecommendationServices();
        builder.Services.AddRetryAgentServices(builder.Configuration);
        builder.Services.AddSecurityAgentServices(builder.Configuration);
        builder.Services.AddAiLogSummarizationServices();
        builder.Services.AddEndpointHealthScoringServices();
        builder.Services.AddWebhookFailureAnomalyDetectionServices();
        builder.Services.AddAiSecurityAnalysisServices();
        builder.Services.AddWebhookDuplicateReplayDetectionServices();
        builder.Services.AddAiKafkaServices();
        builder.Services.AddAiMongoServices();
        builder.Services.AddSingleton<IDateTimeProvider>(_ => new FixedDateTimeProvider(DateTime.UtcNow));
        builder.Services.Configure<AiNaturalLanguageQueryOptions>(builder.Configuration.GetSection(AiNaturalLanguageQueryOptions.SectionName));
        builder.Services.AddSingleton<IAiNaturalLanguageQueryPromptBuilder, AiNaturalLanguageQueryPromptBuilder>();
        builder.Services.AddSingleton<IAiNaturalLanguageQueryService, AiNaturalLanguageQueryService>();
        builder.Services.AddHostedService<AiProcessingWorker>();
        builder.Services.AddHostedService<AiSecurityAnalysisWorker>();
        builder.Services.AddHostedService<RetryAgentWorker>();
        builder.Services.AddHostedService<SecurityAgentWorker>();
        builder.Services.AddHostedService<WebhookDuplicateReplayDetectionWorker>();
        builder.Services.AddHostedService<AiAnomalyRecordPersistenceWorker>();

        _host = builder.Build();
        await _host.StartAsync();
        _database = _host.Services.GetRequiredService<IMongoClient>().GetDatabase(DatabaseName);
    }

    public async Task DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(10));
            _host.Dispose();
        }

        if (_mongoContainer is not null)
        {
            await _mongoContainer.DisposeAsync();
        }

        if (_kafkaContainer is not null)
        {
            await _kafkaContainer.DisposeAsync();
        }
    }

    public Task ResetAsync(CancellationToken cancellationToken = default) => CleanMongoAsync(cancellationToken);

    public async Task CleanMongoAsync(CancellationToken cancellationToken)
    {
        if (IsSkipped || _database is null)
        {
            return;
        }

        foreach (var collectionName in new[]
                 {
                     AiMongoOptions.DefaultAiAnalysisResultsCollectionName,
                     AiMongoOptions.DefaultAiAnomalyRecordsCollectionName,
                     AiMongoOptions.DefaultAiSecurityAnalysisResultsCollectionName,
                     AiMongoOptions.DefaultWebhookEventFingerprintsCollectionName,
                     AiMongoOptions.DefaultAiRecommendationApprovalsCollectionName,
                     AiMongoOptions.DefaultPayloadSchemaDetectionResultsCollectionName,
                     AiMongoOptions.DefaultJsonToDtoSuggestionResultsCollectionName,
                     AiMongoOptions.DefaultFluentValidationRuleGenerationResultsCollectionName,
                     AiMongoOptions.DefaultWebhookTransformationRecommendationResultsCollectionName,
                     AiMongoOptions.DefaultCustomerEndpointRiskScoreResultsCollectionName,
                     AiMongoOptions.DefaultWebhookFailureAnomalyDetectionResultsCollectionName,
                     AiMongoOptions.DefaultRetryAgentResultsCollectionName,
                     AiMongoOptions.DefaultSecurityAgentResultsCollectionName
                 })
        {
            await _database.GetCollection<object>(collectionName).DeleteManyAsync(FilterDefinition<object>.Empty, cancellationToken);
        }

        FakeLocalLlmClient.Mode = FakeLocalLlmMode.Success;
    }

    public async Task<string> LoadSampleJsonAsync(string fileName, CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "SampleWebhookFailures", fileName);
        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    public async Task<string> PublishAiAnalysisEventAsync(string sampleFileName, string eventId, CancellationToken cancellationToken)
    {
        var sampleJson = await LoadSampleJsonAsync(sampleFileName, cancellationToken);
        using var document = JsonDocument.Parse(sampleJson);
        var root = document.RootElement;
        var payloadJson = BuildAnalysisPayload(root, eventId);
        var analysisEvent = new AiAnalysisEventDto
        {
            EventId = eventId,
            CorrelationId = $"corr_{eventId}",
            Source = root.GetProperty("source").GetString() ?? "HookBridge.Worker",
            EventType = root.GetProperty("eventType").GetString() ?? "WebhookDeliveryFailed",
            FailureReason = root.TryGetProperty("failureReason", out var failureReason) ? failureReason.GetString() : null,
            Payload = payloadJson,
            CreatedAtUtc = root.TryGetProperty("createdAtUtc", out var createdAt) ? createdAt.GetDateTimeOffset() : DateTimeOffset.UtcNow
        };

        await PublishJsonAsync(AiKafkaTopics.Analysis, analysisEvent.EventId, analysisEvent, cancellationToken);
        return eventId;
    }

    public async Task<string> PublishSecurityAnalysisEventAsync(string sampleFileName, string eventId, CancellationToken cancellationToken)
    {
        var sampleJson = await LoadSampleJsonAsync(sampleFileName, cancellationToken);
        var request = JsonSerializer.Deserialize<AiSecurityAnalysisRequestDto>(sampleJson, JsonOptions)!;
        request.EventId = eventId;
        request.CorrelationId = $"corr_{eventId}";
        request.ReceivedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        await PublishJsonAsync(AiKafkaTopics.SecurityAnalysis, request.EventId, request, cancellationToken);
        return eventId;
    }

    public async Task<string> PublishDuplicateReplayEventAsync(string sampleFileName, string eventId, CancellationToken cancellationToken, bool makeReplay = false)
    {
        var sampleJson = await LoadSampleJsonAsync(sampleFileName, cancellationToken);
        var request = JsonSerializer.Deserialize<WebhookDuplicateReplayDetectionRequestDto>(sampleJson, JsonOptions)!;
        request.EventId = eventId;
        request.CorrelationId = $"corr_{eventId}";
        request.ReceivedAtUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        request.EventTimestampUtc = makeReplay
            ? DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-2), DateTimeKind.Utc)
            : DateTime.SpecifyKind(DateTime.UtcNow.AddSeconds(-10), DateTimeKind.Utc);
        await PublishJsonAsync(AiKafkaTopics.DuplicateReplayDetection, request.EventId, request, cancellationToken);
        return eventId;
    }

    public async Task PublishDuplicateReplayRequestAsync(WebhookDuplicateReplayDetectionRequestDto request, CancellationToken cancellationToken)
        => await PublishJsonAsync(AiKafkaTopics.DuplicateReplayDetection, request.EventId ?? request.CorrelationId ?? Guid.NewGuid().ToString("N"), request, cancellationToken);


    public async Task<string> PublishRetryAgentEventAsync(RetryAgentRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EventId)) request.EventId = $"retry_{Guid.NewGuid():N}";
        request.CorrelationId ??= $"corr_{request.EventId}";
        await PublishJsonAsync(AiKafkaTopics.RetryAgent, request.EventId, request, cancellationToken);
        return request.EventId;
    }

    public async Task<string> PublishSecurityAgentEventAsync(SecurityAgentRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EventId)) request.EventId = $"security_{Guid.NewGuid():N}";
        request.CorrelationId ??= $"corr_{request.EventId}";
        request.ReceivedAtUtc = DateTime.SpecifyKind(request.ReceivedAtUtc == default ? DateTime.UtcNow : request.ReceivedAtUtc, DateTimeKind.Utc);
        await PublishJsonAsync(AiKafkaTopics.SecurityAgent, request.EventId, request, cancellationToken);
        return request.EventId;
    }

    public async Task<AiAnalysisResult?> WaitForMongoResultAsync(string eventId, CancellationToken cancellationToken)
        => await WaitForAsync(async ct => await GetCollection<AiAnalysisResult>(AiMongoOptions.DefaultAiAnalysisResultsCollectionName)
            .Find(result => result.EventId == eventId)
            .FirstOrDefaultAsync(ct), cancellationToken);

    public async Task<AiSecurityAnalysisResult?> WaitForSecurityResultAsync(string eventId, CancellationToken cancellationToken)
        => await WaitForAsync(async ct => await GetCollection<AiSecurityAnalysisResult>(AiMongoOptions.DefaultAiSecurityAnalysisResultsCollectionName)
            .Find(result => result.EventId == eventId)
            .FirstOrDefaultAsync(ct), cancellationToken);

    public async Task<AiAnomalyRecord?> WaitForAnomalyRecordAsync(string eventId, CancellationToken cancellationToken)
        => await WaitForAsync(async ct => await GetCollection<AiAnomalyRecord>(AiMongoOptions.DefaultAiAnomalyRecordsCollectionName)
            .Find(result => result.EventId == eventId)
            .FirstOrDefaultAsync(ct), cancellationToken);

    public async Task<AiRecommendationApproval?> WaitForApprovalAsync(string eventId, CancellationToken cancellationToken)
        => await WaitForAsync(async ct => await GetCollection<AiRecommendationApproval>(AiMongoOptions.DefaultAiRecommendationApprovalsCollectionName)
            .Find(result => result.EventId == eventId)
            .FirstOrDefaultAsync(ct), cancellationToken);

    public async Task<RetryAgentResult?> WaitForRetryAgentResultAsync(string eventId, CancellationToken cancellationToken)
        => await WaitForAsync(async ct => await GetCollection<RetryAgentResult>(AiMongoOptions.DefaultRetryAgentResultsCollectionName)
            .Find(result => result.EventId == eventId)
            .FirstOrDefaultAsync(ct), cancellationToken);

    public async Task<SecurityAgentResult?> WaitForSecurityAgentResultAsync(string eventId, CancellationToken cancellationToken)
        => await WaitForAsync(async ct => await GetCollection<SecurityAgentResult>(AiMongoOptions.DefaultSecurityAgentResultsCollectionName)
            .Find(result => result.EventId == eventId)
            .FirstOrDefaultAsync(ct), cancellationToken);

    public IMongoCollection<T> GetCollection<T>(string collectionName)
        => (_database ?? throw new InvalidOperationException("MongoDB has not been initialized.")).GetCollection<T>(collectionName);

    public T GetRequiredService<T>() where T : notnull
        => (_host ?? throw new InvalidOperationException("Host has not been initialized.")).Services.GetRequiredService<T>();


    private KafkaContainer KafkaContainer
        => _kafkaContainer ?? throw new InvalidOperationException("Kafka container has not been initialized.");

    private MongoDbContainer MongoContainer
        => _mongoContainer ?? throw new InvalidOperationException("MongoDB container has not been initialized.");

    private IConfiguration BuildConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Enabled"] = "true",
                ["AI:Provider"] = "fake",
                ["AI:Model"] = "fake-llm",
                ["AI:Endpoint"] = "http://localhost/fake",
                ["AI:EnableFallback"] = "true",
                ["AI:EnableSecurityAnalysisFallback"] = "true",
                ["AIPrompts:DefaultVersion"] = "v1.0.0",
                ["AiMongo:ConnectionString"] = MongoContainer.GetConnectionString(),
                ["AiMongo:DatabaseName"] = DatabaseName,
                ["AiMongo:AiAnalysisResultsCollectionName"] = AiMongoOptions.DefaultAiAnalysisResultsCollectionName,
                ["AiMongo:PayloadSchemaDetectionResultsCollectionName"] = AiMongoOptions.DefaultPayloadSchemaDetectionResultsCollectionName,
                ["AiMongo:JsonToDtoSuggestionResultsCollectionName"] = AiMongoOptions.DefaultJsonToDtoSuggestionResultsCollectionName,
                ["AiMongo:FluentValidationRuleGenerationResultsCollectionName"] = AiMongoOptions.DefaultFluentValidationRuleGenerationResultsCollectionName,
                ["AiMongo:WebhookTransformationRecommendationResultsCollectionName"] = AiMongoOptions.DefaultWebhookTransformationRecommendationResultsCollectionName,
                ["AiMongo:CustomerEndpointRiskScoreResultsCollectionName"] = AiMongoOptions.DefaultCustomerEndpointRiskScoreResultsCollectionName,
                ["AiMongo:WebhookFailureAnomalyDetectionResultsCollectionName"] = AiMongoOptions.DefaultWebhookFailureAnomalyDetectionResultsCollectionName,
                ["AiMongo:AiAnomalyRecordsCollectionName"] = AiMongoOptions.DefaultAiAnomalyRecordsCollectionName,
                ["AiMongo:AiSecurityAnalysisResultsCollectionName"] = AiMongoOptions.DefaultAiSecurityAnalysisResultsCollectionName,
                ["AiMongo:WebhookEventFingerprintsCollectionName"] = AiMongoOptions.DefaultWebhookEventFingerprintsCollectionName,
                ["AiMongo:AiRecommendationApprovalsCollectionName"] = AiMongoOptions.DefaultAiRecommendationApprovalsCollectionName,
                ["AiMongo:RetryAgentResultsCollectionName"] = AiMongoOptions.DefaultRetryAgentResultsCollectionName,
                ["AiMongo:SecurityAgentResultsCollectionName"] = AiMongoOptions.DefaultSecurityAgentResultsCollectionName,
                ["AiKafka:BootstrapServers"] = KafkaContainer.GetBootstrapAddress(),
                ["AiKafka:SecurityProtocol"] = "Plaintext",
                ["AiKafka:AiAnalysisTopic"] = AiKafkaTopics.Analysis,
                ["AiKafka:PayloadSchemaDetectionTopic"] = AiKafkaTopics.SchemaDetection,
                ["AiKafka:JsonToDtoSuggestionTopic"] = AiKafkaTopics.DtoSuggestion,
                ["AiKafka:FluentValidationRuleGenerationTopic"] = AiKafkaTopics.ValidationRuleGeneration,
                ["AiKafka:WebhookTransformationRecommendationTopic"] = AiKafkaTopics.TransformationRecommendation,
                ["AiKafka:WebhookFailureAnomalyDetectionTopic"] = AiKafkaTopics.FailureAnomalies,
                ["AiKafka:AnomaliesTopic"] = AiKafkaTopics.Anomalies,
                ["AiKafka:SecurityAnalysisTopic"] = AiKafkaTopics.SecurityAnalysis,
                ["AiKafka:DuplicateReplayDetectionTopic"] = AiKafkaTopics.DuplicateReplayDetection,
                ["AiKafka:RetryAgentTopic"] = AiKafkaTopics.RetryAgent,
                ["AiKafka:SecurityAgentTopic"] = AiKafkaTopics.SecurityAgent,
                ["AiKafka:ConsumerGroupId"] = $"hookbridge-ai-integration-{Guid.NewGuid():N}",
                ["AiKafka:EnableAutoCommit"] = "false",
                ["DuplicateReplayDetection:Enabled"] = "true",
                ["DuplicateReplayDetection:ReplayWindowMinutes"] = "15",
                ["DuplicateReplayDetection:FingerprintTtlHours"] = "72",
                ["DuplicateReplayDetection:FutureTimestampToleranceMinutes"] = "5",
                ["DuplicateReplayDetection:HighFrequencyThreshold"] = "5",
                ["DuplicateReplayDetection:HighFrequencyWindowSeconds"] = "60",
                ["DuplicateReplayDetection:HashAlgorithm"] = "SHA256",
                ["AiNaturalLanguageQuery:Enabled"] = "true",
                ["AiNaturalLanguageQuery:DefaultLookbackHours"] = "48",
                ["AiNaturalLanguageQuery:MaxLookbackDays"] = "30",
                ["AiNaturalLanguageQuery:DefaultMaxResults"] = "10",
                ["AiNaturalLanguageQuery:HardMaxResults"] = "25"
            })
            .Build();

    private async Task CreateTopicsAsync()
    {
        using var adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = KafkaContainer.GetBootstrapAddress()
        }).Build();

        var topics = new[]
        {
            AiKafkaTopics.Analysis,
            AiKafkaTopics.Anomalies,
            AiKafkaTopics.SecurityAnalysis,
            AiKafkaTopics.DuplicateReplayDetection,
            AiKafkaTopics.RetryAgent,
            AiKafkaTopics.SecurityAgent
        };

        try
        {
            await adminClient.CreateTopicsAsync(topics.Select(topic => new TopicSpecification
            {
                Name = topic,
                NumPartitions = 1,
                ReplicationFactor = 1
            }));
        }
        catch (CreateTopicsException ex) when (ex.Results.All(result => result.Error.Code is ErrorCode.NoError or ErrorCode.TopicAlreadyExists))
        {
        }
    }

    private async Task PublishJsonAsync<T>(string topic, string key, T value, CancellationToken cancellationToken)
    {
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = KafkaContainer.GetBootstrapAddress(),
            SecurityProtocol = SecurityProtocol.Plaintext
        }).Build();

        await producer.ProduceAsync(topic, new Message<string, string>
        {
            Key = key,
            Value = JsonSerializer.Serialize(value, JsonOptions)
        }, cancellationToken);
        producer.Flush(TimeSpan.FromSeconds(5));
    }

    private static string BuildAnalysisPayload(JsonElement root, string eventId)
    {
        if (root.TryGetProperty("payloadRaw", out var payloadRaw))
        {
            return payloadRaw.GetString() ?? string.Empty;
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("eventId", eventId);
            CopyIfExists(root, writer, "correlationId");
            CopyIfExists(root, writer, "failureReason");
            CopyIfExists(root, writer, "statusCode");
            CopyIfExists(root, writer, "retryCount");
            CopyIfExists(root, writer, "maxRetryCount");
            CopyIfExists(root, writer, "targetUrl");
            CopyIfExists(root, writer, "errorMessage");
            if (root.TryGetProperty("payload", out var payload))
            {
                writer.WritePropertyName("payload");
                payload.WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void CopyIfExists(JsonElement root, Utf8JsonWriter writer, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return;
        }

        writer.WritePropertyName(propertyName);
        value.WriteTo(writer);
    }

    private static async Task<T?> WaitForAsync<T>(Func<CancellationToken, Task<T?>> action, CancellationToken cancellationToken)
        where T : class
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await action(cancellationToken);
            if (result is not null)
            {
                return result;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        return null;
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    }
}

[CollectionDefinition(Name)]
public sealed class SampleWebhookFailureIntegrationTestCollection : ICollectionFixture<SampleWebhookFailureIntegrationTestFixture>
{
    public const string Name = "SampleWebhookFailureIntegrationTests";
}
