using System.IdentityModel.Tokens.Jwt;
using FluentValidation;
using HookBridge.Application.DTOs.Auth;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Validation.Auth;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.Configuration;
using HookBridge.Infrastructure.Services.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace HookBridge.Application.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task RegisterAdmin_CreatesTenantAndOwner()
    {
        var tenantRepo = new InMemoryRepository<Tenant>();
        var adminRepo = new InMemoryRepository<AdminUser>();
        var service = BuildService(adminRepo, tenantRepo);

        var result = await service.RegisterAsync(new RegisterAdminRequestDto
        {
            Email = "admin@acme.com",
            Password = "Password123!",
            OrganizationName = "Acme",
        });

        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.Equal("tenant-1", result.User.TenantId);
        Assert.Equal(AdminRole.Owner, result.User.Role);

        var createdTenant = await tenantRepo.GetByIdAsync("tenant-1");
        Assert.NotNull(createdTenant);
        Assert.Equal("Acme", createdTenant.Name);
    }

    [Fact]
    public async Task RegisterAdmin_Fails_WhenDuplicateEmail()
    {
        var service = BuildService(new InMemoryRepository<AdminUser>(new AdminUser
        {
            Id = "admin-1",
            TenantId = "tenant-0",
            Email = "admin@acme.com",
            PasswordHash = "hash",
            FullName = "Existing",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        }), new InMemoryRepository<Tenant>());

        await Assert.ThrowsAsync<ConflictException>(() => service.RegisterAsync(new RegisterAdminRequestDto
        {
            Email = "admin@acme.com",
            Password = "Password123!",
        }));
    }

    [Fact]
    public async Task RegisterAdmin_OrganizationNameOptional_Works()
    {
        var service = BuildService(new InMemoryRepository<AdminUser>(), new InMemoryRepository<Tenant>());

        var result = await service.RegisterAsync(new RegisterAdminRequestDto
        {
            Email = "owner@acme.com",
            Password = "Password123!",
        });

        Assert.Equal("Acme", result.User.OrganizationName);
    }

    [Fact]
    public async Task Jwt_ContainsTenantAndRoleClaims_OnRegister()
    {
        var service = BuildService(new InMemoryRepository<AdminUser>(), new InMemoryRepository<Tenant>());
        var result = await service.RegisterAsync(new RegisterAdminRequestDto { Email = "admin@acme.com", Password = "Password123!" });

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        Assert.Equal("tenant-1", token.Claims.FirstOrDefault(x => x.Type == "tenantId")?.Value);
        Assert.Equal(AdminRole.Owner.ToString(), token.Claims.FirstOrDefault(x => x.Type == "role")?.Value);
    }

    private static AuthService BuildService(InMemoryRepository<AdminUser> adminRepo, InMemoryRepository<Tenant> tenantRepo)
        => new(
            adminRepo,
            tenantRepo,
            new SequencedGuidGenerator(),
            new FixedDateTimeProvider(),
            new PasswordHasher(),
            new RegisterAdminRequestDtoValidator(),
            new LoginRequestDtoValidator(),
            Options.Create(new JwtSettings
            {
                Issuer = "test-issuer",
                Audience = "test-audience",
                Secret = "test-secret-key-with-at-least-32-chars",
                ExpiryMinutes = 60,
            }));

    private sealed class SequencedGuidGenerator : IGuidGenerator
    {
        private int _counter;
        public string NewGuid() => $"tenant-{++_counter}";
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private sealed class InMemoryRepository<T>(params T[] seed) : IMongoRepository<T> where T : BaseEntity
    {
        private readonly List<T> _items = seed.ToList();
        public Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<T>> FindAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<T>>(_items.Where(predicate.Compile()).ToList());
        public Task<(IReadOnlyList<T> Items, long TotalCount)> QueryAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, MongoDB.Driver.SortDefinition<T> sort, int skip, int limit, CancellationToken cancellationToken = default) { var f = _items.Where(predicate.Compile()).ToList(); return Task.FromResult<(IReadOnlyList<T>, long)>((f.Skip(skip).Take(limit).ToList(), f.LongCount())); }
        public Task<T?> FirstOrDefaultAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult(_items.FirstOrDefault(predicate.Compile()));
        public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<T>>(_items.ToList());
        public Task AddAsync(T entity, CancellationToken cancellationToken = default) { _items.Add(entity); return Task.CompletedTask; }
        public Task UpdateAsync(T entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
