namespace HookBridge.AI.Worker.Mongo;

public sealed class AiAnomalyRecordRepositoryResult
{
    public bool IsSuccess { get; init; }
    public bool IsDuplicate { get; init; }
    public string? Id { get; init; }
    public string? AnomalyId { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime StoredAtUtc { get; init; }

    public static AiAnomalyRecordRepositoryResult Success(AiAnomalyRecord record)
        => new()
        {
            IsSuccess = true,
            Id = record.Id,
            AnomalyId = record.AnomalyId,
            StoredAtUtc = record.StoredAtUtc
        };

    public static AiAnomalyRecordRepositoryResult Duplicate(string anomalyId, string? id = null)
        => new()
        {
            IsSuccess = false,
            IsDuplicate = true,
            Id = id,
            AnomalyId = anomalyId,
            ErrorMessage = "An anomaly record with the same AnomalyId already exists.",
            StoredAtUtc = DateTime.UtcNow
        };

    public static AiAnomalyRecordRepositoryResult Failure(string? anomalyId, string errorMessage)
        => new()
        {
            IsSuccess = false,
            AnomalyId = anomalyId,
            ErrorMessage = errorMessage,
            StoredAtUtc = DateTime.UtcNow
        };
}
