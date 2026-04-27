using FluentValidation;
using HookBridge.Application.DTOs.Subscriptions;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Services;
using HookBridge.Application.Validation.Subscriptions;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class SubscriptionServiceTests
{
    [Fact]
    public async Task CreateSubscription_Success()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        var result = await service.CreateAsync(new CreateSubscriptionRequestDto
        {
            TenantId = "tenant-1",
            EventType = "order.created",
            TargetUrl = "https://example.com/hooks",
            RetryPolicy = new RetryPolicyDto
            {
                MaxAttempts = 5,
                InitialDelaySeconds = 45,
                BackoffType = "Fixed",
            },
            TimeoutSeconds = 40,
        });

        Assert.Equal("subscription-1", result.Id);
        Assert.True(result.IsActive);
        Assert.Equal("order.created", result.EventType);
    }

    [Fact]
    public async Task CreateSubscription_FailsIfTenantNotFound()
    {
        var tenantRepo = new InMemoryRepository<Tenant>();
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateAsync(BuildValidRequest()));
    }

    [Fact]
    public async Task CreateSubscription_FailsIfTenantDisabled()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Disabled);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(BuildValidRequest()));
    }

    [Fact]
    public async Task InvalidTargetUrl_ValidationFails()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        var request = BuildValidRequest();
        request.TargetUrl = "/not-absolute";

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(request));
    }

    [Fact]
    public async Task DuplicateHeaderNames_ValidationFails()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        var request = BuildValidRequest();
        request.Headers =
        [
            new KeyValueDto { Name = "x-signature", Value = "1" },
            new KeyValueDto { Name = "X-Signature", Value = "2" },
        ];

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(request));
    }

    [Fact]
    public async Task DefaultRetryPolicy_IsApplied()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        var request = BuildValidRequest();
        request.RetryPolicy = null;
        request.TimeoutSeconds = null;

        var result = await service.CreateAsync(request);

        Assert.Equal(3, result.RetryPolicy.MaxAttempts);
        Assert.Equal(30, result.RetryPolicy.InitialDelaySeconds);
        Assert.Equal("Exponential", result.RetryPolicy.BackoffType);
        Assert.Equal(30, result.TimeoutSeconds);
    }

    [Fact]
    public async Task GetSubscriptionById_ReturnsSubscription()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        var created = await service.CreateAsync(BuildValidRequest());

        var fetched = await service.GetByIdAsync(created.Id);

        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task SearchSubscription_ByTenantId()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        await tenantRepo.AddAsync(new Tenant { Id = "tenant-2", Name = "Two", Slug = "two", Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow });

        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        await service.CreateAsync(BuildValidRequest());

        var second = BuildValidRequest();
        second.TenantId = "tenant-2";
        second.TargetUrl = "https://example.com/two";
        second.EventType = "order.updated";
        await service.CreateAsync(second);

        var results = await service.SearchAsync(new SubscriptionSearchRequestDto { TenantId = "tenant-1" });

        Assert.Single(results);
        Assert.Equal("tenant-1", results[0].TenantId);
    }

    [Fact]
    public async Task SearchSubscription_ByEventType()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        await service.CreateAsync(BuildValidRequest());

        var second = BuildValidRequest();
        second.EventType = "order.cancelled";
        second.TargetUrl = "https://example.com/cancel";
        await service.CreateAsync(second);

        var results = await service.SearchAsync(new SubscriptionSearchRequestDto { EventType = "order.cancelled" });

        Assert.Single(results);
        Assert.Equal("order.cancelled", results[0].EventType);
    }

    [Fact]
    public async Task SecretValues_AreMaskedInResponse()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        var request = BuildValidRequest();
        request.Authentication = new AuthenticationDto
        {
            Type = "Basic",
            Basic = new BasicAuthDto
            {
                Username = "user",
                Password = "super-secret",
            },
        };

        var created = await service.CreateAsync(request);

        Assert.NotNull(created.Authentication);
        Assert.Equal("********", created.Authentication!.Basic!.Password);
    }

    [Fact]
    public async Task UpdateSubscription_Success()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        var created = await service.CreateAsync(BuildValidRequest());

        var updated = await service.UpdateAsync(created.Id, new UpdateSubscriptionRequestDto
        {
            EventType = "order.updated",
            TargetUrl = "https://example.com/updated",
            TimeoutSeconds = 60,
        });

        Assert.NotNull(updated);
        Assert.Equal("order.updated", updated!.EventType);
        Assert.Equal("https://example.com/updated", updated.TargetUrl);
        Assert.Equal(60, updated.TimeoutSeconds);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdateSubscription_OnlyProvidedFieldsAreChanged()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        var created = await service.CreateAsync(BuildValidRequest());

        var updated = await service.UpdateAsync(created.Id, new UpdateSubscriptionRequestDto
        {
            TargetUrl = "https://example.com/new-target",
        });

        Assert.NotNull(updated);
        Assert.Equal("order.created", updated!.EventType);
        Assert.Equal("https://example.com/new-target", updated.TargetUrl);
        Assert.Equal(30, updated.TimeoutSeconds);
    }

    [Fact]
    public async Task UpdateSubscription_ReturnsNull_WhenNotFound()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        var updated = await service.UpdateAsync("missing", new UpdateSubscriptionRequestDto
        {
            EventType = "order.updated",
        });

        Assert.Null(updated);
    }

    [Fact]
    public async Task UpdateSubscription_InvalidTargetUrl_FailsValidation()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        var created = await service.CreateAsync(BuildValidRequest());

        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(created.Id, new UpdateSubscriptionRequestDto
        {
            TargetUrl = "/invalid",
        }));
    }

    [Fact]
    public async Task UpdateSubscription_DuplicateHeaders_FailsValidation()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        var created = await service.CreateAsync(BuildValidRequest());

        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(created.Id, new UpdateSubscriptionRequestDto
        {
            Headers =
            [
                new KeyValueDto { Name = "x-test", Value = "1" },
                new KeyValueDto { Name = "X-Test", Value = "2" },
            ],
        }));
    }

    [Fact]
    public async Task DeleteSubscription_Success()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        var created = await service.CreateAsync(BuildValidRequest());

        var deleted = await service.DeleteAsync(created.Id);
        var fetched = await service.GetByIdAsync(created.Id);

        Assert.True(deleted);
        Assert.Null(fetched);
    }

    [Fact]
    public async Task DeleteSubscription_ReturnsFalse_WhenNotFound()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);

        var deleted = await service.DeleteAsync("missing");

        Assert.False(deleted);
    }

    [Fact]
    public async Task DisableSubscription_SetsInactiveAndDisabledAt()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        var created = await service.CreateAsync(BuildValidRequest());

        var disabled = await service.DisableAsync(created.Id);
        var fetched = await service.GetByIdAsync(created.Id);

        Assert.True(disabled);
        Assert.NotNull(fetched);
        Assert.False(fetched!.IsActive);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), fetched.DisabledAt);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), fetched.UpdatedAt);
    }

    [Fact]
    public async Task EnableSubscription_SetsActiveAndClearsDisabledAt()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        var created = await service.CreateAsync(BuildValidRequest());
        await service.DisableAsync(created.Id);

        var enabled = await service.EnableAsync(created.Id);
        var fetched = await service.GetByIdAsync(created.Id);

        Assert.True(enabled);
        Assert.NotNull(fetched);
        Assert.True(fetched!.IsActive);
        Assert.Null(fetched.DisabledAt);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), fetched.UpdatedAt);
    }

    [Fact]
    public async Task SecretValues_AreMaskedAfterUpdate()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var subscriptionRepo = new InMemoryRepository<Subscription>();
        var service = CreateService(subscriptionRepo, tenantRepo);
        var created = await service.CreateAsync(BuildValidRequest());

        var updated = await service.UpdateAsync(created.Id, new UpdateSubscriptionRequestDto
        {
            Authentication = new AuthenticationDto
            {
                Type = "ApiKeyHeader",
                ApiKeyHeader = new ApiKeyHeaderDto
                {
                    HeaderName = "x-api-key",
                    HeaderValue = "new-super-secret",
                },
            },
        });

        Assert.NotNull(updated);
        Assert.NotNull(updated!.Authentication);
        Assert.Equal("********", updated.Authentication!.ApiKeyHeader!.HeaderValue);
    }

    private static CreateSubscriptionRequestDto BuildValidRequest() => new()
    {
        TenantId = "tenant-1",
        EventType = "order.created",
        TargetUrl = "https://example.com/hooks",
        RetryPolicy = new RetryPolicyDto
        {
            MaxAttempts = 3,
            InitialDelaySeconds = 30,
            BackoffType = "Exponential",
        },
        TimeoutSeconds = 30,
    };

    private static SubscriptionService CreateService(InMemoryRepository<Subscription> subscriptionRepo, InMemoryRepository<Tenant> tenantRepo)
    {
        return new SubscriptionService(
            subscriptionRepo,
            tenantRepo,
            new FixedGuidGenerator(),
            new FixedDateTimeProvider(),
            new CreateSubscriptionRequestDtoValidator(),
            new UpdateSubscriptionRequestDtoValidator(),
            NullLogger<SubscriptionService>.Instance);
    }

    private static InMemoryRepository<Tenant> BuildTenantRepo(TenantStatus status)
    {
        var repo = new InMemoryRepository<Tenant>();
        repo.AddAsync(new Tenant
        {
            Id = "tenant-1",
            Name = "Acme",
            Slug = "acme",
            Status = status,
            CreatedAt = DateTime.UtcNow,
        }).GetAwaiter().GetResult();

        return repo;
    }

    private sealed class FixedGuidGenerator : IGuidGenerator
    {
        private int _counter;

        public string NewGuid()
        {
            _counter++;
            return $"subscription-{_counter}";
        }
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
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
