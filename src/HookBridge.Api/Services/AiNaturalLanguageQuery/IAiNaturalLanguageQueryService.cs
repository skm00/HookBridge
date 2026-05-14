using HookBridge.Application.DTOs.AiNaturalLanguageQuery;

namespace HookBridge.Api.Services.AiNaturalLanguageQuery;

public interface IAiNaturalLanguageQueryService
{
    Task<AiNaturalLanguageQueryResponseDto> QueryAsync(AiNaturalLanguageQueryRequestDto request, CancellationToken cancellationToken = default);
}
