using HookBridge.AI.Worker.DTOs;

namespace HookBridge.AI.Worker.Mongo;

public interface IObservabilityAgentResultRepository
{
    Task InsertAsync(ObservabilityAgentResult result, CancellationToken cancellationToken = default);
    Task<ObservabilityAgentResult?> GetByEventIdAsync(string eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ObservabilityAgentResult>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ObservabilityAgentResult>> GetByEnvironmentAsync(string environment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ObservabilityAgentResult>> GetByServiceNameAsync(string serviceName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ObservabilityAgentResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ObservabilityAgentResult>> SearchAsync(ObservabilityAgentSearchRequestDto request, CancellationToken cancellationToken = default);
}
