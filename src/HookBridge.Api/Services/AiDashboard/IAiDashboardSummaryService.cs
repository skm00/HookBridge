using HookBridge.Application.DTOs.AiDashboard;

namespace HookBridge.Api.Services.AiDashboard;

public interface IAiDashboardSummaryService
{
    Task<AiDashboardSummaryResponseDto> GetSummaryAsync(AiDashboardSummaryRequestDto request, CancellationToken cancellationToken = default);
}
