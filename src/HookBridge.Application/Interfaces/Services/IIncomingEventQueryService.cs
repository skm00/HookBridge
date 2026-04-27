using HookBridge.Application.DTOs.Events;

namespace HookBridge.Application.Interfaces.Services;

public interface IIncomingEventQueryService
{
    Task<IReadOnlyList<IncomingEventResponseDto>> SearchAsync(
        IncomingEventSearchRequestDto request,
        CancellationToken cancellationToken = default);

    Task<IncomingEventResponseDto?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);
}
