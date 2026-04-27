using HookBridge.Application.DTOs.FailedEvents;
using HookBridge.Domain.Entities;

namespace HookBridge.Application.Interfaces.Persistence;

public interface IFailedEventRepository
{
    Task AddAsync(FailedEvent failedEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FailedEvent>> SearchAsync(
        FailedEventSearchRequestDto request,
        CancellationToken cancellationToken = default);

    Task<FailedEvent?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task UpdateAsync(FailedEvent failedEvent, CancellationToken cancellationToken = default);
}
