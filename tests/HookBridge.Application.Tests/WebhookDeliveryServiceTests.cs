using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Messaging;
using HookBridge.Application.Models.Delivery;
using HookBridge.Application.DTOs.FailedEvents;
using HookBridge.Application.Services;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.Extensions.Logging;
using Xunit;

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
        Assert.Equal(1, fixture.UsageService.DeliveredIncrements);
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
    public async Task ResponseBody_IsTruncatedBeforeStorage()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();
        fixture.SeedSubscription("sub-1");
        var largeBody = new string('x', HookBridge.Shared.Constants.ValidationLimits.MaxResponseBodyStoredLength + 250);
        fixture.DeliveryClient.Results.Enqueue(new WebhookDeliveryResult
        {
            IsSuccess = false,
            HttpStatusCode = 500,
            ResponseBody = largeBody,
            ErrorMessage = "too large",
            DurationMs = 18,
        });

        await fixture.Service.ProcessEventAsync(fixture.Message);

        var attempt = (await fixture.Attempts.GetAllAsync()).Single();
        Assert.Equal(HookBridge.Shared.Constants.ValidationLimits.MaxResponseBodyStoredLength, attempt.ResponseBody!.Length);
        Assert.True(attempt.ResponseBodyTruncated);
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

    [Fact]
    public async Task ProcessRetryAsync_FailedRetryWithNoAttemptsRemaining_CreatesFailedEvent()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();
        fixture.SeedSubscription("sub-1", maxAttempts: 2, initialDelaySeconds: 30, backoffType: "Fixed");
        fixture.DeliveryClient.Results.Enqueue(new WebhookDeliveryResult { IsSuccess = false, HttpStatusCode = 500, ErrorMessage = "boom", DurationMs = 44 });

        await fixture.Service.ProcessRetryAsync(fixture.RetryMessage);

        var failedEvent = Assert.Single(fixture.FailedEventService.CreatedEvents);
        Assert.Equal("tenant-1", failedEvent.TenantId);
        Assert.Equal("evt-1", failedEvent.EventId);
        Assert.Equal("sub-1", failedEvent.SubscriptionId);
        Assert.Equal("DLQ", failedEvent.Status);
        Assert.Equal(2, failedEvent.FinalAttemptNumber);
        Assert.Equal(1, fixture.UsageService.FailedIncrements);
    }

    [Fact]
    public async Task ProcessRetryAsync_FailedRetryWithNoAttemptsRemaining_PublishesWebhookDlqMessage()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();
        fixture.SeedSubscription("sub-1", maxAttempts: 2, initialDelaySeconds: 30, backoffType: "Fixed");
        fixture.DeliveryClient.Results.Enqueue(new WebhookDeliveryResult { IsSuccess = false, HttpStatusCode = 500, ErrorMessage = "boom", DurationMs = 44 });

        await fixture.Service.ProcessRetryAsync(fixture.RetryMessage);

        var published = Assert.Single(fixture.KafkaProducer.Published);
        Assert.Equal("webhook-dlq", published.Topic);
        Assert.Equal("tenant-1", published.Key);
        var dlqMessage = Assert.IsType<WebhookDlqMessage>(published.Message);
        Assert.Equal("evt-1", dlqMessage.EventId);
        Assert.Equal("sub-1", dlqMessage.SubscriptionId);
        Assert.Equal(2, dlqMessage.FinalAttemptNumber);
    }

    [Fact]
    public async Task ProcessRetryAsync_FailedEventStoredEvenIfDlqPublishFails()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();
        fixture.SeedSubscription("sub-1", maxAttempts: 2, initialDelaySeconds: 30, backoffType: "Fixed");
        fixture.DeliveryClient.Results.Enqueue(new WebhookDeliveryResult { IsSuccess = false, HttpStatusCode = 500, ErrorMessage = "boom", DurationMs = 44 });
        fixture.KafkaProducer.ThrowOnProduce = true;

        var exception = await Record.ExceptionAsync(() => fixture.Service.ProcessRetryAsync(fixture.RetryMessage));

        Assert.Null(exception);
        Assert.Single(fixture.FailedEventService.CreatedEvents);
    }

    [Fact]
    public async Task ProcessRetryAsync_FailedEventStorageFailure_DoesNotThrow()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();
        fixture.SeedSubscription("sub-1", maxAttempts: 2, initialDelaySeconds: 30, backoffType: "Fixed");
        fixture.DeliveryClient.Results.Enqueue(new WebhookDeliveryResult { IsSuccess = false, HttpStatusCode = 500, ErrorMessage = "boom", DurationMs = 44 });
        fixture.FailedEventService.ThrowOnCreate = true;

        var exception = await Record.ExceptionAsync(() => fixture.Service.ProcessRetryAsync(fixture.RetryMessage));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ProcessRetryAsync_Success_StoresDeliveryAttempt()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();
        fixture.SeedSubscription("sub-1", maxAttempts: 3, initialDelaySeconds: 30, backoffType: "Fixed");
        fixture.DeliveryClient.Results.Enqueue(new WebhookDeliveryResult { IsSuccess = true, HttpStatusCode = 200, ResponseBody = "ok", DurationMs = 21 });

        await fixture.Service.ProcessRetryAsync(fixture.RetryMessage);

        var attempt = (await fixture.Attempts.GetAllAsync()).Single();
        Assert.Equal(2, attempt.AttemptNumber);
        Assert.Equal(DeliveryStatus.Success, attempt.Status);
        Assert.Equal("corr-1", attempt.CorrelationId);
        Assert.Empty(fixture.KafkaProducer.Published);
    }

    [Fact]
    public async Task ProcessRetryAsync_Failure_ReschedulesRetry()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();
        fixture.SeedSubscription("sub-1", maxAttempts: 4, initialDelaySeconds: 30, backoffType: "Fixed");
        fixture.DeliveryClient.Results.Enqueue(new WebhookDeliveryResult { IsSuccess = false, HttpStatusCode = 500, ErrorMessage = "boom", DurationMs = 44 });

        await fixture.Service.ProcessRetryAsync(fixture.RetryMessage);

        var attempt = (await fixture.Attempts.GetAllAsync()).Single();
        Assert.Equal(2, attempt.AttemptNumber);
        Assert.Equal(DeliveryStatus.Failed, attempt.Status);

        var published = Assert.Single(fixture.KafkaProducer.Published);
        Assert.Equal("webhook-retry", published.Topic);
        var retryMessage = Assert.IsType<WebhookRetryMessage>(published.Message);
        Assert.Equal(3, retryMessage.AttemptNumber);
        Assert.Equal(new DateTime(2026, 4, 27, 11, 0, 30, DateTimeKind.Utc), retryMessage.NextRetryAt);
    }

    [Fact]
    public async Task ProcessRetryAsync_MissingIncomingEvent_Stops()
    {
        var fixture = new Fixture();

        await fixture.Service.ProcessRetryAsync(fixture.RetryMessage);

        Assert.Empty(await fixture.Attempts.GetAllAsync());
        Assert.Empty(fixture.KafkaProducer.Published);
        Assert.Contains(fixture.Logger.Records, x => x.Level == LogLevel.Warning && x.Message.Contains("incoming event was not found"));
    }

    [Fact]
    public async Task ProcessRetryAsync_MissingSubscription_Stops()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();

        await fixture.Service.ProcessRetryAsync(fixture.RetryMessage);

        Assert.Empty(await fixture.Attempts.GetAllAsync());
        Assert.Empty(fixture.KafkaProducer.Published);
        Assert.Contains(fixture.Logger.Records, x => x.Level == LogLevel.Warning && x.Message.Contains("subscription was not found"));
    }

    [Fact]
    public async Task ProcessRetryAsync_InactiveSubscription_Stops()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();
        fixture.SeedSubscription("sub-1", isActive: false);

        await fixture.Service.ProcessRetryAsync(fixture.RetryMessage);

        Assert.Empty(await fixture.Attempts.GetAllAsync());
        Assert.Empty(fixture.KafkaProducer.Published);
        Assert.Contains(fixture.Logger.Records, x => x.Level == LogLevel.Information && x.Message.Contains("subscription is inactive"));
    }


    [Fact]
    public async Task DeliveryService_DecryptsSecretsBeforeSending()
    {
        var fixture = new Fixture();
        fixture.SeedIncomingEvent();
        fixture.SeedSubscription("sub-1");
        fixture.DeliveryClient.Results.Enqueue(new WebhookDeliveryResult { IsSuccess = true, HttpStatusCode = 200, DurationMs = 20 });

        await fixture.Service.ProcessEventAsync(fixture.Message);

        var sent = fixture.DeliveryClient.Requests.Single();
        Assert.Equal("secret", sent.Authentication!.ApiKeyHeader!.HeaderValue);
    }

    private sealed class Fixture
    {
        public InMemoryRepository<IncomingEvent> IncomingEvents { get; } = new();
        public InMemoryRepository<Subscription> Subscriptions { get; } = new();
        public InMemoryRepository<DeliveryAttempt> Attempts { get; } = new();
        public FakeWebhookDeliveryClient DeliveryClient { get; } = new();
        public FakeKafkaProducer KafkaProducer { get; } = new();
        public FakeFailedEventService FailedEventService { get; } = new();
        public FakeUsageService UsageService { get; } = new();
        public ListLogger<WebhookDeliveryService> Logger { get; } = new();
        public FakeSecretEncryptionService EncryptionService { get; } = new();
        public WebhookEventMessage Message { get; } = new()
        {
            TenantId = "tenant-1",
            EventId = "evt-1",
            EventType = "order.created",
            CorrelationId = "corr-1",
            ReceivedAt = new DateTime(2026, 4, 27, 10, 0, 0, DateTimeKind.Utc),
        };
        public WebhookRetryMessage RetryMessage { get; } = new()
        {
            TenantId = "tenant-1",
            EventId = "evt-1",
            SubscriptionId = "sub-1",
            AttemptNumber = 2,
            NextRetryAt = new DateTime(2026, 4, 27, 11, 0, 0, DateTimeKind.Utc),
            CorrelationId = "corr-1",
        };

        public WebhookDeliveryService Service => new(
            IncomingEvents,
            Subscriptions,
            Attempts,
            new FixedDateTimeProvider(),
            DeliveryClient,
            KafkaProducer,
            new RetryPolicyService(),
            FailedEventService,
            UsageService,
            EncryptionService,
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
            string backoffType = "Exponential",
            bool isActive = true)
        {
            Subscriptions.AddAsync(new Subscription
            {
                Id = id,
                TenantId = "tenant-1",
                EventType = "order.created",
                TargetUrl = url,
                IsActive = isActive,
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
                        HeaderValue = EncryptionService.Encrypt("secret"),
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

    private sealed class FakeFailedEventService : IFailedEventService
    {
        public List<FailedEvent> CreatedEvents { get; } = [];
        public bool ThrowOnCreate { get; set; }

        public Task CreateAsync(FailedEvent failedEvent, CancellationToken cancellationToken = default)
        {
            if (ThrowOnCreate)
            {
                throw new InvalidOperationException("Mongo unavailable");
            }

            CreatedEvents.Add(failedEvent);
            return Task.CompletedTask;
        }

        public Task<HookBridge.Application.DTOs.Common.PagedResponseDto<FailedEventResponseDto>> SearchAsync(FailedEventSearchRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(HookBridge.Application.DTOs.Common.PagedResponseDto<FailedEventResponseDto>.Create([], 1, 50, 0));

        public Task<FailedEventResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult<FailedEventResponseDto?>(null);

        public Task<bool> RetryAsync(string failedEventId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FakeUsageService : IUsageService
    {
        public int DeliveredIncrements { get; private set; }
        public int FailedIncrements { get; private set; }

        public Task<UsageMetric> GetCurrentMonthUsageAsync(string tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(new UsageMetric { TenantId = tenantId, Year = 2026, Month = 4, LastUpdatedAt = DateTime.UtcNow });

        public Task IncrementEventsReceivedAsync(string tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task IncrementEventsDeliveredAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            DeliveredIncrements++;
            return Task.CompletedTask;
        }

        public Task IncrementEventsFailedAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            FailedIncrements++;
            return Task.CompletedTask;
        }

        public Task<bool> CanAcceptEventAsync(string tenantId, CancellationToken cancellationToken = default) => Task.FromResult(true);
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


    private sealed class FakeSecretEncryptionService : ISecretEncryptionService
    {
        private const string Prefix = "enc:v1:fake:";

        public string Encrypt(string plainText)
            => IsEncrypted(plainText)
                ? plainText
                : Prefix + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainText));

        public string Decrypt(string cipherText)
            => IsEncrypted(cipherText)
                ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cipherText[Prefix.Length..]))
                : cipherText;

        public bool IsEncrypted(string value)
            => !string.IsNullOrWhiteSpace(value)
               && value.StartsWith(Prefix, StringComparison.Ordinal);
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

        
        public Task<(IReadOnlyList<T> Items, long TotalCount)> QueryAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, MongoDB.Driver.SortDefinition<T> sort, int skip, int limit, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            var filtered = _items.Where(compiled).ToList();
            var paged = filtered.Skip(skip).Take(limit).ToList();
            return Task.FromResult<(IReadOnlyList<T>, long)>((paged, filtered.LongCount()));
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
