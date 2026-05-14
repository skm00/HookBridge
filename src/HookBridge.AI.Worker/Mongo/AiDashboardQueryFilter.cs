namespace HookBridge.AI.Worker.Mongo;

public sealed class AiDashboardQueryFilter
{
    public string? Environment { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? EventType { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
}
