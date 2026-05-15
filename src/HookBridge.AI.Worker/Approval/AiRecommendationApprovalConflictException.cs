namespace HookBridge.AI.Worker.Approval;

public sealed class AiRecommendationApprovalConflictException : Exception
{
    public AiRecommendationApprovalConflictException(string message) : base(message)
    {
    }

    public AiRecommendationApprovalConflictException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
