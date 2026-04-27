using HookBridge.Application.DTOs.Events;

namespace HookBridge.Application.Interfaces.Services;

public interface IEventIngestionService
{
    Task<EventIngestionResponseDto> IngestAsync(
        string tenantId,
        string apiKey,
        EventIngestionRequestDto request,
        string? correlationId,
        CancellationToken cancellationToken = default);
}
