using System.Linq.Expressions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Domain.Entities;
using HookBridge.Infrastructure.Configuration;
using HookBridge.Infrastructure.Services;
using HookBridge.Infrastructure.Services.Auth;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class DemoDataSeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesDemoTenantAndAdminAndSubscriptions_AndIsIdempotent()
    {
        var fixture = BuildFixture();

        await fixture.Seeder.SeedAsync();
        await fixture.Seeder.SeedAsync();

        var tenant = Assert.Single(await fixture.Tenants.FindAsync(x => x.Slug == "demo-company"));
        Assert.Equal("Demo Company", tenant.Name);

        var admin = Assert.Single(await fixture.AdminUsers.FindAsync(x => x.TenantId == tenant.Id && x.Email == "demo@hookbridge.local"));
        Assert.True(admin.IsActive);

        var apiKey = Assert.Single(await fixture.ApiKeys.FindAsync(x => x.TenantId == tenant.Id));
        Assert.NotEqual("hb_live_demo_key_for_local_testing", apiKey.KeyHash);
        Assert.True(fixture.ApiKeyHasher.Verify("hb_live_demo_key_for_local_testing", apiKey.KeyHash));

        var subscriptions = await fixture.Subscriptions.FindAsync(x => x.TenantId == tenant.Id);
        Assert.Equal(3, subscriptions.Count);

        Assert.Equal(40, (await fixture.IncomingEvents.FindAsync(x => x.TenantId == tenant.Id)).Count);
        Assert.Equal(24, (await fixture.DeliveryAttempts.FindAsync(x => x.TenantId == tenant.Id)).Count);
        Assert.Equal(4, (await fixture.FailedEvents.FindAsync(x => x.TenantId == tenant.Id)).Count);
        Assert.Equal(3, (await fixture.Notifications.FindAsync(x => x.TenantId == tenant.Id)).Count);
        Assert.Equal(5, (await fixture.AuditLogs.FindAsync(x => x.TenantId == tenant.Id)).Count);
    }

    private static SeederFixture BuildFixture()
    {
        var options = Options.Create(new DemoDataSettings
        {
            Enabled = true,
            AdminEmail = "demo@hookbridge.local",
            AdminPassword = "DemoPassword123!",
            TenantName = "Demo Company",
            TenantSlug = "demo-company",
        });

        var tenants = new InMemoryRepository<Tenant>();
        var adminUsers = new InMemoryRepository<AdminUser>();
        var apiKeys = new InMemoryRepository<ApiKey>();
        var subscriptions = new InMemoryRepository<Subscription>();
        var incomingEvents = new InMemoryRepository<IncomingEvent>();
        var deliveryAttempts = new InMemoryRepository<DeliveryAttempt>();
        var failedEvents = new InMemoryRepository<FailedEvent>();
        var notifications = new InMemoryRepository<Notification>();
        var auditLogs = new InMemoryRepository<AuditLog>();
        var apiKeyHasher = new ApiKeyHasher();

        var seeder = new DemoDataSeeder(
            tenants,
            adminUsers,
            apiKeys,
            subscriptions,
            incomingEvents,
            deliveryAttempts,
            failedEvents,
            notifications,
            auditLogs,
            new PasswordHasher(),
            apiKeyHasher,
            new FixedDateTimeProvider(),
            new SequentialGuidGenerator(),
            new DevelopmentHostEnvironment(),
            options,
            NullLogger<DemoDataSeeder>.Instance);

        return new SeederFixture(
            seeder,
            tenants,
            adminUsers,
            apiKeys,
            subscriptions,
            incomingEvents,
            deliveryAttempts,
            failedEvents,
            notifications,
            auditLogs,
            apiKeyHasher);
    }

    private sealed record SeederFixture(
        DemoDataSeeder Seeder,
        InMemoryRepository<Tenant> Tenants,
        InMemoryRepository<AdminUser> AdminUsers,
        InMemoryRepository<ApiKey> ApiKeys,
        InMemoryRepository<Subscription> Subscriptions,
        InMemoryRepository<IncomingEvent> IncomingEvents,
        InMemoryRepository<DeliveryAttempt> DeliveryAttempts,
        InMemoryRepository<FailedEvent> FailedEvents,
        InMemoryRepository<Notification> Notifications,
        InMemoryRepository<AuditLog> AuditLogs,
        ApiKeyHasher ApiKeyHasher);

    private sealed class InMemoryRepository<T> : IMongoRepository<T>
        where T : BaseEntity
    {
        private readonly List<T> _items = [];

        public Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<T>>(_items.AsQueryable().Where(predicate).ToList());

        public Task<(IReadOnlyList<T> Items, long TotalCount)> QueryAsync(Expression<Func<T, bool>> predicate, SortDefinition<T> sort, int skip, int limit, CancellationToken cancellationToken = default)
        {
            var filtered = _items.AsQueryable().Where(predicate).Skip(skip).Take(limit).ToList();
            var total = _items.AsQueryable().Count(predicate);
            return Task.FromResult(((IReadOnlyList<T>)filtered, (long)total));
        }

        public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.AsQueryable().FirstOrDefault(predicate));

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

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 4, 28, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class SequentialGuidGenerator : IGuidGenerator
    {
        private int _counter;
        public string NewGuid() => $"demo-{++_counter:D4}";
    }

    private sealed class DevelopmentHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "HookBridge";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    }
}
