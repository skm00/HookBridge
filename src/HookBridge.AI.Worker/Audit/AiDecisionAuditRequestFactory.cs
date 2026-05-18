using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Audit;

public static class AiDecisionAuditRequestFactory
{
    public static AiDecisionAuditCreateRequestDto FromRetry(RetryAgentResponseDto response, RetryAgentRequestDto request)
    {
        var audit = Base(response.EventId, response.CorrelationId, request.CustomerId, request.CustomerIdType, request.SubscriptionId, request.EndpointId, request.Environment, "RetryAgent", AiDecisionAuditType.RetryDecision);
        audit.Decision = response.RetryDecision.ToString(); audit.RiskLevel = response.RiskLevel; audit.ConfidenceScore = response.ConfidenceScore; audit.ConfidenceLevel = response.ConfidenceLevel.ToString(); audit.SuggestedAction = response.RetryDecision.ToString(); audit.RequiresApproval = response.RequiresApproval; audit.SafeModeDecision = response.SafeModeDecision.ToString(); audit.IsActionAllowed = response.IsActionAllowed; audit.UsedAi = !response.Fallback; audit.UsedFallback = response.Fallback; audit.FallbackReason = response.Fallback ? "AgentDisabledOrFallback" : null; audit.Summary = response.Summary; audit.Recommendation = response.Recommendation; audit.ReasonCodes = response.ReasonCodes.Select(code => code.ToString()).ToList(); audit.Metadata["maxAllowedRetries"] = response.MaxAllowedRetries.ToString(); audit.Metadata["retryDelaySeconds"] = response.RetryDelaySeconds.ToString();
        return audit;
    }

    public static AiDecisionAuditCreateRequestDto FromSecurity(SecurityAgentResponseDto response, SecurityAgentRequestDto request)
    {
        var audit = Base(response.EventId, response.CorrelationId, request.CustomerId, request.CustomerIdType, request.SubscriptionId, request.EndpointId, request.Environment, "SecurityAgent", AiDecisionAuditType.SecurityDecision);
        audit.Decision = response.SecurityDecision.ToString(); audit.RiskLevel = response.RiskLevel.ToString(); audit.ConfidenceScore = response.ConfidenceScore; audit.ConfidenceLevel = response.ConfidenceLevel.ToString(); audit.SuggestedAction = response.SecurityDecision.ToString(); audit.RequiresApproval = response.RequiresApproval; audit.SafeModeDecision = response.SafeModeDecision.ToString(); audit.IsActionAllowed = response.IsActionAllowed; audit.UsedAi = !response.Fallback; audit.UsedFallback = response.Fallback; audit.Summary = response.Summary; audit.Recommendation = response.Recommendation; audit.ReasonCodes = response.ReasonCodes.Select(code => code.ToString()).ToList(); audit.Metadata["securityRiskScore"] = response.SecurityRiskScore.ToString(); audit.Metadata["isSuspicious"] = response.IsSuspicious.ToString();
        return audit;
    }

    public static AiDecisionAuditCreateRequestDto FromTransformation(TransformationAgentResponseDto response, TransformationAgentRequestDto request)
    {
        var audit = Base(response.EventId, response.CorrelationId, request.CustomerId, request.CustomerIdType, request.SubscriptionId, request.EndpointId, request.Environment, "TransformationAgent", AiDecisionAuditType.TransformationDecision);
        audit.Decision = response.TransformationDecision.ToString(); audit.RiskLevel = response.RiskLevel; audit.ConfidenceScore = response.ConfidenceScore; audit.ConfidenceLevel = response.ConfidenceLevel.ToString(); audit.SuggestedAction = response.TransformationDecision.ToString(); audit.RequiresApproval = response.RequiresApproval; audit.SafeModeDecision = response.SafeModeDecision.ToString(); audit.IsActionAllowed = response.IsActionAllowed; audit.UsedAi = !response.Fallback; audit.UsedFallback = response.Fallback; audit.PromptName = response.PromptName; audit.PromptVersion = response.PromptVersion; audit.PromptHash = response.PromptHash; audit.Summary = response.Summary; audit.Recommendation = response.Recommendation; audit.ReasonCodes = response.ReasonCodes.Select(code => code.ToString()).ToList();
        return audit;
    }

    public static AiDecisionAuditCreateRequestDto FromObservability(ObservabilityAgentResponseDto response, ObservabilityAgentRequestDto request)
    {
        var audit = Base(response.EventId, response.CorrelationId, request.CustomerId, null, request.SubscriptionId, request.EndpointId, request.Environment, "ObservabilityAgent", AiDecisionAuditType.ObservabilityDecision);
        audit.Decision = response.ObservabilityStatus.ToString(); audit.RiskLevel = response.RiskLevel.ToString(); audit.ConfidenceScore = response.ConfidenceScore; audit.ConfidenceLevel = response.ConfidenceLevel.ToString(); audit.SuggestedAction = string.Join(",", response.SuggestedActions); audit.RequiresApproval = response.RequiresApproval; audit.SafeModeDecision = response.SafeModeDecision.ToString(); audit.IsActionAllowed = response.IsActionAllowed; audit.UsedAi = !response.Fallback; audit.UsedFallback = response.Fallback; audit.PromptName = response.PromptName; audit.PromptVersion = response.PromptVersion; audit.PromptHash = response.PromptHash; audit.Summary = response.Summary; audit.Recommendation = response.Recommendation;
        return audit;
    }

    public static AiDecisionAuditCreateRequestDto FromOrchestration(AiAgentOrchestrationResponseDto response, AiAgentOrchestrationRequestDto request)
    {
        var audit = Base(response.EventId, response.CorrelationId, request.CustomerId, request.CustomerIdType, request.SubscriptionId, request.EndpointId, request.Environment, "MultiAgentOrchestrator", AiDecisionAuditType.OrchestrationDecision);
        audit.Decision = response.RecommendedAction.ToString(); audit.RiskLevel = response.OverallRiskLevel.ToString(); audit.ConfidenceScore = response.ConfidenceScore; audit.ConfidenceLevel = response.ConfidenceLevel.ToString(); audit.SuggestedAction = response.RecommendedAction.ToString(); audit.RequiresApproval = response.RequiresApproval; audit.ApprovalId = response.ApprovalId; audit.SafeModeDecision = response.SafeModeDecision.ToString(); audit.IsActionAllowed = response.IsActionAllowed; audit.UsedAi = true; audit.UsedFallback = response.AgentResults.Any(agent => agent.UsedFallback); audit.Summary = response.OverallSummary; audit.Recommendation = response.RecommendedAction.ToString(); audit.Metadata["agentCount"] = response.AgentResults.Count.ToString();
        return audit;
    }

    public static AiDecisionAuditCreateRequestDto FromAutoRemediation(AutoRemediationRecommendationResponseDto response, AutoRemediationRecommendationRequestDto request)
    {
        var audit = Base(response.EventId, response.CorrelationId, request.CustomerId, request.CustomerIdType, request.SubscriptionId, request.EndpointId, request.Environment, "AutoRemediationRecommendationService", AiDecisionAuditType.AutoRemediationRecommendation);
        audit.Decision = response.RecommendedAction.ToString(); audit.RiskLevel = response.RiskLevel; audit.ConfidenceScore = response.ConfidenceScore; audit.SuggestedAction = response.RecommendedAction.ToString(); audit.RequiresApproval = response.RequiresApproval; audit.SafeModeDecision = response.SafeModeDecision.ToString(); audit.IsActionAllowed = response.IsActionAllowed; audit.UsedAi = true; audit.UsedFallback = false; audit.Summary = response.Summary; audit.Recommendation = response.Recommendation; audit.ReasonCodes = response.ReasonCodes.Select(code => code.ToString()).ToList(); audit.Metadata["remediationType"] = response.RemediationType.ToString(); audit.Metadata["canAutoApply"] = response.CanAutoApply.ToString();
        return audit;
    }

    private static AiDecisionAuditCreateRequestDto Base(string eventId, string? correlationId, string? customerId, string? customerIdType, string? subscriptionId, string? endpointId, string? environment, string agentName, AiDecisionAuditType type) => new()
    {
        EventId = eventId,
        CorrelationId = correlationId,
        CustomerId = customerId,
        CustomerIdType = customerIdType,
        SubscriptionId = subscriptionId,
        EndpointId = endpointId,
        Environment = environment,
        AgentName = agentName,
        DecisionType = type,
        CreatedBy = "HookBridge.AI.Worker"
    };
}
