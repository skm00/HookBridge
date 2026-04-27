using FluentValidation.TestHelper;
using HookBridge.Application.DTOs.ApiKeys;
using HookBridge.Application.DTOs.Events;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Messaging;
using HookBridge.Application.Services;
using HookBridge.Application.Validation.Events;
using HookBridge.Domain.Entities;
using HookBridge.Shared.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class EventIngestionServiceTests
{
    [Fact]
    public async Task IngestEvent_Success()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        var kafkaProducer = new FakeKafkaProducer();
        var usageService = new FakeUsageService();
        var service = CreateService(repository, new FakeApiKeyService(valid: true), kafkaProducer, usageService);

        var response = await service.IngestAsync("tenant-1", "hb_live_key", BuildRequest("evt-1"), "corr-1");

        Assert.Equal("accepted", response.Status);
        Assert.Equal("evt-1", response.EventId);
        Assert.Equal("Event accepted for delivery.", response.Message);
    }

    [Fact]
    public async Task InvalidApiKey_ThrowsUnauthorized()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        var service = CreateService(repository, new FakeApiKeyService(valid: false), new FakeKafkaProducer());

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.IngestAsync("tenant-1", "bad-key", BuildRequest("evt-1"), null));
    }

    [Fact]
    public async Task DuplicateEvent_ReturnsAcceptedResponse()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        await repository.AddAsync(new IncomingEvent
        {
            Id = "incoming-1",
            TenantId = "tenant-1",
            EventId = "evt-1",
            EventType = "order.created",
            Payload = new { },
            Status = "Accepted",
            ReceivedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });

        var kafkaProducer = new FakeKafkaProducer();
        var usageService = new FakeUsageService();
        var service = CreateService(repository, new FakeApiKeyService(valid: true), kafkaProducer, usageService);
        var response = await service.IngestAsync("tenant-1", "hb_live_key", BuildRequest("evt-1"), "corr-1");

        Assert.Equal("accepted", response.Status);
        Assert.Equal("Event already accepted.", response.Message);

        var stored = await repository.FindAsync(x => x.TenantId == "tenant-1" && x.EventId == "evt-1");
        Assert.Single(stored);
        Assert.False(kafkaProducer.WasCalled);
        Assert.Equal(0, usageService.EventsReceivedIncrements);
    }

    [Fact]
    public async Task IngestEvent_IncrementsEventsReceived()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        var usageService = new FakeUsageService { CanAcceptEvent = true };
        var service = CreateService(repository, new FakeApiKeyService(valid: true), new FakeKafkaProducer(), usageService);

        await service.IngestAsync("tenant-1", "hb_live_key", BuildRequest("evt-usage"), "corr-usage");

        Assert.Equal(1, usageService.EventsReceivedIncrements);
    }

    [Fact]
    public async Task IngestEvent_WhenLimitExceeded_ThrowsTooManyRequests()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        var usageService = new FakeUsageService { CanAcceptEvent = false };
        var service = CreateService(repository, new FakeApiKeyService(valid: true), new FakeKafkaProducer(), usageService);

        var ex = await Assert.ThrowsAsync<TooManyRequestsException>(() =>
            service.IngestAsync("tenant-1", "hb_live_key", BuildRequest("evt-limit"), "corr-limit"));

        Assert.Equal("Monthly event limit exceeded for the current billing plan.", ex.Message);
    }

    [Fact]
    public async Task EventStored_WithAcceptedStatus()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        var service = CreateService(repository, new FakeApiKeyService(valid: true), new FakeKafkaProducer());

        await service.IngestAsync("tenant-1", "hb_live_key", BuildRequest("evt-2"), null);

        var stored = (await repository.FindAsync(x => x.EventId == "evt-2")).Single();
        Assert.Equal("Accepted", stored.Status);
        Assert.Equal("api-key-1", stored.ApiKeyId);
    }

    [Fact]
    public async Task CorrelationIdStored_IfProvided()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        var service = CreateService(repository, new FakeApiKeyService(valid: true), new FakeKafkaProducer());

        await service.IngestAsync("tenant-1", "hb_live_key", BuildRequest("evt-3"), "corr-123");

        var stored = (await repository.FindAsync(x => x.EventId == "evt-3")).Single();
        Assert.Equal("corr-123", stored.CorrelationId);
    }

    [Fact]
    public async Task IngestEvent_CallsKafkaProducer()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        var kafkaProducer = new FakeKafkaProducer();
        var service = CreateService(repository, new FakeApiKeyService(valid: true), kafkaProducer);

        await service.IngestAsync("tenant-1", "hb_live_key", BuildRequest("evt-4"), "corr-777");

        Assert.True(kafkaProducer.WasCalled);
        Assert.Equal(KafkaTopics.WebhookEvents, kafkaProducer.Topic);
        Assert.Equal("tenant-1", kafkaProducer.Key);
    }

    [Fact]
    public async Task IngestEvent_ReturnsAcceptedWhenKafkaPublishFails()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        var kafkaProducer = new FakeKafkaProducer(shouldThrow: true);
        var service = CreateService(repository, new FakeApiKeyService(valid: true), kafkaProducer);

        var response = await service.IngestAsync("tenant-1", "hb_live_key", BuildRequest("evt-5"), "corr-888");

        Assert.Equal("accepted", response.Status);
        Assert.Equal("Event accepted but publishing is delayed.", response.Message);
        var stored = (await repository.FindAsync(x => x.EventId == "evt-5")).SingleOrDefault();
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task IngestEvent_PublishesWebhookEventMessageWithCorrectValues()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        var kafkaProducer = new FakeKafkaProducer();
        var service = CreateService(repository, new FakeApiKeyService(valid: true), kafkaProducer);

        await service.IngestAsync("tenant-1", "hb_live_key", BuildRequest("evt-6"), "corr-999");

        var message = Assert.IsType<WebhookEventMessage>(kafkaProducer.Message);
        Assert.Equal("evt-6", message.EventId);
        Assert.Equal("tenant-1", message.TenantId);
        Assert.Equal("order.created", message.EventType);
        Assert.Equal(new DateTime(2026, 4, 27, 10, 30, 0, DateTimeKind.Utc), message.ReceivedAt);
        Assert.Equal("corr-999", message.CorrelationId);
    }


    [Fact]
    public void EventType_WithInvalidCharacters_FailsValidation()
    {
        var validator = new EventIngestionRequestDtoValidator();
        var result = validator.TestValidate(new EventIngestionRequestDto
        {
            EventType = "order/created",
            EventId = "evt-1",
            Data = new { orderId = "1001" },
        });

        result.ShouldHaveValidationErrorFor(x => x.EventType)
            .WithErrorMessage("EventType may contain only letters, numbers, dot (.), dash (-), and underscore (_).");
    }

    [Fact]
    public void PayloadLargerThanLimit_FailsValidation()
    {
        var validator = new EventIngestionRequestDtoValidator();
        var result = validator.TestValidate(new EventIngestionRequestDto
        {
            EventType = "order.created",
            EventId = "evt-1",
            Data = new { blob = new string('a', HookBridge.Shared.Constants.ValidationLimits.MaxPayloadSizeBytes + 10) },
        });

        result.ShouldHaveValidationErrorFor(x => x.Data)
            .WithErrorMessage($"Data payload exceeds {HookBridge.Shared.Constants.ValidationLimits.MaxPayloadSizeBytes} bytes.");
    }

    [Fact]
    public void InvalidRequestValidation_Fails()
    {
        var validator = new EventIngestionRequestDtoValidator();
        var result = validator.TestValidate(new EventIngestionRequestDto
        {
            EventType = string.Empty,
            EventId = string.Empty,
            Data = null!,
        });

        result.ShouldHaveValidationErrorFor(x => x.EventType);
        result.ShouldHaveValidationErrorFor(x => x.EventId);
        result.ShouldHaveValidationErrorFor(x => x.Data);
    }

    private static EventIngestionService CreateService(
        InMemoryRepository<IncomingEvent> repository,
        IApiKeyService apiKeyService,
        IKafkaProducer kafkaProducer,
        IUsageService? usageService = null)
    {
        return new EventIngestionService(
            repository,
            apiKeyService,
            usageService ?? new FakeUsageService(),
            new FixedGuidGenerator(),
            new FixedDateTimeProvider(),
            new EventIngestionRequestDtoValidator(),
            kafkaProducer,
            NullLogger<EventIngestionService>.Instance);
    }

    private static EventIngestionRequestDto BuildRequest(string eventId) => new()
    {
        EventType = "order.created",
        EventId = eventId,
        Timestamp = new DateTime(2026, 4, 27, 10, 0, 0, DateTimeKind.Utc),
        Data = new { orderId = "1001", amount = 250 },
    };

    private sealed class FakeKafkaProducer(bool shouldThrow = false) : IKafkaProducer
    {
        public bool WasCalled { get; private set; }

        public string? Topic { get; private set; }

        public string? Key { get; private set; }

        public object? Message { get; private set; }

        public Task ProduceAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            Topic = topic;
            Key = key;
            Message = message;

            if (shouldThrow)
            {
                throw new InvalidOperationException("Kafka unavailable");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeApiKeyService(bool valid) : IApiKeyService
    {
        public Task<CreateApiKeyResponseDto> CreateAsync(string tenantId, CreateApiKeyRequestDto request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ApiKeyResponseDto>> GetByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> RevokeAsync(string tenantId, string keyId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ApiKeyValidationResult> ValidateAsync(string tenantId, string plainApiKey, CancellationToken cancellationToken = default)
            => Task.FromResult(valid
                ? new ApiKeyValidationResult { IsValid = true, TenantId = tenantId, ApiKeyId = "api-key-1" }
                : new ApiKeyValidationResult { IsValid = false, FailureReason = "api_key_not_found" });
    }

    private sealed class FakeUsageService : IUsageService
    {
        public bool CanAcceptEvent { get; set; } = true;
        public int EventsReceivedIncrements { get; private set; }

        public Task<UsageMetric> GetCurrentMonthUsageAsync(string tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(new UsageMetric
            {
                TenantId = tenantId,
                Year = 2026,
                Month = 4,
                EventsReceived = EventsReceivedIncrements,
                LastUpdatedAt = new DateTime(2026, 4, 27, 10, 30, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 4, 27, 10, 30, 0, DateTimeKind.Utc),
            });

        public Task IncrementEventsReceivedAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            EventsReceivedIncrements++;
            return Task.CompletedTask;
        }

        public Task IncrementEventsDeliveredAsync(string tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task IncrementEventsFailedAsync(string tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> CanAcceptEventAsync(string tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(CanAcceptEvent);
    }

    private sealed class FixedGuidGenerator : IGuidGenerator
    {
        private int _counter;
        public string NewGuid() => $"incoming-{++_counter}";
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 4, 27, 10, 30, 0, DateTimeKind.Utc);
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
}
