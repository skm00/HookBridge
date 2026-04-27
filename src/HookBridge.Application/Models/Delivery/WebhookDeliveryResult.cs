namespace HookBridge.Application.Models.Delivery;

public sealed class WebhookDeliveryResult
{
    public bool IsSuccess { get; set; }

    public int? HttpStatusCode { get; set; }

    public string? ResponseBody { get; set; }

    public string? ErrorMessage { get; set; }

    public long DurationMs { get; set; }
}
