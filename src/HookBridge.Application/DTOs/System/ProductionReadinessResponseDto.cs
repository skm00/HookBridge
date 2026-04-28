namespace HookBridge.Application.DTOs.System;

public sealed class ProductionReadinessItemDto
{
    public string Name { get; set; } = string.Empty;

    public bool IsReady { get; set; }

    public string Message { get; set; } = string.Empty;
}

public sealed class ProductionReadinessResponseDto
{
    public bool IsReady { get; set; }

    public List<ProductionReadinessItemDto> Checks { get; set; } = [];
}
