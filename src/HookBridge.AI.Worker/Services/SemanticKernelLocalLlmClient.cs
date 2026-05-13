using HookBridge.AI.Worker.Configuration;
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

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var attempts = Math.Max(1, _options.MaxRetries + 1);
        Exception? lastException = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));

            try
            {
                var kernel = _kernel ??= _kernelFactory.CreateKernel();
                var result = await kernel.InvokePromptAsync(prompt, cancellationToken: timeout.Token);
                return result.ToString();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (attempt < attempts)
            {
                lastException = exception;
                _logger.LogWarning(
                    exception,
                    "Local LLM request attempt {Attempt} of {Attempts} failed for provider {Provider}, model {Model}.",
                    attempt,
                    attempts,
                    _options.Provider,
                    _options.Model);
            }
            catch (Exception exception)
            {
                lastException = exception;
            }
        }

        throw new InvalidOperationException("Local LLM request failed after all retry attempts.", lastException);
    }
}
