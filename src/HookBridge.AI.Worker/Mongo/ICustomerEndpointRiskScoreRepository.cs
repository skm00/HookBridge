namespace HookBridge.AI.Worker.Mongo;

public interface ICustomerEndpointRiskScoreRepository
{
    Task InsertAsync(CustomerEndpointRiskScoreResult result, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetBySubscriptionIdAsync(string subscriptionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetByEndpointIdAsync(string endpointId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerEndpointRiskScoreResult>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
    Task<long> CountHighRiskEndpointsAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult(0L);
    Task<IReadOnlyDictionary<string, long>> CountByHealthStatusAsync(AiDashboardQueryFilter filter, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<string, long>>(new Dictionary<string, long>());
}
