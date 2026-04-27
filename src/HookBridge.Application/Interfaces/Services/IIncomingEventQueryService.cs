using HookBridge.Application.DTOs.Common;
using HookBridge.Application.DTOs.Events;

namespace HookBridge.Application.Interfaces.Services;

public interface IIncomingEventQueryService
{
    Task<PagedResponseDto<IncomingEventResponseDto>> SearchAsync(
        IncomingEventSearchRequestDto request,
        CancellationToken cancellationToken = default);

    Task<IncomingEventResponseDto?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);
}
