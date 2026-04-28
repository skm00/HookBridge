using HookBridge.Application.DTOs.System;

namespace HookBridge.Application.Interfaces.Services;

public interface IProductionReadinessService
{
    Task<ProductionReadinessResponseDto> CheckAsync(CancellationToken cancellationToken = default);
}
