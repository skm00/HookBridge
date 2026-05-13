using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Mapping;
using HookBridge.AI.Worker.Mongo;
using HookBridge.AI.Worker.Services;
using HookBridge.AI.Worker.Services.RetryRecommendations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker;

public sealed class AiProcessingWorker : BackgroundService
{
    private readonly IAiAnalysisConsumer _analysisConsumer;
    private readonly IKernelFactory _kernelFactory;
    private readonly IAiAnalysisResultRepository _analysisResultRepository;
    private readonly IAiRetryRecommendationService _retryRecommendationService;
    private readonly ILogger<AiProcessingWorker> _logger;
    private readonly IOptions<AiOptions> _options;

    public AiProcessingWorker(
        ILogger<AiProcessingWorker> logger,
        IOptions<AiOptions> options,
        IKernelFactory kernelFactory,
        IAiAnalysisConsumer analysisConsumer,
        IAiAnalysisResultRepository analysisResultRepository,
        IAiRetryRecommendationService retryRecommendationService)
    {
        _logger = logger;
        _options = options;
        _kernelFactory = kernelFactory;
        _analysisConsumer = analysisConsumer;
        _analysisResultRepository = analysisResultRepository;
        _retryRecommendationService = retryRecommendationService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;
        _logger.LogInformation(
            "HookBridge AI Worker starting. Enabled: {Enabled}, Provider: {Provider}, Model: {Model}, Endpoint: {Endpoint}",
            options.Enabled,
            options.Provider,
            options.Model,
            options.Endpoint);

        if (!options.Enabled)
        {
            _logger.LogInformation("AI is disabled. Semantic Kernel initialization will be skipped.");
        }
        else
        {
            _kernelFactory.CreateKernel();
            _logger.LogInformation("Semantic Kernel startup verification completed.");
        }

        try
        {
            await foreach (var analysisEvent in _analysisConsumer.ConsumeAsync(stoppingToken))
            {
                _logger.LogInformation(
                    "AI analysis event ready for processing. EventId: {EventId}, CorrelationId: {CorrelationId}, Source: {Source}, EventType: {EventType}, CreatedAtUtc: {CreatedAtUtc}",
                    analysisEvent.EventId,
                    analysisEvent.CorrelationId,
                    analysisEvent.Source,
                    analysisEvent.EventType,
                    analysisEvent.CreatedAtUtc);

                var request = WebhookFailureAnalysisMapper.ToWebhookFailureAnalysisRequest(analysisEvent);
                var recommendation = await _retryRecommendationService.AnalyzeAsync(request, stoppingToken);
                var analysisResult = WebhookFailureAnalysisMapper.ToAiAnalysisResult(recommendation, request);
                await _analysisResultRepository.InsertAsync(analysisResult, stoppingToken);

                _logger.LogInformation(
                    "AI analysis result stored. EventId: {EventId}, CorrelationId: {CorrelationId}, ResultId: {ResultId}",
                    analysisResult.EventId,
                    analysisResult.CorrelationId,
                    analysisResult.Id);

                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during graceful shutdown.
        }
        finally
        {
            _logger.LogInformation("HookBridge AI Worker shutting down.");
        }
    }
}
