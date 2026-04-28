using FluentValidation;
using HookBridge.Application.DTOs.Tenants;
using HookBridge.Application.Exceptions;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace HookBridge.Application.Services;

public sealed class TenantService(
    IMongoRepository<Tenant> tenantRepository,
    IAuditLogService auditLogService,
    IGuidGenerator guidGenerator,
    IDateTimeProvider dateTimeProvider,
    IValidator<CreateTenantRequestDto> createValidator,
    IValidator<UpdateTenantRequestDto> updateValidator,
    ILogger<TenantService> logger) : ITenantService
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
            NotificationEmails = request.NotificationEmails.ToList(),
            Status = TenantStatus.Active,
            CreatedAt = now,
            UpdatedAt = null,
        };

        await tenantRepository.AddAsync(tenant, cancellationToken);
        await TryAuditAsync(
            new AuditLog
            {
                TenantId = tenant.Id,
                Action = "TenantCreated",
                ResourceType = nameof(Tenant),
                ResourceId = tenant.Id,
                Description = $"Tenant '{tenant.Name}' created.",
                Metadata = new Dictionary<string, object?>
                {
                    ["name"] = tenant.Name,
                    ["slug"] = tenant.Slug,
                },
            },
            cancellationToken);
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
        tenant.NotificationEmails = request.NotificationEmails.ToList();
        tenant.UpdatedAt = dateTimeProvider.UtcNow;

        await tenantRepository.UpdateAsync(tenant, cancellationToken);
        await TryAuditAsync(
            new AuditLog
            {
                TenantId = tenant.Id,
                Action = "TenantUpdated",
                ResourceType = nameof(Tenant),
                ResourceId = tenant.Id,
                Description = $"Tenant '{tenant.Name}' updated.",
                Metadata = new Dictionary<string, object?>
                {
                    ["name"] = tenant.Name,
                    ["contactEmail"] = tenant.ContactEmail,
                    ["notificationEmails"] = tenant.NotificationEmails,
                },
            },
            cancellationToken);
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
        await TryAuditAsync(
            new AuditLog
            {
                TenantId = tenant.Id,
                Action = "TenantDisabled",
                ResourceType = nameof(Tenant),
                ResourceId = tenant.Id,
                Description = $"Tenant '{tenant.Name}' disabled.",
            },
            cancellationToken);
        return true;
    }

    private static TenantResponseDto Map(Tenant tenant) => new()
    {
        Id = tenant.Id,
        Name = tenant.Name,
        Slug = tenant.Slug,
        Status = tenant.Status,
        ContactEmail = tenant.ContactEmail,
        NotificationEmails = tenant.NotificationEmails.ToList(),
        CreatedAt = tenant.CreatedAt,
        UpdatedAt = tenant.UpdatedAt,
    };

    private async Task TryAuditAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        try
        {
            await auditLogService.LogAsync(auditLog, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audit logging failed for action {Action}.", auditLog.Action);
        }
    }
}
