using FluentValidation;
using HookBridge.Application.DTOs.Tenants;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;

namespace HookBridge.Application.Services;

public sealed class TenantService(
    IMongoRepository<Tenant> tenantRepository,
    IGuidGenerator guidGenerator,
    IDateTimeProvider dateTimeProvider,
    IValidator<CreateTenantRequestDto> createValidator,
    IValidator<UpdateTenantRequestDto> updateValidator) : ITenantService
{
    public async Task<TenantResponseDto> CreateAsync(CreateTenantRequestDto request, CancellationToken cancellationToken = default)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var existing = await tenantRepository.FirstOrDefaultAsync(x => x.Slug == request.Slug, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException($"Tenant slug '{request.Slug}' already exists.");
        }

        var now = dateTimeProvider.UtcNow;
        var tenant = new Tenant
        {
            Id = guidGenerator.NewGuid(),
            Name = request.Name,
            Slug = request.Slug,
            ContactEmail = request.ContactEmail,
            Status = TenantStatus.Active,
            CreatedAt = now,
            UpdatedAt = null,
        };

        await tenantRepository.AddAsync(tenant, cancellationToken);
        return Map(tenant);
    }

    public async Task<TenantResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(id, cancellationToken);
        return tenant is null ? null : Map(tenant);
    }

    public async Task<IReadOnlyList<TenantResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await tenantRepository.FindAsync(_ => true, cancellationToken);
        return tenants.Select(Map).ToList();
    }

    public async Task<TenantResponseDto?> UpdateAsync(string id, UpdateTenantRequestDto request, CancellationToken cancellationToken = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var tenant = await tenantRepository.GetByIdAsync(id, cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            tenant.Name = request.Name;
        }

        tenant.ContactEmail = request.ContactEmail;
        tenant.UpdatedAt = dateTimeProvider.UtcNow;

        await tenantRepository.UpdateAsync(tenant, cancellationToken);
        return Map(tenant);
    }

    public async Task<bool> DisableAsync(string id, CancellationToken cancellationToken = default)
    {
        var tenant = await tenantRepository.GetByIdAsync(id, cancellationToken);
        if (tenant is null)
        {
            return false;
        }

        tenant.Status = TenantStatus.Disabled;
        tenant.UpdatedAt = dateTimeProvider.UtcNow;

        await tenantRepository.UpdateAsync(tenant, cancellationToken);
        return true;
    }

    private static TenantResponseDto Map(Tenant tenant) => new()
    {
        Id = tenant.Id,
        Name = tenant.Name,
        Slug = tenant.Slug,
        Status = tenant.Status,
        ContactEmail = tenant.ContactEmail,
        CreatedAt = tenant.CreatedAt,
        UpdatedAt = tenant.UpdatedAt,
    };
}
