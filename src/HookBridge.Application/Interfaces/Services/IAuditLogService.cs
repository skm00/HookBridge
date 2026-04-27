using HookBridge.Application.DTOs.AuditLogs;
using HookBridge.Application.DTOs.Common;
using HookBridge.Domain.Entities;

namespace HookBridge.Application.Interfaces.Services;

public interface IAuditLogService
{
    Task LogAsync(AuditLog auditLog, CancellationToken cancellationToken = default);

    Task<PagedResponseDto<AuditLogResponseDto>> SearchAsync(AuditLogSearchRequestDto request, CancellationToken cancellationToken = default);

    Task<AuditLogResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
}
