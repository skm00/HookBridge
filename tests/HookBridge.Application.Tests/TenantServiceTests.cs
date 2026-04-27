using FluentValidation;
using FluentValidation.TestHelper;
using HookBridge.Application.DTOs.Tenants;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Services;
using HookBridge.Application.Validation.Tenants;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class TenantServiceTests
{
    [Fact]
    public async Task CreateTenant_Success()
    {
        var repo = new InMemoryTenantRepository();
        var service = CreateService(repo);

        var result = await service.CreateAsync(new CreateTenantRequestDto
        {
            Name = "Acme",
            Slug = "acme",
            ContactEmail = "admin@acme.com",
        });

        Assert.Equal("fixed-guid", result.Id);
        Assert.Equal(TenantStatus.Active, result.Status);
        Assert.Equal("acme", result.Slug);
    }

    [Fact]
    public async Task CreateTenant_DuplicateSlug_Fails()
    {
        var repo = new InMemoryTenantRepository();
        await repo.AddAsync(new Tenant { Id = "1", Name = "One", Slug = "acme", Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow });

        var service = CreateService(repo);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new CreateTenantRequestDto
        {
            Name = "Two",
            Slug = "acme",
        }));
    }

    [Fact]
    public async Task GetTenantById_ReturnsTenant()
    {
        var repo = new InMemoryTenantRepository();
        await repo.AddAsync(new Tenant { Id = "tenant-1", Name = "Acme", Slug = "acme", Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow });

        var service = CreateService(repo);
        var result = await service.GetByIdAsync("tenant-1");

        Assert.NotNull(result);
        Assert.Equal("tenant-1", result!.Id);
    }

    [Fact]
    public async Task UpdateTenant_UpdatesNameAndEmail()
    {
        var repo = new InMemoryTenantRepository();
        await repo.AddAsync(new Tenant { Id = "tenant-1", Name = "Old", Slug = "acme", ContactEmail = "old@acme.com", Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow });

        var service = CreateService(repo);
        var result = await service.UpdateAsync("tenant-1", new UpdateTenantRequestDto
        {
            Name = "New",
            ContactEmail = "new@acme.com",
        });

        Assert.NotNull(result);
        Assert.Equal("New", result!.Name);
        Assert.Equal("new@acme.com", result.ContactEmail);
    }

    [Fact]
    public async Task DisableTenant_SetsStatusDisabled()
    {
        var repo = new InMemoryTenantRepository();
        await repo.AddAsync(new Tenant { Id = "tenant-1", Name = "Acme", Slug = "acme", Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow });

        var service = CreateService(repo);
        var disabled = await service.DisableAsync("tenant-1");
        var after = await repo.GetByIdAsync("tenant-1");

        Assert.True(disabled);
        Assert.Equal(TenantStatus.Disabled, after!.Status);
    }

    [Fact]
    public void InvalidSlugValidation_Fails()
    {
        var validator = new CreateTenantRequestDtoValidator();
        var result = validator.TestValidate(new CreateTenantRequestDto
        {
            Name = "Acme",
            Slug = "Acme_Invalid",
        });

        result.ShouldHaveValidationErrorFor(x => x.Slug);
    }

    private static TenantService CreateService(InMemoryTenantRepository repo)
    {
        return new TenantService(
            repo,
            new FixedGuidGenerator(),
            new FixedDateTimeProvider(),
            new CreateTenantRequestDtoValidator(),
            new UpdateTenantRequestDtoValidator());
    }

    private sealed class FixedGuidGenerator : IGuidGenerator
    {
        public string NewGuid() => "fixed-guid";
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private sealed class InMemoryTenantRepository : IMongoRepository<Tenant>
    {
        private readonly List<Tenant> _items = [];

        public Task<Tenant?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<Tenant>> FindAsync(System.Linq.Expressions.Expression<Func<Tenant, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return Task.FromResult<IReadOnlyList<Tenant>>(_items.Where(compiled).ToList());
        }

        public Task<(IReadOnlyList<Tenant> Items, long TotalCount)> QueryAsync(System.Linq.Expressions.Expression<Func<Tenant, bool>> predicate, MongoDB.Driver.SortDefinition<Tenant> sort, int skip, int limit, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            var filtered = _items.Where(compiled).ToList();
            var paged = filtered.Skip(skip).Take(limit).ToList();
            return Task.FromResult<(IReadOnlyList<Tenant>, long)>((paged, filtered.LongCount()));
        }

        public Task<Tenant?> FirstOrDefaultAsync(System.Linq.Expressions.Expression<Func<Tenant, bool>> predicate, CancellationToken cancellationToken = default)
        {
            var compiled = predicate.Compile();
            return Task.FromResult(_items.FirstOrDefault(compiled));
        }

        public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Tenant>>(_items.ToList());

        public Task AddAsync(Tenant entity, CancellationToken cancellationToken = default)
        {
            _items.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Tenant entity, CancellationToken cancellationToken = default)
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
