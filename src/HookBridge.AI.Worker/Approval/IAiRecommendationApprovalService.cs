using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Approval;

public interface IAiRecommendationApprovalService
{
    Task<AiRecommendationApprovalResponseDto> CreateAsync(AiRecommendationApprovalCreateRequestDto request, CancellationToken cancellationToken = default);
    Task<AiRecommendationApprovalResponseDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<AiRecommendationApprovalResponseDto?> GetByRecommendationIdAsync(string recommendationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiRecommendationApprovalResponseDto>> GetPendingAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiRecommendationApprovalResponseDto>> SearchAsync(AiRecommendationApprovalSearchRequestDto request, CancellationToken cancellationToken = default);
    Task<AiRecommendationApprovalResponseDto?> UpdateStatusAsync(string id, AiRecommendationApprovalUpdateRequestDto request, CancellationToken cancellationToken = default);
}
