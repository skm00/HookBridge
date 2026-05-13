using System.Diagnostics;
using System.Text.Json;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace HookBridge.AI.Worker.Services;

public sealed class SemanticKernelLocalLlmClient : ILocalLlmClient
{
    private readonly IKernelFactory _kernelFactory;
    private readonly ILogger<SemanticKernelLocalLlmClient> _logger;
    private readonly AiOptions _options;
    private Kernel? _kernel;

    public SemanticKernelLocalLlmClient(
        IKernelFactory kernelFactory,
        IOptions<AiOptions> options,
        ILogger<SemanticKernelLocalLlmClient> logger)
    {
        _kernelFactory = kernelFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LlmResponseResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        if (string.IsNullOrWhiteSpace(_options.Provider) || string.IsNullOrWhiteSpace(_options.Model) || string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            return LlmResponseResult.Failure(
                AiFallbackReason.ConfigurationError,
                "AI provider, model, or endpoint is not configured.",
                durationMs: 0);
        }

        var attempts = Math.Max(1, _options.MaxRetries + 1);
        LlmResponseResult? lastFailure = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var stopwatch = Stopwatch.StartNew();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.LlmRequestTimeoutSeconds)));

            try
            {
                var kernel = _kernel ??= _kernelFactory.CreateKernel();
                var result = await kernel.InvokePromptAsync(prompt, cancellationToken: timeout.Token);
                stopwatch.Stop();

                var responseText = result.ToString();
                if (string.IsNullOrWhiteSpace(responseText))
                {
                    lastFailure = LlmResponseResult.Failure(
                        AiFallbackReason.InvalidResponse,
                        "LLM provider returned an empty response.",
                        stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    return LlmResponseResult.Success(responseText, stopwatch.ElapsedMilliseconds);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException exception)
            {
                stopwatch.Stop();
                lastFailure = LlmResponseResult.Failure(
                    AiFallbackReason.Timeout,
                    "LLM provider request timed out.",
                    stopwatch.ElapsedMilliseconds);
                LogExpectedFailure(exception, attempt, attempts, lastFailure);
            }
            catch (HttpRequestException exception)
            {
                stopwatch.Stop();
                lastFailure = LlmResponseResult.Failure(
                    MapHttpFailureReason(exception),
                    BuildSafeHttpErrorMessage(exception),
                    stopwatch.ElapsedMilliseconds,
                    exception.StatusCode.HasValue ? (int)exception.StatusCode.Value : null);
                LogExpectedFailure(exception, attempt, attempts, lastFailure);
            }
            catch (JsonException exception)
            {
                stopwatch.Stop();
                lastFailure = LlmResponseResult.Failure(
                    AiFallbackReason.InvalidJson,
                    "LLM provider returned invalid JSON.",
                    stopwatch.ElapsedMilliseconds);
                LogExpectedFailure(exception, attempt, attempts, lastFailure);
            }
            catch (InvalidOperationException exception) when (IsExpectedProviderFailure(exception))
            {
                stopwatch.Stop();
                lastFailure = LlmResponseResult.Failure(
                    MapExceptionFailureReason(exception),
                    BuildSafeProviderErrorMessage(exception),
                    stopwatch.ElapsedMilliseconds);
                LogExpectedFailure(exception, attempt, attempts, lastFailure);
            }
            catch (Exception exception) when (IsExpectedProviderFailure(exception))
            {
                stopwatch.Stop();
                lastFailure = LlmResponseResult.Failure(
                    MapExceptionFailureReason(exception),
                    BuildSafeProviderErrorMessage(exception),
                    stopwatch.ElapsedMilliseconds);
                LogExpectedFailure(exception, attempt, attempts, lastFailure);
            }

            if (attempt < attempts && lastFailure is not null)
            {
                continue;
            }
        }

        return lastFailure ?? LlmResponseResult.Failure(AiFallbackReason.UnknownError, "LLM provider failed for an unknown reason.", 0);
    }

    private void LogExpectedFailure(Exception exception, int attempt, int attempts, LlmResponseResult failure)
    {
        _logger.LogWarning(
            exception,
            "Local LLM request attempt {Attempt} of {Attempts} failed. FallbackReason: {FallbackReason}, Provider: {Provider}, Model: {Model}, StatusCode: {StatusCode}, DurationMs: {DurationMs}",
            attempt,
            attempts,
            failure.FallbackReason,
            _options.Provider,
            _options.Model,
            failure.StatusCode,
            failure.DurationMs);
    }

    private static AiFallbackReason MapHttpFailureReason(HttpRequestException exception)
    {
        if (IsModelMissingMessage(exception.Message))
        {
            return AiFallbackReason.ModelUnavailable;
        }

        return exception.StatusCode is null
            ? AiFallbackReason.ProviderUnavailable
            : AiFallbackReason.InvalidResponse;
    }

    private static AiFallbackReason MapExceptionFailureReason(Exception exception)
    {
        if (IsModelMissingMessage(exception.Message))
        {
            return AiFallbackReason.ModelUnavailable;
        }

        if (exception is TimeoutException)
        {
            return AiFallbackReason.Timeout;
        }

        if (exception is JsonException)
        {
            return AiFallbackReason.InvalidJson;
        }

        return IsProviderUnavailableMessage(exception.Message)
            ? AiFallbackReason.ProviderUnavailable
            : AiFallbackReason.UnknownError;
    }

    private static bool IsExpectedProviderFailure(Exception exception)
        => exception is TimeoutException or TaskCanceledException ||
           IsModelMissingMessage(exception.Message) ||
           IsProviderUnavailableMessage(exception.Message) ||
           exception.InnerException is not null && IsExpectedProviderFailure(exception.InnerException);

    private static bool IsModelMissingMessage(string? message)
        => !string.IsNullOrWhiteSpace(message) &&
           (message.Contains("model", StringComparison.OrdinalIgnoreCase) &&
            (message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("pull", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)));

    private static bool IsProviderUnavailableMessage(string? message)
        => !string.IsNullOrWhiteSpace(message) &&
           (message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("refused", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("unavailable", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("no such host", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("network", StringComparison.OrdinalIgnoreCase));

    private static string BuildSafeHttpErrorMessage(HttpRequestException exception)
    {
        if (IsModelMissingMessage(exception.Message))
        {
            return "Configured LLM model is unavailable.";
        }

        return exception.StatusCode is null
            ? "LLM provider connection failed."
            : $"LLM provider returned non-success status code {(int)exception.StatusCode.Value}.";
    }

    private static string BuildSafeProviderErrorMessage(Exception exception)
    {
        if (IsModelMissingMessage(exception.Message))
        {
            return "Configured LLM model is unavailable.";
        }

        return MapExceptionFailureReason(exception) switch
        {
            AiFallbackReason.ProviderUnavailable => "LLM provider was unavailable.",
            AiFallbackReason.Timeout => "LLM provider request timed out.",
            AiFallbackReason.InvalidJson => "LLM provider returned invalid JSON.",
            _ => "LLM provider failed."
        };
    }
}
