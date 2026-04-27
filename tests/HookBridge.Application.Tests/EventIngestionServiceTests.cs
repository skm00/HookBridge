using FluentValidation.TestHelper;
using HookBridge.Application.DTOs.ApiKeys;
using HookBridge.Application.DTOs.Events;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Services;
using HookBridge.Application.Validation.Events;
using HookBridge.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace HookBridge.Application.Tests;

public sealed class EventIngestionServiceTests
{
    [Fact]
    public async Task IngestEvent_Success()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        var service = CreateService(repository, new FakeApiKeyService(valid: true));

        var response = await service.IngestAsync("tenant-1", "hb_live_key", BuildRequest("evt-1"), "corr-1");

        Assert.Equal("accepted", response.Status);
        Assert.Equal("evt-1", response.EventId);
        Assert.Equal("Event accepted for delivery.", response.Message);
    }

    [Fact]
    public async Task InvalidApiKey_ThrowsUnauthorized()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        var service = CreateService(repository, new FakeApiKeyService(valid: false));

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

        var service = CreateService(repository, new FakeApiKeyService(valid: true));
        var response = await service.IngestAsync("tenant-1", "hb_live_key", BuildRequest("evt-1"), "corr-1");

        Assert.Equal("accepted", response.Status);
        Assert.Equal("Event already accepted.", response.Message);

        var stored = await repository.FindAsync(x => x.TenantId == "tenant-1" && x.EventId == "evt-1");
        Assert.Single(stored);
    }

    [Fact]
    public async Task EventStored_WithAcceptedStatus()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        var service = CreateService(repository, new FakeApiKeyService(valid: true));

        await service.IngestAsync("tenant-1", "hb_live_key", BuildRequest("evt-2"), null);

        var stored = (await repository.FindAsync(x => x.EventId == "evt-2")).Single();
        Assert.Equal("Accepted", stored.Status);
        Assert.Equal("api-key-1", stored.ApiKeyId);
    }

    [Fact]
    public async Task CorrelationIdStored_IfProvided()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        var service = CreateService(repository, new FakeApiKeyService(valid: true));

        await service.IngestAsync("tenant-1", "hb_live_key", BuildRequest("evt-3"), "corr-123");

        var stored = (await repository.FindAsync(x => x.EventId == "evt-3")).Single();
        Assert.Equal("corr-123", stored.CorrelationId);
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
        IApiKeyService apiKeyService)
    {
        return new EventIngestionService(
            repository,
            apiKeyService,
            new FixedGuidGenerator(),
            new FixedDateTimeProvider(),
            new EventIngestionRequestDtoValidator(),
            NullLogger<EventIngestionService>.Instance);
    }

    private static EventIngestionRequestDto BuildRequest(string eventId) => new()
    {
        EventType = "order.created",
        EventId = eventId,
        Timestamp = new DateTime(2026, 4, 27, 10, 0, 0, DateTimeKind.Utc),
        Data = new { orderId = "1001", amount = 250 },
    };

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
