using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Approval;

public interface IHumanApprovalWorkflowService
{
    Task<HumanApprovalWorkflowResponseDto> CreateAsync(HumanApprovalWorkflowCreateRequestDto request, CancellationToken cancellationToken = default);
    Task<HumanApprovalWorkflowResponseDto?> GetByIdAsync(string approvalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HumanApprovalWorkflowResponseDto>> GetPendingAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HumanApprovalWorkflowResponseDto>> SearchPendingAsync(AiRecommendationApprovalSearchRequestDto request, CancellationToken cancellationToken = default);
    Task<HumanApprovalWorkflowResponseDto?> ReviewAsync(string approvalId, HumanApprovalWorkflowReviewRequestDto request, CancellationToken cancellationToken = default);
    Task<HumanApprovalWorkflowResponseDto?> ApplyAsync(string approvalId, HumanApprovalWorkflowApplyRequestDto request, CancellationToken cancellationToken = default);
    Task<HumanApprovalWorkflowResponseDto?> ExpireAsync(string approvalId, CancellationToken cancellationToken = default);
}
