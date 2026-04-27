using HookBridge.Application.DTOs.DeliveryAttempts;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using MongoDB.Driver;

namespace HookBridge.Application.Interfaces.Persistence;

public interface IDeliveryAttemptRepository
{
    Task<(IReadOnlyList<DeliveryAttempt> Items, long TotalCount)> SearchAsync(
        DeliveryAttemptSearchRequestDto request,
        SortDefinition<DeliveryAttempt> sort,
        int skip,
        int limit,
        CancellationToken cancellationToken = default);

    Task<DeliveryAttempt?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<long> CountAsync(
        string tenantId,
        DateTime fromDateInclusive,
        DateTime toDateExclusive,
        DeliveryStatus? status,
        CancellationToken cancellationToken = default);
}
