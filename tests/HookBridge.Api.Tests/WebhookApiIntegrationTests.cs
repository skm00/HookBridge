using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using HookBridge.AI.Worker.Mongo;
using HookBridge.Application.DTOs.AiAnalysis;
using HookBridge.Application.DTOs.AiDashboard;
using HookBridge.Api.Health;
using HookBridge.Application.DTOs.ApiKeys;
using HookBridge.Application.DTOs.Common;
using HookBridge.Application.DTOs.Events;
using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Messaging;
using HookBridge.Domain.Enums;
using HookBridge.Shared.Api;
using HookBridge.Shared.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;

namespace HookBridge.Api.Tests;

public sealed class WebhookApiIntegrationTests : IClassFixture<WebhookApiIntegrationTests.HookBridgeApiFactory>
{
    private readonly HookBridgeApiFactory _factory;
    private readonly HttpClient _client;

    public WebhookApiIntegrationTests(HookBridgeApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.State.Reset();
    }

    [Fact]
    public async Task CreateSubscriptionApi_WhenRequestIsValid_ShouldPersistSubscriptionAndReturnCreated()
    {
        var request = IntegrationTestData.CreateSubscriptionRequest();

        var response = await _client.PostAsJsonAsync("/api/v1/admin/subscriptions", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SubscriptionResponseDto>>();
        body!.Success.Should().BeTrue();
        body.Data!.EventType.Should().Be("order.created");
        _factory.State.Subscriptions.Should().ContainSingle(x => x.TargetUrl == request.TargetUrl);
    }

    [Fact]
    public async Task CreateSubscriptionApi_WhenRequestIsInvalid_ShouldReturnBadRequest()
    {
        var request = IntegrationTestData.CreateSubscriptionRequest(targetUrl: "not-a-url");
        _factory.State.SubscriptionServiceShouldRejectInvalidRequests = true;

        var response = await _client.PostAsJsonAsync("/api/v1/admin/subscriptions", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _factory.State.Subscriptions.Should().BeEmpty();
    }

    [Fact]
    public async Task SendWebhookEventApi_WhenApiKeyIsValid_ShouldPublishKafkaEventAndReturnAccepted()
    {
        var request = IntegrationTestData.EventRequest();

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/events/tenant-1")
        {
            Content = JsonContent.Create(request),
        };
        httpRequest.Headers.Add("x-api-key", "hb_live_valid");
        httpRequest.Headers.Add("x-correlation-id", "corr-integration");
        var response = await _client.SendAsync(httpRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<EventIngestionResponseDto>>();
        body!.Data!.Status.Should().Be("Accepted");
        _factory.State.IncomingEvents.Should().ContainSingle(x => x.EventId == request.EventId);
        _factory.State.PublishedMessages.Should().ContainSingle(x => x.Topic == KafkaTopics.WebhookEvents);
    }

    [Fact]
    public async Task SendWebhookEventApi_WhenApiKeyIsMissing_ShouldReturnUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/events/tenant-1", IntegrationTestData.EventRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _factory.State.IncomingEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task FailedWebhookRetryFlow_WhenDeliveryFailsBeforeMaxRetry_ShouldPublishRetryMessage()
    {
        await SendAcceptedEventAsync("evt_retry_1");

        _factory.State.RecordDeliveryFailure(eventId: "evt_retry_1", subscriptionId: "sub-1", attemptNumber: 1, maxAttempts: 3);

        _factory.State.PublishedMessages.Should().Contain(x => x.Topic == KafkaTopics.WebhookRetry);
        _factory.State.FailedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task DlqFlow_WhenMaxRetryIsReached_ShouldPersistFailedEventAndPublishDlqMessage()
    {
        await SendAcceptedEventAsync("evt_dlq_1");

        _factory.State.RecordDeliveryFailure(eventId: "evt_dlq_1", subscriptionId: "sub-1", attemptNumber: 3, maxAttempts: 3);

        _factory.State.PublishedMessages.Should().Contain(x => x.Topic == KafkaTopics.WebhookDlq);
        _factory.State.FailedEvents.Should().ContainSingle(x => x.EventId == "evt_dlq_1" && x.Status == "DLQ");
    }

    [Fact]
    public async Task MongoDbPersistence_WhenSubscriptionIsCreated_ShouldBeReadableFromTestStore()
    {
        var request = IntegrationTestData.CreateSubscriptionRequest(eventType: "invoice.paid");

        await _client.PostAsJsonAsync("/api/v1/admin/subscriptions", request);

        _factory.State.Subscriptions.Should().ContainSingle(x => x.EventType == "invoice.paid" && x.TenantId == "tenant-1");
    }

    [Fact]
    public async Task KafkaPublishConsumeFlow_WhenEventIsPublished_ShouldBeConsumableFromFakeKafka()
    {
        await SendAcceptedEventAsync("evt_kafka_1");

        using var scope = _factory.Services.CreateScope();
        var consumer = scope.ServiceProvider.GetRequiredService<IKafkaConsumer>();
        var consumed = new List<EventIngestionRequestDto>();
        await foreach (var item in consumer.ConsumeAsync<EventIngestionRequestDto>(KafkaTopics.WebhookEvents, "integration-tests"))
        {
            consumed.Add(item);
            break;
        }

        consumed.Should().ContainSingle(x => x.EventId == "evt_kafka_1");
    }

    [Fact]
    public async Task GetAiAnalysisByEventId_WhenResultExists_ShouldReturnSuccessfulJsonResponseShape()
    {
        _factory.State.AiAnalysisResults.Add(new AiAnalysisResult
        {
            Id = "663f0c7a9f1e2a5a12345678",
            EventId = "evt_ai_integration",
            CorrelationId = "corr_ai",
            Source = "HookBridge.Worker",
            EventType = "WebhookDeliveryFailed",
            FailureReason = "Too Many Requests",
            AiSummary = "The target endpoint is rate limiting requests.",
            RootCause = "HTTP 429 indicates rate limiting.",
            AiRecommendation = "Retry using exponential backoff.",
            RiskLevel = "Medium",
            ConfidenceScore = 0.86,
            SuggestedRetryAction = "RetryWithBackoff",
            IsRetryRecommended = true,
            Model = "llama3",
            Provider = "Ollama",
            CreatedAtUtc = new DateTime(2026, 5, 13, 10, 30, 0, DateTimeKind.Utc),
        });

        var response = await _client.GetAsync("/api/ai-analysis/events/evt_ai_integration");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AiAnalysisResultResponseDto>>();
        body!.Success.Should().BeTrue();
        body.Data!.Id.Should().Be("663f0c7a9f1e2a5a12345678");
        body.Data.EventId.Should().Be("evt_ai_integration");
        body.Data.CorrelationId.Should().Be("corr_ai");
        body.Data.Source.Should().Be("HookBridge.Worker");
        body.Data.EventType.Should().Be("WebhookDeliveryFailed");
        body.Data.FailureReason.Should().Be("Too Many Requests");
        body.Data.AiSummary.Should().Be("The target endpoint is rate limiting requests.");
        body.Data.RootCause.Should().Be("HTTP 429 indicates rate limiting.");
        body.Data.AiRecommendation.Should().Be("Retry using exponential backoff.");
        body.Data.RiskLevel.Should().Be("Medium");
        body.Data.ConfidenceScore.Should().Be(0.86);
        body.Data.SuggestedRetryAction.Should().Be("RetryWithBackoff");
        body.Data.IsRetryRecommended.Should().BeTrue();
        body.Data.Model.Should().Be("llama3");
        body.Data.Provider.Should().Be("Ollama");
        body.Data.CreatedAtUtc.Should().Be(new DateTime(2026, 5, 13, 10, 30, 0, DateTimeKind.Utc));
    }


    [Fact]
    public async Task GetAiDashboardSummary_WhenCalled_ShouldReturnSuccessfulJsonResponseShape()
    {
        _factory.State.AiAnalysisResults.Add(new AiAnalysisResult
        {
            Id = "663f0c7a9f1e2a5a12345679",
            EventId = "evt_dashboard",
            Environment = "qa",
            CustomerId = "cust_123",
            EventType = "WebhookDeliveryFailed",
            AiSummary = "Endpoint is rate limited.",
            RootCause = "HTTP 429 spike.",
            RiskLevel = "High",
            ConfidenceScore = 0.82,
            SuggestedRetryAction = "RetryWithBackoff",
            IsRetryRecommended = true,
            CreatedAtUtc = new DateTime(2026, 5, 14, 10, 0, 0, DateTimeKind.Utc),
        });

        var response = await _client.GetAsync("/api/ai-dashboard/summary?fromUtc=2026-05-14T00:00:00Z&toUtc=2026-05-14T23:59:59Z");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AiDashboardSummaryResponseDto>>();
        body!.Success.Should().BeTrue();
        body.Data!.TotalAiAnalyses.Should().Be(1);
        body.Data.RiskDistribution.High.Should().Be(1);
        body.Data.RetryActionDistribution.Should().ContainSingle(x => x.Name == "RetryWithBackoff" && x.Count == 1);
        body.Data.RecentFindings.Should().ContainSingle(x => x.EventId == "evt_dashboard");
    }

    [Fact]
    public async Task GetAiDashboardSummary_WithEnvironmentFilter_ShouldReturnFilteredResults()
    {
        _factory.State.AiAnalysisResults.Add(new AiAnalysisResult { Id = "663f0c7a9f1e2a5a12345680", EventId = "evt_qa", Environment = "qa", RiskLevel = "Low", SuggestedRetryAction = "None", CreatedAtUtc = new DateTime(2026, 5, 14, 10, 0, 0, DateTimeKind.Utc) });
        _factory.State.AiAnalysisResults.Add(new AiAnalysisResult { Id = "663f0c7a9f1e2a5a12345681", EventId = "evt_prod", Environment = "prod", RiskLevel = "Low", SuggestedRetryAction = "None", CreatedAtUtc = new DateTime(2026, 5, 14, 10, 0, 0, DateTimeKind.Utc) });

        var response = await _client.GetAsync("/api/ai-dashboard/summary?environment=qa&fromUtc=2026-05-14T00:00:00Z&toUtc=2026-05-14T23:59:59Z");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AiDashboardSummaryResponseDto>>();
        body!.Data!.Environment.Should().Be("qa");
        body.Data.TotalAiAnalyses.Should().Be(1);
    }

    [Fact]
    public async Task GetAiDashboardSummary_WithCustomerIdFilter_ShouldReturnFilteredResults()
    {
        _factory.State.AiAnalysisResults.Add(new AiAnalysisResult { Id = "663f0c7a9f1e2a5a12345682", EventId = "evt_cust", CustomerId = "cust_123", RiskLevel = "Low", SuggestedRetryAction = "None", CreatedAtUtc = new DateTime(2026, 5, 14, 10, 0, 0, DateTimeKind.Utc) });
        _factory.State.AiAnalysisResults.Add(new AiAnalysisResult { Id = "663f0c7a9f1e2a5a12345683", EventId = "evt_other", CustomerId = "cust_other", RiskLevel = "Low", SuggestedRetryAction = "None", CreatedAtUtc = new DateTime(2026, 5, 14, 10, 0, 0, DateTimeKind.Utc) });

        var response = await _client.GetAsync("/api/ai-dashboard/summary?customerId=cust_123&fromUtc=2026-05-14T00:00:00Z&toUtc=2026-05-14T23:59:59Z");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AiDashboardSummaryResponseDto>>();
        body!.Data!.CustomerId.Should().Be("cust_123");
        body.Data.TotalAiAnalyses.Should().Be(1);
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/v1/health/kafka")]
    [InlineData("/api/v1/health/mongodb")]
    [InlineData("/api/v1/health/worker")]
    public async Task HealthCheckEndpoints_WhenCalled_ShouldReturnOk(string path)
    {
        var response = await _client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task SendAcceptedEventAsync(string eventId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/events/tenant-1")
        {
            Content = JsonContent.Create(IntegrationTestData.EventRequest(eventId)),
        };
        request.Headers.Add("x-api-key", "hb_live_valid");
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public sealed class HookBridgeApiFactory : WebApplicationFactory<Program>
    {
        public IntegrationTestState State { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MongoDb:ConnectionString"] = "mongodb://localhost:27017",
                    ["MongoDb:DatabaseName"] = "hookbridge-tests",
                    ["Kafka:BootstrapServers"] = "localhost:9092",
                    ["Kafka:MessageTimeoutMs"] = "1000",
                    ["Jwt:Issuer"] = "HookBridge.Tests",
                    ["Jwt:Audience"] = "HookBridge.Tests",
                    ["Jwt:Secret"] = "0123456789abcdef0123456789abcdef",
                    ["Jwt:ExpiryMinutes"] = "60",
                    ["Stripe:SuccessUrl"] = "https://billing.example.com/success",
                    ["Stripe:CancelUrl"] = "https://billing.example.com/cancel",
                    ["Elastic:ServiceName"] = "hookbridge-api-tests",
                    ["Elastic:Environment"] = "test",
                    ["DataRetention:IncomingEventsDays"] = "30",
                    ["DataRetention:DeliveryLogsDays"] = "30",
                    ["DataRetention:FailedEventsDays"] = "30",
                    ["DataRetention:AuditLogsDays"] = "30",
                    ["DataRetention:NotificationsDays"] = "30",
                    ["DemoData:Enabled"] = "false",
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<ISubscriptionService>();
                services.RemoveAll<IEventIngestionService>();
                services.RemoveAll<IApiKeyService>();
                services.RemoveAll<IKafkaProducer>();
                services.RemoveAll<IKafkaConsumer>();
                services.RemoveAll<IKafkaAdminService>();
                services.RemoveAll<ICurrentUserContext>();
                services.RemoveAll<IMongoDatabase>();
                services.RemoveAll<IElasticsearchHealthService>();
                services.RemoveAll<IAiAnalysisResultRepository>();
                services.RemoveAll<IAiAnomalyRecordRepository>();
                services.RemoveAll<IAiSecurityAnalysisRepository>();
                services.RemoveAll<ICustomerEndpointRiskScoreRepository>();

                services.AddSingleton(State);
                services.AddScoped<ISubscriptionService, InMemorySubscriptionService>();
                services.AddScoped<IEventIngestionService, RecordingEventIngestionService>();
                services.AddScoped<IApiKeyService, AcceptingApiKeyService>();
                services.AddSingleton<IKafkaProducer, InMemoryKafkaBus>();
                services.AddSingleton<IKafkaConsumer>(sp => (InMemoryKafkaBus)sp.GetRequiredService<IKafkaProducer>());
                services.AddSingleton<IKafkaAdminService, HealthyKafkaAdminService>();
                services.AddScoped<ICurrentUserContext, TestCurrentUserContext>();
                services.AddSingleton<IElasticsearchHealthService, HealthyElasticsearchHealthService>();
                services.AddScoped<IAiAnalysisResultRepository, InMemoryAiAnalysisResultRepository>();
                services.AddScoped<IAiAnomalyRecordRepository, EmptyAiAnomalyRecordRepository>();
                services.AddScoped<IAiSecurityAnalysisRepository, EmptyAiSecurityAnalysisRepository>();
                services.AddScoped<ICustomerEndpointRiskScoreRepository, EmptyCustomerEndpointRiskScoreRepository>();

                var mongoDatabase = new Mock<IMongoDatabase>();
                mongoDatabase
                    .Setup(x => x.RunCommandAsync<BsonDocument>(It.IsAny<Command<BsonDocument>>(), null, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new BsonDocument("ok", 1));
                services.AddSingleton(mongoDatabase.Object);

                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
            });
        }
    }

    private static class IntegrationTestData
    {
        public static CreateSubscriptionRequestDto CreateSubscriptionRequest(string targetUrl = "https://webhooks.example.com/orders", string eventType = "order.created") => new()
        {
            EventType = eventType,
            TargetUrl = targetUrl,
            Headers = [new KeyValueDto { Name = "x-test", Value = "integration" }],
            RetryPolicy = new RetryPolicyDto { MaxAttempts = 3, InitialDelaySeconds = 10, BackoffType = "Exponential" },
            TimeoutSeconds = 30,
        };

        public static EventIngestionRequestDto EventRequest(string eventId = "evt_integration_1") => new()
        {
            EventType = "order.created",
            EventId = eventId,
            Data = new { orderId = "ord_123" },
        };
    }

    public sealed class IntegrationTestState
    {
        public List<StoredSubscription> Subscriptions { get; } = [];
        public List<EventIngestionRequestDto> IncomingEvents { get; } = [];
        public List<FailedEventRecord> FailedEvents { get; } = [];
        public List<PublishedMessage> PublishedMessages { get; } = [];
        public List<AiAnalysisResult> AiAnalysisResults { get; } = [];
        public bool SubscriptionServiceShouldRejectInvalidRequests { get; set; }

        public void Reset()
        {
            Subscriptions.Clear();
            IncomingEvents.Clear();
            FailedEvents.Clear();
            PublishedMessages.Clear();
            AiAnalysisResults.Clear();
            SubscriptionServiceShouldRejectInvalidRequests = false;
        }

        public void RecordDeliveryFailure(string eventId, string subscriptionId, int attemptNumber, int maxAttempts)
        {
            if (attemptNumber < maxAttempts)
            {
                PublishedMessages.Add(new PublishedMessage(KafkaTopics.WebhookRetry, "tenant-1", new { eventId, subscriptionId, attemptNumber = attemptNumber + 1 }));
                return;
            }

            FailedEvents.Add(new FailedEventRecord(eventId, subscriptionId, "DLQ"));
            PublishedMessages.Add(new PublishedMessage(KafkaTopics.WebhookDlq, "tenant-1", new { eventId, subscriptionId, finalAttemptNumber = attemptNumber }));
        }
    }

    public sealed record StoredSubscription(string Id, string TenantId, string EventType, string TargetUrl);
    public sealed record FailedEventRecord(string EventId, string SubscriptionId, string Status);
    public sealed record PublishedMessage(string Topic, string Key, object Message);

    private sealed class InMemoryAiAnalysisResultRepository(IntegrationTestState state) : IAiAnalysisResultRepository
    {
        public Task InsertAsync(AiAnalysisResult result, CancellationToken cancellationToken = default)
        {
            state.AiAnalysisResults.Add(result);
            return Task.CompletedTask;
        }

        public Task<AiAnalysisResult?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(state.AiAnalysisResults.FirstOrDefault(x => x.Id == id));

        public Task<AiAnalysisResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default)
            => Task.FromResult(state.AiAnalysisResults.FirstOrDefault(x => x.EventId == eventId));

        public Task<IReadOnlyList<AiAnalysisResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AiAnalysisResult>>(state.AiAnalysisResults.Where(x => x.CorrelationId == correlationId).ToList());

        public Task<IReadOnlyList<AiAnalysisResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AiAnalysisResult>>(state.AiAnalysisResults.OrderByDescending(x => x.CreatedAtUtc).Take(limit).ToList());

        public Task<long> CountByDateRangeAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default)
            => Task.FromResult((long)ApplyFilter(filter).Count());

        public Task<IReadOnlyDictionary<string, long>> CountByRiskLevelAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, long>>(ApplyFilter(filter).GroupBy(x => string.IsNullOrWhiteSpace(x.RiskLevel) ? "Unknown" : x.RiskLevel).ToDictionary(x => x.Key, x => (long)x.Count()));

        public Task<IReadOnlyDictionary<string, long>> CountByRetryActionAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, long>>(ApplyFilter(filter).GroupBy(x => string.IsNullOrWhiteSpace(x.SuggestedRetryAction) ? "Unknown" : x.SuggestedRetryAction).ToDictionary(x => x.Key, x => (long)x.Count()));

        public Task<double> GetAverageConfidenceScoreAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default)
        {
            var results = ApplyFilter(filter).ToList();
            return Task.FromResult(results.Count == 0 ? 0 : results.Average(x => x.ConfidenceScore));
        }

        public Task<IReadOnlyList<AiDashboardRecentFindingResult>> GetRecentFindingsAsync(AiDashboardQueryFilter filter, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AiDashboardRecentFindingResult>>(ApplyFilter(filter)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(limit)
                .Select(x => new AiDashboardRecentFindingResult
                {
                    Id = x.Id,
                    EventId = x.EventId,
                    CorrelationId = x.CorrelationId,
                    CustomerId = x.CustomerId,
                    SubscriptionId = x.SubscriptionId,
                    EndpointId = x.EndpointId,
                    FindingType = "Analysis",
                    Title = string.IsNullOrWhiteSpace(x.RootCause) ? "AI analysis completed" : x.RootCause,
                    Summary = x.AiSummary,
                    RiskLevel = x.RiskLevel,
                    SuggestedAction = x.SuggestedRetryAction,
                    CreatedAtUtc = x.CreatedAtUtc
                })
                .ToList());

        private IEnumerable<AiAnalysisResult> ApplyFilter(AiDashboardQueryFilter filter)
            => state.AiAnalysisResults.Where(x =>
                x.CreatedAtUtc >= filter.FromUtc && x.CreatedAtUtc < filter.ToUtc &&
                (string.IsNullOrWhiteSpace(filter.Environment) || x.Environment == filter.Environment) &&
                (string.IsNullOrWhiteSpace(filter.CustomerId) || x.CustomerId == filter.CustomerId) &&
                (string.IsNullOrWhiteSpace(filter.CustomerIdType) || x.CustomerIdType == filter.CustomerIdType) &&
                (string.IsNullOrWhiteSpace(filter.SubscriptionId) || x.SubscriptionId == filter.SubscriptionId) &&
                (string.IsNullOrWhiteSpace(filter.EndpointId) || x.EndpointId == filter.EndpointId) &&
                (string.IsNullOrWhiteSpace(filter.EventType) || x.EventType == filter.EventType));
    }

    private sealed class EmptyAiAnomalyRecordRepository : IAiAnomalyRecordRepository
    {
        public Task<AiAnomalyRecordRepositoryResult> InsertAsync(AiAnomalyRecord record, CancellationToken cancellationToken = default) => Task.FromResult(AiAnomalyRecordRepositoryResult.Success(record));
        public Task<AiAnomalyRecord?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<AiAnomalyRecord?>(null);
        public Task<AiAnomalyRecord?> GetByAnomalyIdAsync(string anomalyId, CancellationToken cancellationToken = default) => Task.FromResult<AiAnomalyRecord?>(null);
        public Task<IReadOnlyList<AiAnomalyRecord>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>([]);
        public Task<IReadOnlyList<AiAnomalyRecord>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>([]);
        public Task<IReadOnlyList<AiAnomalyRecord>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>([]);
        public Task<IReadOnlyList<AiAnomalyRecord>> GetBySubscriptionIdAsync(string subscriptionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>([]);
        public Task<IReadOnlyList<AiAnomalyRecord>> GetByEndpointIdAsync(string endpointId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>([]);
        public Task<IReadOnlyList<AiAnomalyRecord>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>([]);
        public Task<IReadOnlyList<AiAnomalyRecord>> SearchAsync(HookBridge.AI.Worker.DTOs.AiAnomalyRecordSearchRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiAnomalyRecord>>([]);
    }

    private sealed class EmptyAiSecurityAnalysisRepository : IAiSecurityAnalysisRepository
    {
        public Task InsertAsync(AiSecurityAnalysisResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AiSecurityAnalysisResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default) => Task.FromResult<AiSecurityAnalysisResult?>(null);
        public Task<IReadOnlyList<AiSecurityAnalysisResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiSecurityAnalysisResult>>([]);
        public Task<IReadOnlyList<AiSecurityAnalysisResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiSecurityAnalysisResult>>([]);
        public Task<IReadOnlyList<AiSecurityAnalysisResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiSecurityAnalysisResult>>([]);
        public Task<IReadOnlyList<AiSecurityAnalysisResult>> SearchAsync(HookBridge.AI.Worker.DTOs.AiSecurityAnalysisSearchRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AiSecurityAnalysisResult>>([]);
    }

    private sealed class EmptyCustomerEndpointRiskScoreRepository : ICustomerEndpointRiskScoreRepository
    {
        public Task InsertAsync(CustomerEndpointRiskScoreResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerEndpointRiskScoreResult>>([]);
        public Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetBySubscriptionIdAsync(string subscriptionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerEndpointRiskScoreResult>>([]);
        public Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetByEndpointIdAsync(string endpointId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerEndpointRiskScoreResult>>([]);
        public Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerEndpointRiskScoreResult>>([]);
    }

    private sealed class InMemorySubscriptionService(IntegrationTestState state) : ISubscriptionService
    {
        public Task<SubscriptionResponseDto> CreateAsync(string tenantId, CreateSubscriptionRequestDto request, CancellationToken cancellationToken = default)
        {
            if (state.SubscriptionServiceShouldRejectInvalidRequests || !Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out _))
            {
                throw new FluentValidation.ValidationException("TargetUrl must be an absolute HTTP or HTTPS URL.");
            }

            var id = $"sub-{state.Subscriptions.Count + 1}";
            state.Subscriptions.Add(new StoredSubscription(id, tenantId, request.EventType ?? "*", request.TargetUrl));
            return Task.FromResult(new SubscriptionResponseDto
            {
                Id = id,
                EventType = request.EventType,
                TargetUrl = request.TargetUrl,
                Headers = request.Headers,
                RetryPolicy = request.RetryPolicy!,
                TimeoutSeconds = request.TimeoutSeconds ?? 30,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
        }

        public Task<SubscriptionResponseDto?> GetByIdAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.FromResult<SubscriptionResponseDto?>(null);
        public Task<PagedResponseDto<SubscriptionResponseDto>> SearchAsync(SubscriptionSearchRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult(PagedResponseDto<SubscriptionResponseDto>.Create([], 1, 50, 0));
        public Task<SubscriptionResponseDto?> UpdateAsync(string tenantId, string id, UpdateSubscriptionRequestDto request, CancellationToken cancellationToken = default) => Task.FromResult<SubscriptionResponseDto?>(null);
        public Task<bool> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> EnableAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> DisableAsync(string tenantId, string id, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class RecordingEventIngestionService(IntegrationTestState state, IKafkaProducer kafkaProducer) : IEventIngestionService
    {
        public async Task<EventIngestionResponseDto> IngestAsync(string tenantId, string apiKey, EventIngestionRequestDto request, string? correlationId, CancellationToken cancellationToken = default)
        {
            state.IncomingEvents.Add(request);
            await kafkaProducer.ProduceAsync(KafkaTopics.WebhookEvents, tenantId, request, cancellationToken);
            return new EventIngestionResponseDto { EventId = request.EventId ?? "generated-event-id", Status = "Accepted", Message = "Event accepted." };
        }
    }

    private sealed class AcceptingApiKeyService : IApiKeyService
    {
        public Task<ApiKeyValidationResult> ValidateAsync(string tenantId, string plainApiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new ApiKeyValidationResult { IsValid = plainApiKey == "hb_live_valid", TenantId = tenantId, ApiKeyId = "key-1" });

        public Task<CreateApiKeyResponseDto> CreateAsync(string tenantId, HookBridge.Application.DTOs.ApiKeys.CreateApiKeyRequestDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ApiKeyResponseDto>> GetByTenantAsync(string tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ApiKeyResponseDto?> UpdateAsync(string tenantId, string keyId, HookBridge.Application.DTOs.ApiKeys.UpdateApiKeyRequestDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RevokeAsync(string tenantId, string keyId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class InMemoryKafkaBus(IntegrationTestState state) : IKafkaProducer, IKafkaConsumer
    {
        public Task ProduceAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default)
        {
            state.PublishedMessages.Add(new PublishedMessage(topic, key, message!));
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<T> ConsumeAsync<T>(string topic, string groupId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var message in state.PublishedMessages.Where(x => x.Topic == topic).Select(x => x.Message).OfType<T>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return message;
                await Task.Yield();
            }
        }
    }

    private sealed class HealthyKafkaAdminService : IKafkaAdminService
    {
        public Task EnsureTopicsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class HealthyElasticsearchHealthService : IElasticsearchHealthService
    {
        public Task<ElasticsearchHealthResponse> CheckHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ElasticsearchHealthResponse { IsHealthy = true, Message = "Healthy." });
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public string? UserId => "user-1";
        public string? TenantId => "tenant-1";
        public string? Email => "owner@example.com";
        public string? Role => AdminRole.Owner.ToString();
        public bool IsAuthenticated => true;
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim("tenant_id", "tenant-1"),
                new Claim("role", AdminRole.Owner.ToString()),
                new Claim(ClaimTypes.Role, AdminRole.Owner.ToString()),
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
