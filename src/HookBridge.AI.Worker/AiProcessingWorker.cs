using System.Diagnostics;
using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.Kafka;
using HookBridge.AI.Worker.Logging;
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
    private readonly AiKafkaOptions _kafkaOptions;

    public AiProcessingWorker(
        ILogger<AiProcessingWorker> logger,
        IOptions<AiOptions> options,
        IKernelFactory kernelFactory,
        IAiAnalysisConsumer analysisConsumer,
        IAiAnalysisResultRepository analysisResultRepository,
        IAiRetryRecommendationService retryRecommendationService,
        IOptions<AiKafkaOptions>? kafkaOptions = null)
    {
        _logger = logger;
        _options = options;
        _kernelFactory = kernelFactory;
        _analysisConsumer = analysisConsumer;
        _analysisResultRepository = analysisResultRepository;
        _retryRecommendationService = retryRecommendationService;
        _kafkaOptions = kafkaOptions?.Value ?? new AiKafkaOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;
        _logger.LogInformation(
            AiWorkerLogMessages.WorkerStarting,
            options.Enabled,
            options.Provider,
            options.Model,
            _kafkaOptions.AiAnalysisTopic,
            _kafkaOptions.ConsumerGroupId);

        if (!options.Enabled)
        {
            _logger.LogInformation(
                AiWorkerLogMessages.WorkerAiDisabled,
                _kafkaOptions.AiAnalysisTopic,
                _kafkaOptions.ConsumerGroupId);
        }
        else
        {
            _logger.LogInformation(
                AiWorkerLogMessages.WorkerAiEnabled,
                options.Provider,
                options.Model,
                _kafkaOptions.AiAnalysisTopic,
                _kafkaOptions.ConsumerGroupId);
            _kernelFactory.CreateKernel();
            _logger.LogInformation("Semantic Kernel startup verification completed. Provider: {Provider}, Model: {Model}", options.Provider, options.Model);
        }

        try
        {
            await foreach (var analysisEvent in _analysisConsumer.ConsumeAsync(stoppingToken))
            {
                var processingStopwatch = Stopwatch.StartNew();
                using var scope = _logger.BeginScope(new Dictionary<string, object?>
                {
                    ["EventId"] = analysisEvent.EventId,
                    ["CorrelationId"] = analysisEvent.CorrelationId
                });

                try
                {
                    _logger.LogInformation(
                        AiWorkerLogMessages.ProcessingStarted,
                        analysisEvent.EventId,
                        analysisEvent.CorrelationId,
                        analysisEvent.EventType,
                        analysisEvent.Source,
                        options.Provider,
                        options.Model,
                        _kafkaOptions.AiAnalysisTopic,
                        _kafkaOptions.ConsumerGroupId);

                    var request = WebhookFailureAnalysisMapper.ToWebhookFailureAnalysisRequest(analysisEvent);
                    var recommendation = await _retryRecommendationService.AnalyzeAsync(request, stoppingToken);

                    var fallbackUsed = recommendation.Fallback?.UsedFallback ?? false;
                    var fallbackReason = recommendation.Fallback?.FallbackReason.ToString() ?? "None";
                    if (fallbackUsed)
                    {
                        _logger.LogWarning(
                            AiWorkerLogMessages.FallbackUsed,
                            recommendation.EventId,
                            recommendation.CorrelationId,
                            analysisEvent.EventType,
                            analysisEvent.Source,
                            recommendation.Provider,
                            recommendation.Model,
                            fallbackUsed,
                            fallbackReason,
                            recommendation.RiskLevel,
                            recommendation.SuggestedRetryAction);
                    }

                    var analysisResult = WebhookFailureAnalysisMapper.ToAiAnalysisResult(recommendation, request);
                    await InsertAnalysisResultAsync(analysisResult, stoppingToken);

                    processingStopwatch.Stop();
                    _logger.LogInformation(
                        AiWorkerLogMessages.ProcessingCompleted,
                        analysisResult.EventId,
                        analysisResult.CorrelationId,
                        analysisResult.EventType,
                        analysisResult.Source,
                        analysisResult.Provider,
                        analysisResult.Model,
                        _kafkaOptions.AiAnalysisTopic,
                        _kafkaOptions.ConsumerGroupId,
                        fallbackUsed,
                        fallbackReason,
                        analysisResult.RiskLevel,
                        analysisResult.SuggestedRetryAction,
                        processingStopwatch.ElapsedMilliseconds);

                    if (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation(AiWorkerLogMessages.CancellationRequested, "MessageProcessingLoop");
                        break;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation(AiWorkerLogMessages.CancellationRequested, "MessageProcessing");
                    throw;
                }
                catch (Exception exception)
                {
                    processingStopwatch.Stop();
                    _logger.LogError(
                        exception,
                        AiWorkerLogMessages.ProcessingFailed,
                        "MessageProcessing",
                        analysisEvent.EventId,
                        analysisEvent.CorrelationId,
                        analysisEvent.EventType,
                        analysisEvent.Source,
                        options.Provider,
                        options.Model,
                        _kafkaOptions.AiAnalysisTopic,
                        _kafkaOptions.ConsumerGroupId,
                        processingStopwatch.ElapsedMilliseconds);
                    throw;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(AiWorkerLogMessages.CancellationRequested, "WorkerExecution");
        }
        finally
        {
            _logger.LogInformation(AiWorkerLogMessages.WorkerStopping);
        }
    }

    private async Task InsertAnalysisResultAsync(AiAnalysisResult analysisResult, CancellationToken cancellationToken)
    {
        var persistenceStopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            AiWorkerLogMessages.MongoInsertStarted,
            analysisResult.EventId,
            analysisResult.CorrelationId,
            analysisResult.EventType,
            analysisResult.Source,
            analysisResult.Provider,
            analysisResult.Model);

        try
        {
            await _analysisResultRepository.InsertAsync(analysisResult, cancellationToken);
            persistenceStopwatch.Stop();
            _logger.LogInformation(
                AiWorkerLogMessages.MongoInsertCompleted,
                analysisResult.EventId,
                analysisResult.CorrelationId,
                analysisResult.EventType,
                analysisResult.Source,
                analysisResult.Provider,
                analysisResult.Model,
                persistenceStopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            persistenceStopwatch.Stop();
            _logger.LogError(
                exception,
                AiWorkerLogMessages.MongoInsertFailed,
                "MongoInsert",
                analysisResult.EventId,
                analysisResult.CorrelationId,
                analysisResult.EventType,
                analysisResult.Source,
                analysisResult.Provider,
                analysisResult.Model,
                persistenceStopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
