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
    public async Task RegisterAdmin_Success()
    {
        var tenantRepo = new InMemoryRepository<Tenant>();
        var adminRepo = new InMemoryRepository<AdminUser>();
        await tenantRepo.AddAsync(new Tenant { Id = "tenant-1", Name = "Acme", Slug = "acme", Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow });

        var service = BuildService(adminRepo, tenantRepo);

        var result = await service.RegisterAsync(new RegisterAdminRequestDto
        {
            TenantId = "tenant-1",
            Email = "admin@acme.com",
            Password = "Password123!",
            FullName = "Admin User",
        });

        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.Equal("tenant-1", result.User.TenantId);
        Assert.Equal(AdminRole.Viewer, result.User.Role);
    }

    [Fact]
    public async Task RegisterAdmin_Fails_WhenTenantMissing()
    {
        var service = BuildService(new InMemoryRepository<AdminUser>(), new InMemoryRepository<Tenant>());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RegisterAsync(new RegisterAdminRequestDto
        {
            TenantId = "tenant-missing",
            Email = "admin@acme.com",
            Password = "Password123!",
            FullName = "Admin User",
        }));
    }

    [Fact]
    public async Task RegisterAdmin_Fails_WhenDuplicateEmailInTenant()
    {
        var tenantRepo = new InMemoryRepository<Tenant>();
        var adminRepo = new InMemoryRepository<AdminUser>();
        await tenantRepo.AddAsync(new Tenant { Id = "tenant-1", Name = "Acme", Slug = "acme", Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow });
        await adminRepo.AddAsync(new AdminUser
        {
            Id = "admin-1",
            TenantId = "tenant-1",
            Email = "admin@acme.com",
            PasswordHash = "hash",
            FullName = "Existing",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        var service = BuildService(adminRepo, tenantRepo);

        await Assert.ThrowsAsync<ConflictException>(() => service.RegisterAsync(new RegisterAdminRequestDto
        {
            TenantId = "tenant-1",
            Email = "admin@acme.com",
            Password = "Password123!",
            FullName = "Admin User",
        }));
    }

    [Fact]
    public async Task Login_Success()
    {
        var tenantRepo = new InMemoryRepository<Tenant>();
        var adminRepo = new InMemoryRepository<AdminUser>();
        var hasher = new PasswordHasher();
        await tenantRepo.AddAsync(new Tenant { Id = "tenant-1", Name = "Acme", Slug = "acme", Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow });
        await adminRepo.AddAsync(new AdminUser
        {
            Id = "admin-1",
            TenantId = "tenant-1",
            Email = "admin@acme.com",
            PasswordHash = hasher.HashPassword("Password123!"),
            FullName = "Admin User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        var service = BuildService(adminRepo, tenantRepo);
        var result = await service.LoginAsync(new LoginRequestDto { Email = "admin@acme.com", Password = "Password123!" });

        Assert.False(string.IsNullOrWhiteSpace(result.Token));
    }

    [Fact]
    public async Task Login_Fails_WithWrongPassword()
    {
        var tenantRepo = new InMemoryRepository<Tenant>();
        var adminRepo = new InMemoryRepository<AdminUser>();
        var hasher = new PasswordHasher();
        await tenantRepo.AddAsync(new Tenant { Id = "tenant-1", Name = "Acme", Slug = "acme", Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow });
        await adminRepo.AddAsync(new AdminUser
        {
            Id = "admin-1",
            TenantId = "tenant-1",
            Email = "admin@acme.com",
            PasswordHash = hasher.HashPassword("Password123!"),
            FullName = "Admin User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        var service = BuildService(adminRepo, tenantRepo);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginAsync(new LoginRequestDto { Email = "admin@acme.com", Password = "WrongPassword" }));
        Assert.Equal("Invalid email or password.", ex.Message);
    }

    [Fact]
    public async Task Login_Fails_ForInactiveUser()
    {
        var adminRepo = new InMemoryRepository<AdminUser>();
        var hasher = new PasswordHasher();
        await adminRepo.AddAsync(new AdminUser
        {
            Id = "admin-1",
            TenantId = "tenant-1",
            Email = "admin@acme.com",
            PasswordHash = hasher.HashPassword("Password123!"),
            FullName = "Admin User",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        });

        var service = BuildService(adminRepo, new InMemoryRepository<Tenant>());

        await Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginAsync(new LoginRequestDto { Email = "admin@acme.com", Password = "Password123!" }));
    }

    [Fact]
    public async Task Jwt_ContainsTenantIdClaim()
    {
        var tenantRepo = new InMemoryRepository<Tenant>();
        var adminRepo = new InMemoryRepository<AdminUser>();
        await tenantRepo.AddAsync(new Tenant { Id = "tenant-1", Name = "Acme", Slug = "acme", Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow });

        var service = BuildService(adminRepo, tenantRepo);
        var result = await service.RegisterAsync(new RegisterAdminRequestDto
        {
            TenantId = "tenant-1",
            Email = "admin@acme.com",
            Password = "Password123!",
            FullName = "Admin User",
        });

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        var tenantIdClaim = token.Claims.FirstOrDefault(x => x.Type == "tenantId");

        Assert.Equal("tenant-1", tenantIdClaim?.Value);
    }

    [Fact]
    public async Task Jwt_ContainsRoleClaim()
    {
        var tenantRepo = new InMemoryRepository<Tenant>();
        var adminRepo = new InMemoryRepository<AdminUser>();
        await tenantRepo.AddAsync(new Tenant { Id = "tenant-1", Name = "Acme", Slug = "acme", Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow });

        var service = BuildService(adminRepo, tenantRepo);
        var result = await service.RegisterAsync(new RegisterAdminRequestDto
        {
            TenantId = "tenant-1",
            Email = "owner@acme.com",
            Password = "Password123!",
            FullName = "Owner User",
            Role = AdminRole.Owner,
        });

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        var roleClaim = token.Claims.FirstOrDefault(x => x.Type == "role");

        Assert.Equal(AdminRole.Owner.ToString(), roleClaim?.Value);
    }

    [Fact]
    public async Task RegisterAdmin_DefaultRole_IsViewer()
    {
        var tenantRepo = new InMemoryRepository<Tenant>();
        var adminRepo = new InMemoryRepository<AdminUser>();
        await tenantRepo.AddAsync(new Tenant { Id = "tenant-1", Name = "Acme", Slug = "acme", Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow });

        var service = BuildService(adminRepo, tenantRepo);
        var result = await service.RegisterAsync(new RegisterAdminRequestDto
        {
            TenantId = "tenant-1",
            Email = "viewer@acme.com",
            Password = "Password123!",
            FullName = "Viewer User",
        });

        Assert.Equal(AdminRole.Viewer, result.User.Role);
    }

    [Fact]
    public async Task RegisterAdmin_WithRole_StoresProvidedRole()
    {
        var tenantRepo = new InMemoryRepository<Tenant>();
        var adminRepo = new InMemoryRepository<AdminUser>();
        await tenantRepo.AddAsync(new Tenant { Id = "tenant-1", Name = "Acme", Slug = "acme", Status = TenantStatus.Active, CreatedAt = DateTime.UtcNow });

        var service = BuildService(adminRepo, tenantRepo);
        var result = await service.RegisterAsync(new RegisterAdminRequestDto
        {
            TenantId = "tenant-1",
            Email = "admin@acme.com",
            Password = "Password123!",
            FullName = "Admin User",
            Role = AdminRole.Admin,
        });

        var saved = await adminRepo.GetByIdAsync("fixed-guid");
        Assert.NotNull(saved);
        Assert.Equal(AdminRole.Admin, saved.Role);
        Assert.Equal(AdminRole.Admin, result.User.Role);
    }

    private static AuthService BuildService(InMemoryRepository<AdminUser> adminRepo, InMemoryRepository<Tenant> tenantRepo)
    {
        return new AuthService(
            adminRepo,
            tenantRepo,
            new FixedGuidGenerator(),
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
    }

    private sealed class FixedGuidGenerator : IGuidGenerator
    {
        public string NewGuid() => "fixed-guid";
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
