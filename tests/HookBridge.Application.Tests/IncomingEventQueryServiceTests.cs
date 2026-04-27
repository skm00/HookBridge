using HookBridge.Application.DTOs.Events;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Services;
using HookBridge.Domain.Entities;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class IncomingEventQueryServiceTests
{
    [Fact]
    public async Task SearchByTenantId_ReturnsOnlyTenantRecords()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        await SeedAsync(repository);
        var service = new IncomingEventQueryService(repository);

        var result = await service.SearchAsync(new IncomingEventSearchRequestDto { TenantId = "tenant-1" });

        Assert.All(result.Items, x => Assert.Equal("tenant-1", x.TenantId));
    }

    [Fact]
    public async Task SearchByEventId_ReturnsMatches()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        await SeedAsync(repository);
        var service = new IncomingEventQueryService(repository);

        var result = await service.SearchAsync(new IncomingEventSearchRequestDto { TenantId = "tenant-1", EventId = "evt-2" });

        var item = Assert.Single(result.Items);
        Assert.Equal("evt-2", item.EventId);
    }

    [Fact]
    public async Task SearchByEventType_ReturnsMatches()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        await SeedAsync(repository);
        var service = new IncomingEventQueryService(repository);

        var result = await service.SearchAsync(new IncomingEventSearchRequestDto { TenantId = "tenant-1", EventType = "invoice.paid" });

        var item = Assert.Single(result.Items);
        Assert.Equal("invoice.paid", item.EventType);
    }

    [Fact]
    public async Task SearchByStatus_ReturnsMatches()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        await SeedAsync(repository);
        var service = new IncomingEventQueryService(repository);

        var result = await service.SearchAsync(new IncomingEventSearchRequestDto { TenantId = "tenant-1", Status = "Failed" });

        var item = Assert.Single(result.Items);
        Assert.Equal("Failed", item.Status);
    }

    [Fact]
    public async Task SearchByDateRange_ReturnsMatches()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        await SeedAsync(repository);
        var service = new IncomingEventQueryService(repository);

        var result = await service.SearchAsync(new IncomingEventSearchRequestDto
        {
            TenantId = "tenant-1",
            FromDate = new DateTime(2026, 4, 27, 10, 5, 0, DateTimeKind.Utc),
            ToDate = new DateTime(2026, 4, 27, 10, 20, 0, DateTimeKind.Utc),
        });

        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task SearchByCorrelationId_ReturnsMatches()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        await SeedAsync(repository);
        var service = new IncomingEventQueryService(repository);

        var result = await service.SearchAsync(new IncomingEventSearchRequestDto { TenantId = "tenant-1", CorrelationId = "corr-2" });

        var item = Assert.Single(result.Items);
        Assert.Equal("corr-2", item.CorrelationId);
    }

    [Fact]
    public async Task SearchReturnsNewestFirst()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        await SeedAsync(repository);
        var service = new IncomingEventQueryService(repository);

        var result = await service.SearchAsync(new IncomingEventSearchRequestDto { TenantId = "tenant-1" });

        Assert.True(result.Items.Zip(result.Items.Skip(1)).All(x => x.First.ReceivedAt >= x.Second.ReceivedAt));
    }

    [Fact]
    public async Task SearchLimitsTo500()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        for (var i = 0; i < 600; i++)
        {
            await repository.AddAsync(new IncomingEvent
            {
                Id = $"incoming-{i}",
                TenantId = "tenant-1",
                EventId = $"evt-{i}",
                EventType = "order.created",
                Payload = new { n = i },
                Status = "Accepted",
                ReceivedAt = new DateTime(2026, 4, 27, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i),
                CreatedAt = DateTime.UtcNow,
            });
        }

        var service = new IncomingEventQueryService(repository);

        var result = await service.SearchAsync(new IncomingEventSearchRequestDto { TenantId = "tenant-1" });

        Assert.Equal(500, result.PageSize);
    }

    [Fact]
    public async Task GetById_Success()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        await SeedAsync(repository);
        var service = new IncomingEventQueryService(repository);

        var result = await service.GetByIdAsync("incoming-1");

        Assert.NotNull(result);
        Assert.Equal("incoming-1", result!.Id);
    }

    [Fact]
    public async Task GetById_NotFound()
    {
        var repository = new InMemoryRepository<IncomingEvent>();
        var service = new IncomingEventQueryService(repository);

        var result = await service.GetByIdAsync("missing");

        Assert.Null(result);
    }

    private static async Task SeedAsync(InMemoryRepository<IncomingEvent> repository)
    {
        await repository.AddAsync(new IncomingEvent
        {
            Id = "incoming-1",
            TenantId = "tenant-1",
            EventId = "evt-1",
            EventType = "order.created",
            Payload = new { orderId = "1001" },
            Status = "Accepted",
            ReceivedAt = new DateTime(2026, 4, 27, 10, 0, 0, DateTimeKind.Utc),
            CorrelationId = "corr-1",
            CreatedAt = DateTime.UtcNow,
        });

        await repository.AddAsync(new IncomingEvent
        {
            Id = "incoming-2",
            TenantId = "tenant-1",
            EventId = "evt-2",
            EventType = "invoice.paid",
            Payload = new { invoiceId = "inv-1" },
            Status = "Delivered",
            ReceivedAt = new DateTime(2026, 4, 27, 10, 10, 0, DateTimeKind.Utc),
            CorrelationId = "corr-2",
            CreatedAt = DateTime.UtcNow,
        });

        await repository.AddAsync(new IncomingEvent
        {
            Id = "incoming-3",
            TenantId = "tenant-1",
            EventId = "evt-3",
            EventType = "order.cancelled",
            Payload = new { orderId = "1002" },
            Status = "Failed",
            ReceivedAt = new DateTime(2026, 4, 27, 10, 20, 0, DateTimeKind.Utc),
            CorrelationId = "corr-3",
            CreatedAt = DateTime.UtcNow,
        });

        await repository.AddAsync(new IncomingEvent
        {
            Id = "incoming-4",
            TenantId = "tenant-2",
            EventId = "evt-4",
            EventType = "order.created",
            Payload = new { orderId = "2001" },
            Status = "Accepted",
            ReceivedAt = new DateTime(2026, 4, 27, 10, 30, 0, DateTimeKind.Utc),
            CorrelationId = "corr-4",
            CreatedAt = DateTime.UtcNow,
        });
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


        public Task<(IReadOnlyList<T> Items, long TotalCount)> QueryAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, MongoDB.Driver.SortDefinition<T> sort, int skip, int limit, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            var filtered = _items.Where(compiled).ToList();
            var paged = filtered.Skip(skip).Take(limit).ToList();
            return Task.FromResult<(IReadOnlyList<T>, long)>((paged, filtered.LongCount()));
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
