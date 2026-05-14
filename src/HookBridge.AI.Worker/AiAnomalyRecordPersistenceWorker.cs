using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mappers;
using HookBridge.AI.Worker.Mongo;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HookBridge.AI.Worker;

public sealed class AiAnomalyRecordPersistenceWorker : BackgroundService
{
    private readonly IAiAnomalyConsumer _consumer;
    private readonly IAiAnomalyRecordRepository _repository;
    private readonly ILogger<AiAnomalyRecordPersistenceWorker> _logger;

    public AiAnomalyRecordPersistenceWorker(
        IAiAnomalyConsumer consumer,
        IAiAnomalyRecordRepository repository,
        ILogger<AiAnomalyRecordPersistenceWorker> logger)
    {
        _consumer = consumer;
        _repository = repository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var anomalyEvent in _consumer.ConsumeAsync(stoppingToken))
        {
            var record = AiAnomalyRecordMapper.FromAnomalyEvent(anomalyEvent);
            var result = await _repository.InsertAsync(record, stoppingToken);

            if (result.IsDuplicate)
            {
                _logger.LogWarning(
                    "Duplicate AI anomaly record skipped. AnomalyId: {AnomalyId}, EventId: {EventId}, CorrelationId: {CorrelationId}, CustomerId: {CustomerId}, EndpointId: {EndpointId}, AnomalyType: {AnomalyType}, RiskLevel: {RiskLevel}",
                    record.AnomalyId,
                    record.EventId,
                    record.CorrelationId,
                    record.CustomerId,
                    record.EndpointId,
                    record.AnomalyType,
                    record.RiskLevel);
                continue;
            }

            if (!result.IsSuccess)
            {
                _logger.LogError(
                    "AI anomaly record persistence failed. AnomalyId: {AnomalyId}, EventId: {EventId}, CorrelationId: {CorrelationId}, CustomerId: {CustomerId}, EndpointId: {EndpointId}, AnomalyType: {AnomalyType}, RiskLevel: {RiskLevel}, ErrorMessage: {ErrorMessage}",
                    record.AnomalyId,
                    record.EventId,
                    record.CorrelationId,
                    record.CustomerId,
                    record.EndpointId,
                    record.AnomalyType,
                    record.RiskLevel,
                    result.ErrorMessage);
                continue;
            }

            _logger.LogInformation(
                "AI anomaly record persisted. AnomalyId: {AnomalyId}, EventId: {EventId}, CorrelationId: {CorrelationId}, CustomerId: {CustomerId}, EndpointId: {EndpointId}, AnomalyType: {AnomalyType}, RiskLevel: {RiskLevel}, StoredAtUtc: {StoredAtUtc}",
                result.AnomalyId,
                record.EventId,
                record.CorrelationId,
                record.CustomerId,
                record.EndpointId,
                record.AnomalyType,
                record.RiskLevel,
                result.StoredAtUtc);
        }
    }
}
