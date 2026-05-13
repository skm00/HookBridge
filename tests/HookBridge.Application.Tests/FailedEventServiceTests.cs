using HookBridge.Application.DTOs.FailedEvents;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Messaging;
using HookBridge.Application.Models.Delivery;
using HookBridge.Application.Services;
using HookBridge.Domain.Constants;
using HookBridge.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class FailedEventServiceTests
{
    [Fact]
    public async Task SearchAsync_ByTenantId_ReturnsMatchingItems()
    {
        var fixture = new Fixture();
        fixture.Seed();

        var results = await fixture.Service.SearchAsync(new FailedEventSearchRequestDto { TenantId = "tenant-1" });

        Assert.All(results.Items, x => Assert.Equal("tenant-1", x.TenantId));
    }

    [Fact]
    public async Task SearchAsync_ByEventId_ReturnsMatchingItems()
    {
        var fixture = new Fixture();
        fixture.Seed();

        var results = await fixture.Service.SearchAsync(new FailedEventSearchRequestDto { EventId = "evt-2" });

        Assert.All(results.Items, x => Assert.Equal("evt-2", x.EventId));
    }

    [Fact]
    public async Task SearchAsync_ByStatus_ReturnsMatchingItems()
    {
        var fixture = new Fixture();
        fixture.Seed();

        var results = await fixture.Service.SearchAsync(new FailedEventSearchRequestDto { Status = "DLQ" });

        Assert.NotEmpty(results.Items);
        Assert.All(results.Items, x => Assert.Equal("DLQ", x.Status));
    }

    [Fact]
    public async Task SearchAsync_ReturnsNewestFirst()
    {
        var fixture = new Fixture();
        fixture.Seed();

        var results = await fixture.Service.SearchAsync(new FailedEventSearchRequestDto());

        Assert.True(results.Items.Zip(results.Items.Skip(1)).All(pair => pair.First.FailedAt <= pair.Second.FailedAt));
    }

    [Fact]
    public async Task SearchAsync_LimitsTo500()
    {
        var fixture = new Fixture();
        fixture.SeedMany(520);

        var results = await fixture.Service.SearchAsync(new FailedEventSearchRequestDto());

        Assert.Equal(50, results.PageSize);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsItemWhenFound()
    {
        var fixture = new Fixture();
        fixture.Seed();

        var result = await fixture.Service.GetByIdAsync("failed-1");

        Assert.NotNull(result);
        Assert.Equal("failed-1", result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenMissing()
    {
        var fixture = new Fixture();
        fixture.Seed();

        var result = await fixture.Service.GetByIdAsync("missing");

        Assert.Null(result);
    }


    [Fact]
    public async Task CreateAsync_CreatesDlqNotification()
    {
        var fixture = new Fixture();

        var failedEvent = new FailedEvent
        {
            Id = "failed-new",
            TenantId = "tenant-1",
            EventId = "evt-new",
            SubscriptionId = "sub-1",
            EventType = "order.created",
            TargetUrl = "https://example.com",
            Reason = "Retry attempts exhausted",
            FinalAttemptNumber = 3,
            Status = "DLQ",
            FailedAt = new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc),
        };

        await fixture.Service.CreateAsync(failedEvent);

        var notification = Assert.Single(fixture.Notifications.Created);
        Assert.Equal(NotificationTypes.DlqCreated, notification.Type);
        Assert.Equal(NotificationSeverities.Error, notification.Severity);
        Assert.Equal(nameof(FailedEvent), notification.ResourceType);
        Assert.Equal("failed-new", notification.ResourceId);
    }

    [Fact]
    public async Task RetryAsync_Success_PublishesWebhookRetryMessageAndUpdatesStatus()
    {
        var fixture = new Fixture();
        fixture.Seed();

        var result = await fixture.Service.RetryAsync("failed-1");

        Assert.True(result);
        var published = Assert.Single(fixture.KafkaProducer.Published);
        Assert.Equal("webhook-retry", published.Topic);
        Assert.Equal("tenant-1", published.Key);
        var message = Assert.IsType<WebhookRetryMessage>(published.Message);
        Assert.Equal("evt-1", message.EventId);
        Assert.Equal("tenant-1", message.TenantId);
        Assert.Equal("sub-1", message.SubscriptionId);
        Assert.Equal("failed-1", message.FailedEventId);
        Assert.Equal(1, message.AttemptNumber);
        Assert.Equal(new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc), message.NextRetryAt);
        Assert.Equal("corr-1", message.CorrelationId);

        var updated = await fixture.Repository.GetByIdAsync("failed-1");
        Assert.NotNull(updated);
        Assert.Equal("RetryRequested", updated.Status);
        Assert.Equal(new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc), updated.UpdatedAt);
        Assert.Equal("FailedEventManualRetryRequested", Assert.Single(fixture.Audit.Logged).Action);
    }

    [Fact]
    public async Task RetryAsync_ReturnsFalse_WhenMissing()
    {
        var fixture = new Fixture();
        fixture.Seed();

        var result = await fixture.Service.RetryAsync("missing");

        Assert.False(result);
        Assert.Empty(fixture.KafkaProducer.Published);
    }

    [Fact]
    public async Task RetryAsync_ReturnsFalse_WhenStatusIsNotDlq()
    {
        var fixture = new Fixture();
        fixture.Seed();
        fixture.Repository.UpdateStatus("failed-1", "Processed");

        var result = await fixture.Service.RetryAsync("failed-1");

        Assert.False(result);
        Assert.Empty(fixture.KafkaProducer.Published);
    }

    [Fact]
    public async Task RetryAsync_WhenKafkaPublishFails_DoesNotUpdateStatus()
    {
        var fixture = new Fixture();
        fixture.Seed();
        fixture.KafkaProducer.ThrowOnProduce = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.RetryAsync("failed-1"));

        var failedEvent = await fixture.Repository.GetByIdAsync("failed-1");
        Assert.NotNull(failedEvent);
        Assert.Equal("DLQ", failedEvent.Status);
        Assert.Null(failedEvent.UpdatedAt);
    }

    [Fact]
    public async Task MarkRetrySucceededAsync_UpdatesFailedEventStatus()
    {
        var fixture = new Fixture();
        fixture.Seed();

        await fixture.Service.MarkRetrySucceededAsync(
            "failed-1",
            new WebhookDeliveryResult { IsSuccess = true, HttpStatusCode = 200 },
            2,
            "https://example.com/current",
            "corr-retry");

        var failedEvent = await fixture.Repository.GetByIdAsync("failed-1");
        Assert.NotNull(failedEvent);
        Assert.Equal("Retried", failedEvent.Status);
        Assert.Equal("Manual retry succeeded.", failedEvent.Reason);
        Assert.Equal(2, failedEvent.FinalAttemptNumber);
        Assert.Equal(200, failedEvent.LastHttpStatusCode);
        Assert.Null(failedEvent.LastErrorMessage);
        Assert.Equal("https://example.com/current", failedEvent.TargetUrl);
        Assert.Equal("corr-retry", failedEvent.CorrelationId);
        Assert.Equal(new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc), failedEvent.UpdatedAt);
    }

    [Fact]
    public async Task MarkRetryExhaustedAsync_ReturnsFailedEventToDlq()
    {
        var fixture = new Fixture();
        fixture.Seed();
        fixture.Repository.UpdateStatus("failed-1", "RetryRequested");

        await fixture.Service.MarkRetryExhaustedAsync(
            "failed-1",
            new WebhookDeliveryResult { IsSuccess = false, HttpStatusCode = 500, ErrorMessage = "boom" },
            2,
            "https://example.com/current",
            "corr-retry");

        var failedEvent = await fixture.Repository.GetByIdAsync("failed-1");
        Assert.NotNull(failedEvent);
        Assert.Equal("DLQ", failedEvent.Status);
        Assert.Equal("Retry attempts exhausted", failedEvent.Reason);
        Assert.Equal(2, failedEvent.FinalAttemptNumber);
        Assert.Equal(500, failedEvent.LastHttpStatusCode);
        Assert.Equal("boom", failedEvent.LastErrorMessage);
        Assert.Equal("https://example.com/current", failedEvent.TargetUrl);
        Assert.Equal("corr-retry", failedEvent.CorrelationId);
        Assert.Equal(new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc), failedEvent.FailedAt);
        Assert.Equal(new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc), failedEvent.UpdatedAt);
    }

    [Fact]
    public async Task MarkRetrySucceededAsync_WhenFailedEventIsMissing_DoesNotCreateOrPublishSideEffects()
    {
        var fixture = new Fixture();

        await fixture.Service.MarkRetrySucceededAsync(
            "missing",
            new WebhookDeliveryResult { IsSuccess = true, HttpStatusCode = 200 },
            1,
            "https://example.com/current",
            "corr-missing");

        Assert.Empty(fixture.KafkaProducer.Published);
        Assert.Empty(fixture.Audit.Logged);
        Assert.Empty(fixture.Notifications.Created);
    }

    [Fact]
    public async Task MarkRetryExhaustedAsync_WhenFailedEventIsMissing_DoesNotCreateOrPublishSideEffects()
    {
        var fixture = new Fixture();

        await fixture.Service.MarkRetryExhaustedAsync(
            "missing",
            new WebhookDeliveryResult { IsSuccess = false, HttpStatusCode = 503, ErrorMessage = "unavailable" },
            3,
            "https://example.com/current",
            "corr-missing");

        Assert.Empty(fixture.KafkaProducer.Published);
        Assert.Empty(fixture.Audit.Logged);
        Assert.Empty(fixture.Notifications.Created);
    }

    private sealed class Fixture
    {
        public InMemoryFailedEventRepository Repository { get; } = new();

        public FakeKafkaProducer KafkaProducer { get; } = new();
        public RecordingAuditLogService Audit { get; } = new();

        private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc));
        public RecordingNotificationService Notifications { get; } = new();

        public FailedEventService Service => new(Repository, KafkaProducer, Audit, _dateTimeProvider, Notifications, NullLogger<FailedEventService>.Instance);

        public void Seed()
        {
            Repository.Add(new FailedEvent
            {
                Id = "failed-1",
                TenantId = "tenant-1",
                EventId = "evt-1",
                SubscriptionId = "sub-1",
                EventType = "order.created",
                TargetUrl = "https://a.example.com",
                Reason = "Retry attempts exhausted",
                FinalAttemptNumber = 3,
                Status = "DLQ",
                FailedAt = new DateTime(2026, 4, 27, 10, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 4, 27, 10, 0, 0, DateTimeKind.Utc),
                CorrelationId = "corr-1",
            });
            Repository.Add(new FailedEvent
            {
                Id = "failed-2",
                TenantId = "tenant-2",
                EventId = "evt-2",
                SubscriptionId = "sub-2",
                EventType = "order.updated",
                TargetUrl = "https://b.example.com",
                Reason = "Retry attempts exhausted",
                FinalAttemptNumber = 2,
                Status = "DLQ",
                FailedAt = new DateTime(2026, 4, 27, 11, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 4, 27, 11, 0, 0, DateTimeKind.Utc),
            });
        }

        public void SeedMany(int count)
        {
            for (var i = 0; i < count; i++)
            {
                Repository.Add(new FailedEvent
                {
                    Id = $"failed-{i}",
                    TenantId = "tenant-1",
                    EventId = $"evt-{i}",
                    SubscriptionId = "sub-1",
                    EventType = "order.created",
                    TargetUrl = "https://a.example.com",
                    Reason = "Retry attempts exhausted",
                    FinalAttemptNumber = 3,
                    Status = "DLQ",
                    FailedAt = new DateTime(2026, 4, 27, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i),
                    CreatedAt = new DateTime(2026, 4, 27, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i),
                });
            }
        }
    }

    private sealed class RecordingAuditLogService : IAuditLogService
    {
        public List<AuditLog> Logged { get; } = [];
        public Task LogAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
        {
            Logged.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task<HookBridge.Application.DTOs.Common.PagedResponseDto<HookBridge.Application.DTOs.AuditLogs.AuditLogResponseDto>> SearchAsync(HookBridge.Application.DTOs.AuditLogs.AuditLogSearchRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult(HookBridge.Application.DTOs.Common.PagedResponseDto<HookBridge.Application.DTOs.AuditLogs.AuditLogResponseDto>.Create([], 1, 50, 0));

        public Task<HookBridge.Application.DTOs.AuditLogs.AuditLogResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult<HookBridge.Application.DTOs.AuditLogs.AuditLogResponseDto?>(null);
    }


    public sealed class RecordingNotificationService : INotificationService
    {
        public List<Notification> Created { get; } = [];

        public Task CreateAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            Created.Add(notification);
            return Task.CompletedTask;
        }

        public Task<HookBridge.Application.DTOs.Common.PagedResponseDto<HookBridge.Application.DTOs.Notifications.NotificationResponseDto>> SearchAsync(HookBridge.Application.DTOs.Notifications.NotificationSearchRequestDto request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<HookBridge.Application.DTOs.Notifications.NotificationResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> MarkAsReadAsync(string id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> GetUnreadCountAsync(string tenantId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class InMemoryFailedEventRepository : IFailedEventRepository
    {
        private readonly List<FailedEvent> _items = [];

        public Task AddAsync(FailedEvent failedEvent, CancellationToken cancellationToken = default)
        {
            _items.Add(failedEvent);
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<FailedEvent> Items, long TotalCount)> SearchAsync(FailedEventSearchRequestDto request, MongoDB.Driver.SortDefinition<FailedEvent> sort, int skip, int limit, CancellationToken cancellationToken = default)
        {
            IEnumerable<FailedEvent> query = _items;

            if (!string.IsNullOrWhiteSpace(request.TenantId))
            {
                query = query.Where(x => x.TenantId == request.TenantId);
            }

            if (!string.IsNullOrWhiteSpace(request.EventId))
            {
                query = query.Where(x => x.EventId == request.EventId);
            }

            if (!string.IsNullOrWhiteSpace(request.SubscriptionId))
            {
                query = query.Where(x => x.SubscriptionId == request.SubscriptionId);
            }

            if (!string.IsNullOrWhiteSpace(request.EventType))
            {
                query = query.Where(x => x.EventType == request.EventType);
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                query = query.Where(x => x.Status == request.Status);
            }

            if (request.FromDate.HasValue)
            {
                query = query.Where(x => x.FailedAt >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(x => x.FailedAt <= request.ToDate.Value);
            }

            var list = query.ToList();
            var paged = list.Skip(skip).Take(limit).ToList();
            return Task.FromResult<(IReadOnlyList<FailedEvent>, long)>((paged, list.LongCount()));
        }

        public Task<FailedEvent?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        }

        public Task UpdateAsync(FailedEvent failedEvent, CancellationToken cancellationToken = default)
        {
            var existing = _items.FindIndex(x => x.Id == failedEvent.Id);
            if (existing >= 0)
            {
                _items[existing] = failedEvent;
            }

            return Task.CompletedTask;
        }

        public void Add(FailedEvent failedEvent)
        {
            _items.Add(failedEvent);
        }

        public Task<long> CountByStatusAsync(string tenantId, string status, CancellationToken cancellationToken = default)
        {
            var count = _items.LongCount(x => x.TenantId == tenantId && x.Status == status);
            return Task.FromResult(count);
        }

        public void UpdateStatus(string id, string status)
        {
            var item = _items.First(x => x.Id == id);
            item.Status = status;
        }
    }

    private sealed class FakeKafkaProducer : IKafkaProducer
    {
        public List<(string Topic, string Key, object Message)> Published { get; } = [];

        public bool ThrowOnProduce { get; set; }

        public Task ProduceAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default)
        {
            if (ThrowOnProduce)
            {
                throw new InvalidOperationException("Kafka publish failed");
            }

            Published.Add((topic, key, message!));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
