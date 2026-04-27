using HookBridge.Application.DTOs.FailedEvents;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Services;
using HookBridge.Domain.Entities;

namespace HookBridge.Application.Tests;

public sealed class FailedEventServiceTests
{
    [Fact]
    public async Task SearchAsync_ByTenantId_ReturnsMatchingItems()
    {
        var fixture = new Fixture();
        fixture.Seed();

        var results = await fixture.Service.SearchAsync(new FailedEventSearchRequestDto { TenantId = "tenant-1" });

        Assert.All(results, x => Assert.Equal("tenant-1", x.TenantId));
    }

    [Fact]
    public async Task SearchAsync_ByEventId_ReturnsMatchingItems()
    {
        var fixture = new Fixture();
        fixture.Seed();

        var results = await fixture.Service.SearchAsync(new FailedEventSearchRequestDto { EventId = "evt-2" });

        Assert.All(results, x => Assert.Equal("evt-2", x.EventId));
    }

    [Fact]
    public async Task SearchAsync_ByStatus_ReturnsMatchingItems()
    {
        var fixture = new Fixture();
        fixture.Seed();

        var results = await fixture.Service.SearchAsync(new FailedEventSearchRequestDto { Status = "DLQ" });

        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.Equal("DLQ", x.Status));
    }

    [Fact]
    public async Task SearchAsync_ReturnsNewestFirst()
    {
        var fixture = new Fixture();
        fixture.Seed();

        var results = await fixture.Service.SearchAsync(new FailedEventSearchRequestDto());

        Assert.True(results.Zip(results.Skip(1)).All(pair => pair.First.FailedAt >= pair.Second.FailedAt));
    }

    [Fact]
    public async Task SearchAsync_LimitsTo500()
    {
        var fixture = new Fixture();
        fixture.SeedMany(520);

        var results = await fixture.Service.SearchAsync(new FailedEventSearchRequestDto());

        Assert.Equal(500, results.Count);
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

    private sealed class Fixture
    {
        private readonly InMemoryFailedEventRepository _repository = new();

        public FailedEventService Service => new(_repository);

        public void Seed()
        {
            _repository.Add(new FailedEvent
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
            });
            _repository.Add(new FailedEvent
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
                _repository.Add(new FailedEvent
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

    private sealed class InMemoryFailedEventRepository : IFailedEventRepository
    {
        private readonly List<FailedEvent> _items = [];

        public Task AddAsync(FailedEvent failedEvent, CancellationToken cancellationToken = default)
        {
            _items.Add(failedEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FailedEvent>> SearchAsync(FailedEventSearchRequestDto request, CancellationToken cancellationToken = default)
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

            return Task.FromResult<IReadOnlyList<FailedEvent>>(query
                .OrderByDescending(x => x.FailedAt)
                .Take(500)
                .ToList());
        }

        public Task<FailedEvent?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        }

        public void Add(FailedEvent failedEvent)
        {
            _items.Add(failedEvent);
        }
    }
}
