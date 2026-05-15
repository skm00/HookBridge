namespace HookBridge.AI.Worker.Approval;

public sealed class AiRecommendationApprovalConflictException : Exception
{
    public AiRecommendationApprovalConflictException(string message) : base(message)
    {
    }
}
