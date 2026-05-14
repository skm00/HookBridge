using System.ComponentModel.DataAnnotations;

namespace HookBridge.Application.DTOs.AiNaturalLanguageQuery;

/// <summary>
/// Request body for asking a safe natural language question about webhook delivery data.
/// </summary>
public sealed class AiNaturalLanguageQueryRequestDto
{
    [Required]
    [MaxLength(1000)]
    public string Query { get; set; } = string.Empty;

    public string? Environment { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerIdType { get; set; }
    public string? SubscriptionId { get; set; }
    public string? EndpointId { get; set; }
    public string? EventId { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int? MaxResults { get; set; }
}
