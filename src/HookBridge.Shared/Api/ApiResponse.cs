namespace HookBridge.Shared.Api;

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }

    public string? Message { get; init; }

    public T? Data { get; init; }

    public string? TraceId { get; init; }
}

public sealed class ApiErrorResponse
{
    public bool Success { get; init; } = false;

    public required string Message { get; init; }

    public int StatusCode { get; init; }

    public string? TraceId { get; init; }

    public Dictionary<string, string[]>? Errors { get; init; }
}

public static class ApiResponseFactory
{
    public static ApiResponse<T> Success<T>(T data, string? message = null, string? traceId = null)
        => new()
        {
            Success = true,
            Message = message,
            Data = data,
            TraceId = traceId,
        };

    public static ApiErrorResponse Error(string message, int statusCode, string? traceId = null)
        => new()
        {
            Message = message,
            StatusCode = statusCode,
            TraceId = traceId,
        };

    public static ApiErrorResponse ValidationError(Dictionary<string, string[]> errors, string? traceId = null)
        => new()
        {
            Message = "Validation failed.",
            StatusCode = 400,
            TraceId = traceId,
            Errors = errors,
        };
}
