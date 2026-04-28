using HookBridge.Application.DTOs.Notifications;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Application.Services;
using HookBridge.Domain.Constants;
using HookBridge.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
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
            Severity = NotificationSeverities.Error,
            Title = "test",
            Message = "test",
            IsRead = false,
        });

        Assert.Single(fixture.Repository.Items);
        Assert.False(string.IsNullOrWhiteSpace(fixture.Repository.Items[0].Id));
    }

    [Fact]
    public async Task CriticalNotification_TriggersEmail()
    {
        var fixture = new Fixture();

        await fixture.Service.CreateAsync(new Notification
        {
            TenantId = "tenant-1",
            Type = "LimitExceeded",
            Severity = NotificationSeverities.Critical,
            Title = "critical",
            Message = "critical",
        });

        Assert.Single(fixture.EmailSender.Sent);
        Assert.Equal("alerts@tenant1.com", fixture.EmailSender.Sent[0].ToEmail);
    }

    [Fact]
    public async Task ErrorNotification_TriggersEmail()
    {
        var fixture = new Fixture();

        await fixture.Service.CreateAsync(new Notification
        {
            TenantId = "tenant-1",
            Type = "Failure",
            Severity = NotificationSeverities.Error,
            Title = "error",
            Message = "error",
        });

        Assert.Single(fixture.EmailSender.Sent);
    }


    [Fact]
    public async Task EmailNotificationFeatureDisabled_DoesNotSendEmail()
    {
        var fixture = new Fixture();
        fixture.FeatureFlagService.EnableEmailNotifications = false;

        await fixture.Service.CreateAsync(new Notification
        {
            TenantId = "tenant-1",
            Type = "Failure",
            Severity = NotificationSeverities.Error,
            Title = "error",
            Message = "error",
        });

        Assert.Empty(fixture.EmailSender.Sent);
    }

    [Fact]
    public async Task WarningNotification_DoesNotTriggerEmail()
    {
        var fixture = new Fixture();

        await fixture.Service.CreateAsync(new Notification
        {
            TenantId = "tenant-1",
            Type = "Warn",
            Severity = NotificationSeverities.Warning,
            Title = "warning",
            Message = "warning",
        });

        Assert.Empty(fixture.EmailSender.Sent);
    }

    [Fact]
    public async Task InfoNotification_DoesNotTriggerEmail()
    {
        var fixture = new Fixture();

        await fixture.Service.CreateAsync(new Notification
        {
            TenantId = "tenant-1",
            Type = "Info",
            Severity = NotificationSeverities.Info,
            Title = "info",
            Message = "info",
        });

        Assert.Empty(fixture.EmailSender.Sent);
    }

    [Fact]
    public async Task UsesNotificationEmails_WhenPresent()
    {
        var fixture = new Fixture();
        fixture.TenantRepository.Items[0].NotificationEmails = ["one@tenant.com", "two@tenant.com"];

        await fixture.Service.CreateAsync(new Notification
        {
            TenantId = "tenant-1",
            Type = "Failure",
            Severity = NotificationSeverities.Error,
            Title = "error",
            Message = "error",
        });

        Assert.Equal(2, fixture.EmailSender.Sent.Count);
        Assert.Contains(fixture.EmailSender.Sent, x => x.ToEmail == "one@tenant.com");
        Assert.Contains(fixture.EmailSender.Sent, x => x.ToEmail == "two@tenant.com");
    }

    [Fact]
    public async Task FallsBackToContactEmail_WhenNotificationEmailsEmpty()
    {
        var fixture = new Fixture();
        fixture.TenantRepository.Items[0].NotificationEmails = [];
        fixture.TenantRepository.Items[0].ContactEmail = "contact@tenant.com";

        await fixture.Service.CreateAsync(new Notification
        {
            TenantId = "tenant-1",
            Type = "Failure",
            Severity = NotificationSeverities.Error,
            Title = "error",
            Message = "error",
        });

        Assert.Single(fixture.EmailSender.Sent);
        Assert.Equal("contact@tenant.com", fixture.EmailSender.Sent[0].ToEmail);
    }

    [Fact]
    public async Task EmailFailure_DoesNotFailNotificationCreation()
    {
        var fixture = new Fixture();
        fixture.EmailSender.ShouldThrow = true;

        await fixture.Service.CreateAsync(new Notification
        {
            TenantId = "tenant-1",
            Type = "Failure",
            Severity = NotificationSeverities.Error,
            Title = "error",
            Message = "error",
        });

        Assert.Single(fixture.Repository.Items);
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
        public InMemoryTenantRepository TenantRepository { get; } = new();
        public RecordingEmailSender EmailSender { get; } = new();
        public ToggleFeatureFlagService FeatureFlagService { get; } = new();

        public NotificationService Service => new(
            Repository,
            new FakeGuidGenerator(),
            new FixedDateTimeProvider(),
            TenantRepository,
            EmailSender,
            FeatureFlagService,
            NullLogger<NotificationService>.Instance);

        public Fixture()
        {
            TenantRepository.Items.Add(new Tenant
            {
                Id = "tenant-1",
                Name = "Tenant 1",
                Slug = "tenant-1",
                ContactEmail = "fallback@tenant1.com",
                NotificationEmails = ["alerts@tenant1.com"],
            });
        }

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

    private sealed class InMemoryTenantRepository : IMongoRepository<Tenant>
    {
        public List<Tenant> Items { get; } = [];

        public Task<Tenant?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<Tenant>> FindAsync(System.Linq.Expressions.Expression<Func<Tenant, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Tenant>>(Items.Where(predicate.Compile()).ToList());

        public Task<(IReadOnlyList<Tenant> Items, long TotalCount)> QueryAsync(System.Linq.Expressions.Expression<Func<Tenant, bool>> predicate, SortDefinition<Tenant> sort, int skip, int limit, CancellationToken cancellationToken = default)
        {
            var filtered = Items.Where(predicate.Compile()).ToList();
            return Task.FromResult<(IReadOnlyList<Tenant>, long)>((filtered.Skip(skip).Take(limit).ToList(), filtered.LongCount()));
        }

        public Task<Tenant?> FirstOrDefaultAsync(System.Linq.Expressions.Expression<Func<Tenant, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(predicate.Compile()));

        public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Tenant>>(Items.ToList());

        public Task AddAsync(Tenant entity, CancellationToken cancellationToken = default)
        {
            Items.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Tenant entity, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class ToggleFeatureFlagService : IFeatureFlagService
    {
        public bool EnableEmailNotifications { get; set; } = true;

        public bool IsEnabled(string flagName)
            => !string.Equals(flagName, "EnableEmailNotifications", StringComparison.OrdinalIgnoreCase) || EnableEmailNotifications;

        public bool IsEnabled(string flagName, string tenantId) => IsEnabled(flagName);
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public bool ShouldThrow { get; set; }

        public List<(string ToEmail, string Subject, string HtmlBody)> Sent { get; } = [];

        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Simulated email failure");
            }

            Sent.Add((toEmail, subject, htmlBody));
            return Task.CompletedTask;
        }
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
