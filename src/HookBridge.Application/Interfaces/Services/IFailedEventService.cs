using HookBridge.Application.DTOs.Common;
using HookBridge.Application.DTOs.FailedEvents;
using HookBridge.Domain.Entities;

namespace HookBridge.Application.Interfaces.Services;

public interface IFailedEventService
{
    Task CreateAsync(FailedEvent failedEvent, CancellationToken cancellationToken = default);

    Task<PagedResponseDto<FailedEventResponseDto>> SearchAsync(
        FailedEventSearchRequestDto request,
        CancellationToken cancellationToken = default);

    Task<FailedEventResponseDto?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<bool> RetryAsync(
        string failedEventId,
        CancellationToken cancellationToken = default);
}
