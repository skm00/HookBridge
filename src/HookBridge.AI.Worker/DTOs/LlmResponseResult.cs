namespace HookBridge.AI.Worker.DTOs;

/// <summary>
/// Safe result envelope for local LLM calls. Expected provider/model availability failures are represented here instead of thrown.
/// </summary>
public sealed class LlmResponseResult
{
    public bool IsSuccess { get; set; }

    public string ResponseText { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;

    public AiFallbackReason FallbackReason { get; set; } = AiFallbackReason.None;

    public int? StatusCode { get; set; }

    public long DurationMs { get; set; }

    public static LlmResponseResult Success(string responseText, long durationMs)
        => new()
        {
            IsSuccess = true,
            ResponseText = responseText,
            DurationMs = durationMs,
            FallbackReason = AiFallbackReason.None
        };

    public static LlmResponseResult Failure(AiFallbackReason reason, string errorMessage, long durationMs, int? statusCode = null)
        => new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            DurationMs = durationMs,
            StatusCode = statusCode,
            FallbackReason = reason
        };
}
