using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;

namespace HookBridge.AI.Worker.Audit;

public interface IAiDecisionAuditService
{
    Task<AiDecisionAuditRecord?> AuditRetryDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default);
    Task<AiDecisionAuditRecord?> AuditSecurityDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default);
    Task<AiDecisionAuditRecord?> AuditTransformationDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default);
    Task<AiDecisionAuditRecord?> AuditObservabilityDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default);
    Task<AiDecisionAuditRecord?> AuditOrchestrationDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default);
    Task<AiDecisionAuditRecord?> AuditAutoRemediationRecommendationAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default);
    Task<AiDecisionAuditRecord?> AuditHumanApprovalAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default);
    Task<AiDecisionAuditRecord?> AuditSafeModeEvaluationAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default);
    Task<AiDecisionAuditRecord?> AuditFallbackDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default);
    Task<AiDecisionAuditRecord?> AuditGenericDecisionAsync(AiDecisionAuditCreateRequestDto request, CancellationToken cancellationToken = default);
}
