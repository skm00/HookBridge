using HookBridge.Application.Common;
using HookBridge.Application.DTOs.AuditLogs;
using HookBridge.Application.DTOs.Common;
using HookBridge.Application.Interfaces;
using HookBridge.Application.Interfaces.Persistence;
using HookBridge.Application.Interfaces.Services;
using HookBridge.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace HookBridge.Infrastructure.Services;

public sealed class AuditLogService(
    IMongoRepository<AuditLog> auditLogRepository,
    ICurrentUserContext currentUserContext,
    IHttpContextAccessor httpContextAccessor,
    IGuidGenerator guidGenerator,
    IDateTimeProvider dateTimeProvider,
    ILogger<AuditLogService> logger) : IAuditLogService
{
    public async Task LogAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        auditLog.Id = string.IsNullOrWhiteSpace(auditLog.Id) ? guidGenerator.NewGuid() : auditLog.Id;
        auditLog.TenantId = string.IsNullOrWhiteSpace(auditLog.TenantId) ? (currentUserContext.TenantId ?? string.Empty) : auditLog.TenantId;
        auditLog.UserId ??= currentUserContext.UserId;
        auditLog.UserEmail ??= currentUserContext.Email;
        auditLog.CreatedAt = auditLog.CreatedAt == default ? dateTimeProvider.UtcNow : auditLog.CreatedAt;
        auditLog.UpdatedAt = null;

        var httpContext = httpContextAccessor.HttpContext;
        auditLog.IpAddress ??= httpContext?.Connection.RemoteIpAddress?.ToString();
        auditLog.UserAgent ??= httpContext?.Request.Headers.UserAgent.ToString();

        var metadata = AuditMetadataSanitizer.Sanitize(auditLog.Metadata);
        var metadataWithRole = EnsureRole(metadata, currentUserContext.Role);
        auditLog.Metadata = metadataWithRole;

        await auditLogRepository.AddAsync(auditLog, cancellationToken);
    }

    public async Task<PagedResponseDto<AuditLogResponseDto>> SearchAsync(AuditLogSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = await auditLogRepository.QueryAsync(
            x =>
                (string.IsNullOrWhiteSpace(request.TenantId) || x.TenantId == request.TenantId)
                && (string.IsNullOrWhiteSpace(request.UserId) || x.UserId == request.UserId)
                && (string.IsNullOrWhiteSpace(request.UserEmail) || x.UserEmail == request.UserEmail)
                && (string.IsNullOrWhiteSpace(request.Action) || x.Action == request.Action)
                && (string.IsNullOrWhiteSpace(request.ResourceType) || x.ResourceType == request.ResourceType)
                && (string.IsNullOrWhiteSpace(request.ResourceId) || x.ResourceId == request.ResourceId)
                && (!request.FromDate.HasValue || x.CreatedAt >= request.FromDate.Value)
                && (!request.ToDate.HasValue || x.CreatedAt <= request.ToDate.Value),
            BuildSort(request.SortBy, request.NormalizedSortDirection == "desc"),
            request.Skip,
            request.NormalizedPageSize,
            cancellationToken);

        return PagedResponseDto<AuditLogResponseDto>.Create(
            result.Items.Select(Map).ToList(),
            request.NormalizedPageNumber,
            request.NormalizedPageSize,
            result.TotalCount);
    }

    public async Task<AuditLogResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var item = await auditLogRepository.GetByIdAsync(id, cancellationToken);
        return item is null ? null : Map(item);
    }

    private static SortDefinition<AuditLog> BuildSort(string? sortBy, bool descending)
    {
        var sort = Builders<AuditLog>.Sort;
        return (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "action" => descending ? sort.Descending(x => x.Action) : sort.Ascending(x => x.Action),
            "resourcetype" => descending ? sort.Descending(x => x.ResourceType) : sort.Ascending(x => x.ResourceType),
            _ => descending ? sort.Descending(x => x.CreatedAt) : sort.Ascending(x => x.CreatedAt),
        };
    }

    private static object? EnsureRole(object? metadata, string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return metadata;
        }

        var dictionary = metadata as IReadOnlyDictionary<string, object?>;
        var result = dictionary is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(dictionary, StringComparer.OrdinalIgnoreCase);

        result.TryAdd("role", role);
        return result;
    }

    private static AuditLogResponseDto Map(AuditLog log) => new()
    {
        Id = log.Id,
        TenantId = log.TenantId,
        UserId = log.UserId,
        UserEmail = log.UserEmail,
        Action = log.Action,
        ResourceType = log.ResourceType,
        ResourceId = log.ResourceId,
        Description = log.Description,
        Metadata = log.Metadata,
        IpAddress = log.IpAddress,
        UserAgent = log.UserAgent,
        CreatedAt = log.CreatedAt,
    };
}
