using HookBridge.AI.Worker.Approval;
using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Mongo;

public interface IAiRecommendationApprovalRepository
{
    Task InsertAsync(AiRecommendationApproval approval, CancellationToken cancellationToken = default);
    Task<AiRecommendationApproval?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<AiRecommendationApproval?> GetByRecommendationIdAsync(string recommendationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiRecommendationApproval>> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiRecommendationApproval>> GetPendingAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiRecommendationApproval>> SearchAsync(AiRecommendationApprovalSearchRequestDto request, CancellationToken cancellationToken = default);
    Task<AiRecommendationApproval?> UpdateStatusAsync(string id, AiRecommendationApprovalStatusUpdate update, CancellationToken cancellationToken = default);
}
