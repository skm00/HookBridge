using HookBridge.Application.DTOs.FailedEvents;
using HookBridge.Domain.Entities;
using MongoDB.Driver;

namespace HookBridge.Application.Interfaces.Persistence;

public interface IFailedEventRepository
{
    Task AddAsync(FailedEvent failedEvent, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<FailedEvent> Items, long TotalCount)> SearchAsync(
        FailedEventSearchRequestDto request,
        SortDefinition<FailedEvent> sort,
        int skip,
        int limit,
        CancellationToken cancellationToken = default);

    Task<FailedEvent?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task UpdateAsync(FailedEvent failedEvent, CancellationToken cancellationToken = default);

    Task<long> CountByStatusAsync(
        string tenantId,
        string status,
        CancellationToken cancellationToken = default);
}
