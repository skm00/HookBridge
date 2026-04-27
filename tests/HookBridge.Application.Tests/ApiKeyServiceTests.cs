using FluentValidation;
using HookBridge.Application.DTOs.ApiKeys;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Security;
using HookBridge.Application.Services;
using HookBridge.Application.Validation.ApiKeys;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class ApiKeyServiceTests
{
    [Fact]
    public async Task CreateApiKey_Success()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var apiKeyRepo = new InMemoryRepository<ApiKey>();
        var service = CreateService(apiKeyRepo, tenantRepo);

        var response = await service.CreateAsync("tenant-1", new CreateApiKeyRequestDto { Name = "Primary" });

        Assert.Equal("hb_live_test-value", response.PlainApiKey);
        Assert.Equal("Primary", response.ApiKey.Name);
        Assert.Equal("hb_live_test****", response.ApiKey.KeyPrefix);
    }

    [Fact]
    public async Task PlainApiKey_ReturnedOnlyOnCreationDto()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var apiKeyRepo = new InMemoryRepository<ApiKey>();
        var service = CreateService(apiKeyRepo, tenantRepo);

        await service.CreateAsync("tenant-1", new CreateApiKeyRequestDto { Name = "Primary" });
        var list = await service.GetByTenantAsync("tenant-1");

        Assert.NotEmpty(list);
        Assert.Null(typeof(ApiKeyResponseDto).GetProperty(nameof(CreateApiKeyResponseDto.PlainApiKey)));
    }

    [Fact]
    public async Task CreateApiKey_StoresHashNotPlainValue()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var apiKeyRepo = new InMemoryRepository<ApiKey>();
        var service = CreateService(apiKeyRepo, tenantRepo);

        var response = await service.CreateAsync("tenant-1", new CreateApiKeyRequestDto { Name = "Primary" });
        var stored = (await apiKeyRepo.FindAsync(_ => true)).Single();

        Assert.NotEqual(response.PlainApiKey, stored.KeyHash);
        Assert.Equal("hashed-hb_live_test-value", stored.KeyHash);
    }

    [Fact]
    public async Task Validate_ValidApiKey_ReturnsSuccess()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var apiKeyRepo = new InMemoryRepository<ApiKey>();
        var service = CreateService(apiKeyRepo, tenantRepo);

        var created = await service.CreateAsync("tenant-1", new CreateApiKeyRequestDto { Name = "Primary" });
        var result = await service.ValidateAsync("tenant-1", created.PlainApiKey);

        Assert.True(result.IsValid);
        Assert.Equal("tenant-1", result.TenantId);
        Assert.NotNull(result.ApiKeyId);
    }

    [Fact]
    public async Task Validate_InvalidApiKey_ReturnsFailure()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var apiKeyRepo = new InMemoryRepository<ApiKey>();
        var service = CreateService(apiKeyRepo, tenantRepo);

        await service.CreateAsync("tenant-1", new CreateApiKeyRequestDto { Name = "Primary" });
        var result = await service.ValidateAsync("tenant-1", "hb_live_wrong");

        Assert.False(result.IsValid);
        Assert.Equal("api_key_not_found", result.FailureReason);
    }

    [Fact]
    public async Task RevokeApiKey_SetsInactive()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var apiKeyRepo = new InMemoryRepository<ApiKey>();
        var service = CreateService(apiKeyRepo, tenantRepo);

        var created = await service.CreateAsync("tenant-1", new CreateApiKeyRequestDto { Name = "Primary" });
        var revoked = await service.RevokeAsync("tenant-1", created.ApiKey.Id);
        var stored = await apiKeyRepo.GetByIdAsync(created.ApiKey.Id);

        Assert.True(revoked);
        Assert.NotNull(stored);
        Assert.False(stored!.IsActive);
        Assert.NotNull(stored.RevokedAt);
    }

    [Fact]
    public async Task RevokedKey_CannotValidate()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Active);
        var apiKeyRepo = new InMemoryRepository<ApiKey>();
        var service = CreateService(apiKeyRepo, tenantRepo);

        var created = await service.CreateAsync("tenant-1", new CreateApiKeyRequestDto { Name = "Primary" });
        await service.RevokeAsync("tenant-1", created.ApiKey.Id);

        var result = await service.ValidateAsync("tenant-1", created.PlainApiKey);

        Assert.False(result.IsValid);
        Assert.Equal("api_key_revoked", result.FailureReason);
    }

    [Fact]
    public async Task DisabledTenant_CannotValidateApiKey()
    {
        var tenantRepo = BuildTenantRepo(TenantStatus.Disabled);
        var apiKeyRepo = new InMemoryRepository<ApiKey>();
        await apiKeyRepo.AddAsync(new ApiKey
        {
            Id = "key-1",
            TenantId = "tenant-1",
            Name = "Primary",
            KeyHash = "hashed-hb_live_test-value",
            KeyPrefix = "hb_live_test****",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        var service = CreateService(apiKeyRepo, tenantRepo);
        var result = await service.ValidateAsync("tenant-1", "hb_live_test-value");

        Assert.False(result.IsValid);
        Assert.Equal("tenant_inactive", result.FailureReason);
    }

    private static ApiKeyService CreateService(InMemoryRepository<ApiKey> apiKeyRepo, InMemoryRepository<Tenant> tenantRepo)
    {
        return new ApiKeyService(
            apiKeyRepo,
            tenantRepo,
            new FixedGuidGenerator(),
            new FixedDateTimeProvider(),
            new FixedApiKeyGenerator(),
            new FixedApiKeyHasher(),
            new CreateApiKeyRequestDtoValidator());
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
        private int _id = 0;
        public string NewGuid()
        {
            _id++;
            return $"key-{_id}";
        }
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FixedApiKeyGenerator : IApiKeyGenerator
    {
        public string Generate() => "hb_live_test-value";

        public string GetKeyPrefix(string plainApiKey) => "hb_live_test****";
    }

    private sealed class FixedApiKeyHasher : IApiKeyHasher
    {
        public string Hash(string plainApiKey) => $"hashed-{plainApiKey}";

        public bool Verify(string plainApiKey, string keyHash)
            => keyHash == Hash(plainApiKey);
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
