using System.IO.Compression;
using System.Linq.Expressions;
using System.Text;
using FluentValidation;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Services;
using HookBridge.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class BackupServiceTests
{
    [Fact]
    public async Task Export_ReturnsData()
    {
        var fixture = CreateFixture();

        var bytes = await fixture.Service.ExportAsync("tenant-1", CancellationToken.None);

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task Export_ExcludesPlainSecrets()
    {
        var fixture = CreateFixture();

        var bytes = await fixture.Service.ExportAsync("tenant-1", CancellationToken.None);
        var json = ReadJson(bytes);

        Assert.DoesNotContain("hb_live_plain_api_key", json, StringComparison.Ordinal);
        Assert.DoesNotContain("oauth-plain-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("hmac-plain-secret", json, StringComparison.Ordinal);
        Assert.Contains("hashed-key-value", json, StringComparison.Ordinal);
        Assert.Contains("enc-oauth-secret", json, StringComparison.Ordinal);
        Assert.Contains("enc-hmac-secret", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Import_RejectsMismatchedTenantId()
    {
        var fixture = CreateFixture();
        var bytes = await fixture.Service.ExportAsync("tenant-1", CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.ImportAsync("tenant-2", bytes, CancellationToken.None));
    }

    [Fact]
    public async Task Import_InsertsRecords()
    {
        var source = CreateFixture();
        var bytes = await source.Service.ExportAsync("tenant-1", CancellationToken.None);

        var destination = CreateEmptyFixture();
        await destination.Service.ImportAsync("tenant-1", bytes, CancellationToken.None);

        Assert.Single(await destination.TenantRepo.GetAllAsync());
        Assert.Single(await destination.SubscriptionRepo.GetAllAsync());
        Assert.Single(await destination.ApiKeyRepo.GetAllAsync());
        Assert.Single(await destination.EventRepo.GetAllAsync());
        Assert.Single(await destination.FailedEventRepo.GetAllAsync());
        Assert.Single(await destination.NotificationRepo.GetAllAsync());
        Assert.Single(await destination.AuditLogRepo.GetAllAsync());
    }

    private static string ReadJson(byte[] gz)
    {
        using var ms = new MemoryStream(gz);
        using var gzip = new GZipStream(ms, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static Fixture CreateFixture()
    {
        var f = CreateEmptyFixture();
        var now = DateTime.UtcNow;

        f.TenantRepo.AddAsync(new Tenant { Id = "tenant-1", Name = "Acme", Slug = "acme", CreatedAt = now }).GetAwaiter().GetResult();
        f.SubscriptionRepo.AddAsync(new Subscription
        {
            Id = "sub-1",
            TenantId = "tenant-1",
            EventType = "order.created",
            TargetUrl = "https://example.com",
            Authentication = new AuthenticationConfig
            {
                Type = "OAuth2ClientCredentials",
                OAuth2 = new OAuth2ClientCredentialsConfig
                {
                    TokenUrl = "https://example.com/token",
                    ClientId = "client-1",
                    ClientSecret = "enc-oauth-secret",
                },
                HmacSignature = new HmacSignatureConfig
                {
                    Secret = "enc-hmac-secret",
                    HeaderName = "X-Signature",
                    Algorithm = "SHA256",
                },
            },
            CreatedAt = now,
        }).GetAwaiter().GetResult();
        f.ApiKeyRepo.AddAsync(new ApiKey { Id = "key-1", TenantId = "tenant-1", Name = "Primary", KeyHash = "hashed-key-value", KeyPrefix = "hb_live_test****", IsActive = true, CreatedAt = now }).GetAwaiter().GetResult();
        f.EventRepo.AddAsync(new IncomingEvent { Id = "evt-1", TenantId = "tenant-1", EventId = "evt-1", EventType = "order.created", Payload = new { sample = "value" }, ReceivedAt = now, Status = "Accepted", CreatedAt = now }).GetAwaiter().GetResult();
        f.FailedEventRepo.AddAsync(new FailedEvent { Id = "failed-1", TenantId = "tenant-1", EventId = "evt-1", SubscriptionId = "sub-1", EventType = "order.created", TargetUrl = "https://example.com", Reason = "timeout", Status = "DLQ", FailedAt = now, CreatedAt = now }).GetAwaiter().GetResult();
        f.NotificationRepo.AddAsync(new Notification { Id = "notif-1", TenantId = "tenant-1", Type = "dlq", Severity = "error", Title = "DLQ", Message = "failed", CreatedAt = now }).GetAwaiter().GetResult();
        f.AuditLogRepo.AddAsync(new AuditLog { Id = "audit-1", TenantId = "tenant-1", Action = "Backup", ResourceType = "Tenant", CreatedAt = now }).GetAwaiter().GetResult();

        return f;
    }

    private static Fixture CreateEmptyFixture()
    {
        var tenantRepo = new InMemoryRepository<Tenant>();
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var apiKeyRepo = new InMemoryRepository<ApiKey>();
        var eventRepo = new InMemoryRepository<IncomingEvent>();
        var failedEventRepo = new InMemoryRepository<FailedEvent>();
        var notificationRepo = new InMemoryRepository<Notification>();
        var auditLogRepo = new InMemoryRepository<AuditLog>();

        var service = new BackupService(
            tenantRepo,
            subscriptionRepo,
            apiKeyRepo,
            eventRepo,
            failedEventRepo,
            notificationRepo,
            auditLogRepo,
            new FixedDateTimeProvider(),
            NullLogger<BackupService>.Instance);

        return new Fixture(service, tenantRepo, subscriptionRepo, apiKeyRepo, eventRepo, failedEventRepo, notificationRepo, auditLogRepo);
    }

    private sealed record Fixture(
        BackupService Service,
        InMemoryRepository<Tenant> TenantRepo,
        InMemoryRepository<Subscription> SubscriptionRepo,
        InMemoryRepository<ApiKey> ApiKeyRepo,
        InMemoryRepository<IncomingEvent> EventRepo,
        InMemoryRepository<FailedEvent> FailedEventRepo,
        InMemoryRepository<Notification> NotificationRepo,
        InMemoryRepository<AuditLog> AuditLogRepo);

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private sealed class InMemoryRepository<T> : IMongoRepository<T> where T : BaseEntity
    {
        private readonly List<T> _items = [];

        public Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(i => i.Id == id));

        public Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<T>>(_items.AsQueryable().Where(predicate).ToList());

        public Task<(IReadOnlyList<T> Items, long TotalCount)> QueryAsync(Expression<Func<T, bool>> predicate, SortDefinition<T> sort, int skip, int limit, CancellationToken cancellationToken = default)
        {
            var filtered = _items.AsQueryable().Where(predicate).ToList();
            var paged = filtered.Skip(skip).Take(limit).ToList();
            return Task.FromResult(((IReadOnlyList<T>)paged, (long)filtered.Count));
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
}
