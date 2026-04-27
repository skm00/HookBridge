using HookBridge.Application.DTOs.Notifications;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Services;
using HookBridge.Domain.Entities;
using MongoDB.Driver;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task CreateNotification_Success()
    {
        var fixture = new Fixture();

        await fixture.Service.CreateAsync(new Notification
        {
            TenantId = "tenant-1",
            Type = "DlqCreated",
            Severity = "Error",
            Title = "test",
            Message = "test",
            IsRead = false,
        });

        Assert.Single(fixture.Repository.Items);
        Assert.False(string.IsNullOrWhiteSpace(fixture.Repository.Items[0].Id));
    }

    [Fact]
    public async Task Search_ByType_ReturnsMatchingItems()
    {
        var fixture = new Fixture();
        fixture.Seed();

        var result = await fixture.Service.SearchAsync(new NotificationSearchRequestDto { TenantId = "tenant-1", Type = "DlqCreated" });

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, x => Assert.Equal("DlqCreated", x.Type));
    }

    [Fact]
    public async Task Search_BySeverity_ReturnsMatchingItems()
    {
        var fixture = new Fixture();
        fixture.Seed();

        var result = await fixture.Service.SearchAsync(new NotificationSearchRequestDto { TenantId = "tenant-1", Severity = "Critical" });

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, x => Assert.Equal("Critical", x.Severity));
    }

    [Fact]
    public async Task Search_ByUnreadStatus_ReturnsMatchingItems()
    {
        var fixture = new Fixture();
        fixture.Seed();

        var result = await fixture.Service.SearchAsync(new NotificationSearchRequestDto { TenantId = "tenant-1", IsRead = false });

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, x => Assert.False(x.IsRead));
    }

    [Fact]
    public async Task MarkAsRead_SetsIsReadAndReadAt()
    {
        var fixture = new Fixture();
        fixture.Seed();

        var marked = await fixture.Service.MarkAsReadAsync("notif-1");
        var item = fixture.Repository.Items.Single(x => x.Id == "notif-1");

        Assert.True(marked);
        Assert.True(item.IsRead);
        Assert.Equal(new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc), item.ReadAt);
    }

    [Fact]
    public async Task UnreadCount_ReturnsTenantUnreadOnly()
    {
        var fixture = new Fixture();
        fixture.Seed();

        var unreadCount = await fixture.Service.GetUnreadCountAsync("tenant-1");

        Assert.Equal(1, unreadCount);
    }

    private sealed class Fixture
    {
        public InMemoryNotificationRepository Repository { get; } = new();
        public NotificationService Service => new(Repository, new FakeGuidGenerator(), new FixedDateTimeProvider());

        public void Seed()
        {
            Repository.Items.Add(new Notification
            {
                Id = "notif-1",
                TenantId = "tenant-1",
                Type = "DlqCreated",
                Severity = "Error",
                Title = "t1",
                Message = "m1",
                IsRead = false,
                CreatedAt = new DateTime(2026, 4, 27, 10, 0, 0, DateTimeKind.Utc),
            });

            Repository.Items.Add(new Notification
            {
                Id = "notif-2",
                TenantId = "tenant-1",
                Type = "UsageLimitExceeded",
                Severity = "Critical",
                Title = "t2",
                Message = "m2",
                IsRead = true,
                ReadAt = new DateTime(2026, 4, 27, 11, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 4, 27, 11, 0, 0, DateTimeKind.Utc),
            });

            Repository.Items.Add(new Notification
            {
                Id = "notif-3",
                TenantId = "tenant-2",
                Type = "DlqCreated",
                Severity = "Critical",
                Title = "t3",
                Message = "m3",
                IsRead = false,
                CreatedAt = new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc),
            });
        }
    }

    private sealed class InMemoryNotificationRepository : INotificationRepository
    {
        public List<Notification> Items { get; } = [];

        public Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            Items.Add(notification);
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<Notification> Items, long TotalCount)> SearchAsync(NotificationSearchRequestDto request, SortDefinition<Notification> sort, int skip, int limit, CancellationToken cancellationToken = default)
        {
            IEnumerable<Notification> query = Items;

            if (!string.IsNullOrWhiteSpace(request.TenantId))
            {
                query = query.Where(x => x.TenantId == request.TenantId);
            }

            if (!string.IsNullOrWhiteSpace(request.Type))
            {
                query = query.Where(x => x.Type == request.Type);
            }

            if (!string.IsNullOrWhiteSpace(request.Severity))
            {
                query = query.Where(x => x.Severity == request.Severity);
            }

            if (request.IsRead.HasValue)
            {
                query = query.Where(x => x.IsRead == request.IsRead.Value);
            }

            var ordered = query.OrderByDescending(x => x.CreatedAt).Skip(skip).Take(limit).ToList();
            return Task.FromResult<(IReadOnlyList<Notification>, long)>((ordered, query.LongCount()));
        }

        public Task<Notification?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

        public Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> GetUnreadCountAsync(string tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Count(x => x.TenantId == tenantId && !x.IsRead));

        public Task<bool> ExistsAsync(string tenantId, string type, DateTime fromInclusive, DateTime toExclusive, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(x => x.TenantId == tenantId && x.Type == type && x.CreatedAt >= fromInclusive && x.CreatedAt < toExclusive));
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FakeGuidGenerator : IGuidGenerator
    {
        public string NewGuid() => "generated-id";
    }
}
