using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Messaging;
using HookBridge.Application.Models.Delivery;
using HookBridge.Application.Services;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace HookBridge.Application.Tests;

public sealed class WebhookDeliveryServiceTests
{
    [Fact]
    public async Task ProcessEvent_WithOneActiveSubscriptionSuccess_SetsDeliveredAndStoresAttempt()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();
        fixture.SeedSubscription("sub-1");
        fixture.DeliveryClient.Results.Enqueue(new WebhookDeliveryResult { IsSuccess = true, HttpStatusCode = 200, ResponseBody = "ok", DurationMs = 45 });

        await fixture.Service.ProcessEventAsync(fixture.Message);

        var incoming = (await fixture.IncomingEvents.FindAsync(x => x.EventId == fixture.Message.EventId)).Single();
        Assert.Equal("Delivered", incoming.Status);

        var attempt = (await fixture.Attempts.GetAllAsync()).Single();
        Assert.Equal(DeliveryStatus.Success, attempt.Status);
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Equal("sub-1", attempt.SubscriptionId);
    }

    [Fact]
    public async Task ProcessEvent_WithOneActiveSubscriptionFailure_SetsFailed()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();
        fixture.SeedSubscription("sub-1");
        fixture.DeliveryClient.Results.Enqueue(new WebhookDeliveryResult { IsSuccess = false, HttpStatusCode = 500, ErrorMessage = "boom", DurationMs = 33 });

        await fixture.Service.ProcessEventAsync(fixture.Message);

        var incoming = (await fixture.IncomingEvents.FindAsync(x => x.EventId == fixture.Message.EventId)).Single();
        Assert.Equal("Failed", incoming.Status);

        var attempt = (await fixture.Attempts.GetAllAsync()).Single();
        Assert.Equal(DeliveryStatus.Failed, attempt.Status);
        Assert.Equal(500, attempt.HttpStatusCode);
        Assert.Equal("boom", attempt.ErrorMessage);
    }

    [Fact]
    public async Task ProcessEvent_WithMultipleSubscriptionsAllSuccess_SetsDelivered()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();
        fixture.SeedSubscription("sub-1");
        fixture.SeedSubscription("sub-2", "https://example.com/two");
        fixture.DeliveryClient.Results.Enqueue(new WebhookDeliveryResult { IsSuccess = true, HttpStatusCode = 202, DurationMs = 10 });
        fixture.DeliveryClient.Results.Enqueue(new WebhookDeliveryResult { IsSuccess = true, HttpStatusCode = 204, DurationMs = 12 });

        await fixture.Service.ProcessEventAsync(fixture.Message);

        var incoming = (await fixture.IncomingEvents.FindAsync(x => x.EventId == fixture.Message.EventId)).Single();
        Assert.Equal("Delivered", incoming.Status);
        Assert.Equal(2, (await fixture.Attempts.GetAllAsync()).Count);
    }

    [Fact]
    public async Task ProcessEvent_WithMultipleSubscriptionsPartialFailure_SetsPartiallyFailed()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();
        fixture.SeedSubscription("sub-1");
        fixture.SeedSubscription("sub-2", "https://example.com/two");
        fixture.DeliveryClient.Results.Enqueue(new WebhookDeliveryResult { IsSuccess = true, HttpStatusCode = 200, DurationMs = 10 });
        fixture.DeliveryClient.Results.Enqueue(new WebhookDeliveryResult { IsSuccess = false, HttpStatusCode = 503, ErrorMessage = "unavailable", DurationMs = 14 });

        await fixture.Service.ProcessEventAsync(fixture.Message);

        var incoming = (await fixture.IncomingEvents.FindAsync(x => x.EventId == fixture.Message.EventId)).Single();
        Assert.Equal("PartiallyFailed", incoming.Status);
    }

    [Fact]
    public async Task ProcessEvent_NoMatchingSubscriptions_SetsNoSubscriptions()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();

        await fixture.Service.ProcessEventAsync(fixture.Message);

        var incoming = (await fixture.IncomingEvents.FindAsync(x => x.EventId == fixture.Message.EventId)).Single();
        Assert.Equal("NoSubscriptions", incoming.Status);
        Assert.Empty(await fixture.Attempts.GetAllAsync());
    }

    [Fact]
    public async Task ProcessEvent_MissingIncomingEvent_LogsWarningAndStops()
    {
        var fixture = new Fixture();

        await fixture.Service.ProcessEventAsync(fixture.Message);

        Assert.Contains(fixture.Logger.Records, x =>
            x.Level == LogLevel.Warning &&
            x.Message.Contains("Incoming event not found"));
        Assert.Empty(await fixture.Attempts.GetAllAsync());
    }

    [Fact]
    public async Task ProcessEvent_StoresDeliveryAttemptWithCorrectValues()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();
        fixture.SeedSubscription("sub-1", "https://example.com/orders", timeoutSeconds: 15);
        fixture.DeliveryClient.Results.Enqueue(new WebhookDeliveryResult
        {
            IsSuccess = false,
            HttpStatusCode = 429,
            ResponseBody = "rate limit",
            ErrorMessage = "too many",
            DurationMs = 99,
        });

        await fixture.Service.ProcessEventAsync(fixture.Message);

        var attempt = (await fixture.Attempts.GetAllAsync()).Single();
        Assert.Equal("tenant-1", attempt.TenantId);
        Assert.Equal("evt-1", attempt.EventId);
        Assert.Equal("order.created", attempt.EventType);
        Assert.Equal("sub-1", attempt.SubscriptionId);
        Assert.Equal("https://example.com/orders", attempt.TargetUrl);
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Equal(DeliveryStatus.Failed, attempt.Status);
        Assert.Equal(429, attempt.HttpStatusCode);
        Assert.Equal("rate limit", attempt.ResponseBody);
        Assert.Equal("too many", attempt.ErrorMessage);
        Assert.Equal(99, attempt.DurationMs);
        Assert.Equal(new DateTime(2026, 4, 27, 11, 0, 0, DateTimeKind.Utc), attempt.AttemptedAt);
        Assert.Equal("corr-1", attempt.CorrelationId);

        var sent = fixture.DeliveryClient.Requests.Single();
        Assert.Equal(15, sent.TimeoutSeconds);
        Assert.NotNull(sent.Authentication);
        Assert.Equal("ApiKeyHeader", sent.Authentication!.Type);
        Assert.Single(sent.Headers);
    }

    [Fact]
    public async Task ProcessEvent_FailedDelivery_PublishesWebhookRetryMessage()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();
        fixture.SeedSubscription("sub-1", maxAttempts: 3, initialDelaySeconds: 30, backoffType: "Fixed");
        fixture.DeliveryClient.Results.Enqueue(new WebhookDeliveryResult { IsSuccess = false, HttpStatusCode = 500, ErrorMessage = "boom", DurationMs = 50 });

        await fixture.Service.ProcessEventAsync(fixture.Message);

        var published = Assert.Single(fixture.KafkaProducer.Published);
        Assert.Equal("webhook-retry", published.Topic);
        Assert.Equal("tenant-1", published.Key);
        var retryMessage = Assert.IsType<WebhookRetryMessage>(published.Message);
        Assert.Equal("evt-1", retryMessage.EventId);
        Assert.Equal("tenant-1", retryMessage.TenantId);
        Assert.Equal("sub-1", retryMessage.SubscriptionId);
        Assert.Equal(2, retryMessage.AttemptNumber);
        Assert.Equal(new DateTime(2026, 4, 27, 11, 0, 30, DateTimeKind.Utc), retryMessage.NextRetryAt);
        Assert.Equal("corr-1", retryMessage.CorrelationId);
    }

    [Fact]
    public async Task ProcessEvent_SuccessfulDelivery_DoesNotPublishRetryMessage()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();
        fixture.SeedSubscription("sub-1", maxAttempts: 3, initialDelaySeconds: 30, backoffType: "Fixed");
        fixture.DeliveryClient.Results.Enqueue(new WebhookDeliveryResult { IsSuccess = true, HttpStatusCode = 200, DurationMs = 20 });

        await fixture.Service.ProcessEventAsync(fixture.Message);

        Assert.Empty(fixture.KafkaProducer.Published);
    }

    [Fact]
    public async Task ProcessEvent_RetryPublishFailure_DoesNotThrow()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();
        fixture.SeedSubscription("sub-1", maxAttempts: 3, initialDelaySeconds: 30, backoffType: "Fixed");
        fixture.DeliveryClient.Results.Enqueue(new WebhookDeliveryResult { IsSuccess = false, HttpStatusCode = 500, ErrorMessage = "boom", DurationMs = 50 });
        fixture.KafkaProducer.ThrowOnProduce = true;

        var exception = await Record.ExceptionAsync(() => fixture.Service.ProcessEventAsync(fixture.Message));

        Assert.Null(exception);
        var incoming = (await fixture.IncomingEvents.FindAsync(x => x.EventId == fixture.Message.EventId)).Single();
        Assert.Equal("Failed", incoming.Status);
        var attempt = (await fixture.Attempts.GetAllAsync()).Single();
        Assert.Equal(DeliveryStatus.Failed, attempt.Status);
    }

    private sealed class Fixture
    {
        public InMemoryRepository<IncomingEvent> IncomingEvents { get; } = new();
        public InMemoryRepository<Subscription> Subscriptions { get; } = new();
        public InMemoryRepository<DeliveryAttempt> Attempts { get; } = new();
        public FakeWebhookDeliveryClient DeliveryClient { get; } = new();
        public FakeKafkaProducer KafkaProducer { get; } = new();
        public ListLogger<WebhookDeliveryService> Logger { get; } = new();
        public WebhookEventMessage Message { get; } = new()
        {
            TenantId = "tenant-1",
            EventId = "evt-1",
            EventType = "order.created",
            CorrelationId = "corr-1",
            ReceivedAt = new DateTime(2026, 4, 27, 10, 0, 0, DateTimeKind.Utc),
        };

        public WebhookDeliveryService Service => new(
            IncomingEvents,
            Subscriptions,
            Attempts,
            new FixedDateTimeProvider(),
            DeliveryClient,
            KafkaProducer,
            new RetryPolicyService(),
            Logger);

        public void SeedIncomingEvent()
        {
            IncomingEvents.AddAsync(new IncomingEvent
            {
                Id = "incoming-1",
                TenantId = "tenant-1",
                EventId = "evt-1",
                EventType = "order.created",
                Payload = new { orderId = "1001" },
                Status = "Accepted",
                ReceivedAt = new DateTime(2026, 4, 27, 10, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 4, 27, 10, 0, 0, DateTimeKind.Utc),
            }).GetAwaiter().GetResult();
        }

        public void SeedSubscription(
            string id,
            string url = "https://example.com/orders",
            int timeoutSeconds = 30,
            int maxAttempts = 3,
            int initialDelaySeconds = 30,
            string backoffType = "Exponential")
        {
            Subscriptions.AddAsync(new Subscription
            {
                Id = id,
                TenantId = "tenant-1",
                EventType = "order.created",
                TargetUrl = url,
                IsActive = true,
                TimeoutSeconds = timeoutSeconds,
                RetryPolicy = new RetryPolicy
                {
                    MaxAttempts = maxAttempts,
                    InitialDelaySeconds = initialDelaySeconds,
                    BackoffType = backoffType,
                },
                Headers = [new KeyValueItem { Name = "x-test", Value = "abc" }],
                Authentication = new AuthenticationConfig
                {
                    Type = "ApiKeyHeader",
                    ApiKeyHeader = new ApiKeyHeaderConfig
                    {
                        HeaderName = "x-api-key",
                        HeaderValue = "secret",
                    },
                },
                CreatedAt = new DateTime(2026, 4, 27, 9, 0, 0, DateTimeKind.Utc),
            }).GetAwaiter().GetResult();
        }
    }

    private sealed class FakeKafkaProducer : IKafkaProducer
    {
        public List<PublishedMessage> Published { get; } = [];
        public bool ThrowOnProduce { get; set; }

        public Task ProduceAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default)
        {
            if (ThrowOnProduce)
            {
                throw new InvalidOperationException("Kafka unavailable");
            }

            Published.Add(new PublishedMessage(topic, key, message!));
            return Task.CompletedTask;
        }
    }

    private sealed record PublishedMessage(string Topic, string Key, object Message);

    private sealed class FixedDateTimeProvider : HookBridge.Application.Interfaces.IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 4, 27, 11, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FakeWebhookDeliveryClient : IWebhookDeliveryClient
    {
        public Queue<WebhookDeliveryResult> Results { get; } = new();
        public List<WebhookDeliveryRequest> Requests { get; } = [];

        public Task<WebhookDeliveryResult> SendAsync(WebhookDeliveryRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : new WebhookDeliveryResult { IsSuccess = true, HttpStatusCode = 200, DurationMs = 1 });
        }
    }

    private sealed class InMemoryRepository<T> : IMongoRepository<T>
        where T : BaseEntity
    {
        private readonly List<T> _items = [];

        public Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<T>> FindAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return Task.FromResult<IReadOnlyList<T>>(_items.Where(compiled).ToList());
        }

        public Task<T?> FirstOrDefaultAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return Task.FromResult(_items.FirstOrDefault(compiled));
        }

        public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<T>>(_items.ToList());

        public Task AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            _items.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            var index = _items.FindIndex(x => x.Id == entity.Id);
            if (index >= 0)
            {
                _items[index] = entity;
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<LogRecord> Records { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Records.Add(new LogRecord(logLevel, formatter(state, exception), exception));
        }

        public sealed record LogRecord(LogLevel Level, string Message, Exception? Exception);

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
