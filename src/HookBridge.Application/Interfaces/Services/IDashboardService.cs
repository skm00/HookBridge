using HookBridge.Application.DTOs.Dashboard;

namespace HookBridge.Application.Interfaces.Services;

public interface IDashboardService
{
    Task<DashboardOverviewResponseDto> GetOverviewAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}
