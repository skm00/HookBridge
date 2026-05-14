using HookBridge.AI.Worker.Configuration;
using HookBridge.AI.Worker.DTOs;
using HookBridge.AI.Worker.Mongo;
using Microsoft.Extensions.Options;

namespace HookBridge.AI.Worker.Services.DuplicateReplayDetection;

public sealed class WebhookDuplicateReplayDetectionService : IWebhookDuplicateReplayDetectionService
{
    private readonly IWebhookEventFingerprintRepository _repository;
    private readonly IWebhookFingerprintHashService _hashService;
    private readonly DuplicateReplayDetectionOptions _options;

    public WebhookDuplicateReplayDetectionService(IWebhookEventFingerprintRepository repository, IWebhookFingerprintHashService hashService, IOptions<DuplicateReplayDetectionOptions> options)
    {
        _repository = repository;
        _hashService = hashService;
        _options = options.Value;
    }

    public async Task<WebhookDuplicateReplayDetectionResponseDto> DetectAsync(WebhookDuplicateReplayDetectionRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var now = DateTime.UtcNow;
        var payloadHash = _hashService.GeneratePayloadHash(request.Payload);
        var signatureHash = _hashService.GenerateSignatureHash(request.Signature);
        if (string.IsNullOrWhiteSpace(request.EventId) && string.IsNullOrWhiteSpace(payloadHash)) throw new ArgumentException("EventId or Payload must be provided.", nameof(request));

        var score = 0;
        var duplicateReason = WebhookDuplicateReplayReason.None;
        var replayReason = WebhookDuplicateReplayReason.None;
        var isDuplicate = false;
        var isReplay = false;

        if (!_options.Enabled)
        {
            return BuildResponse(request, payloadHash, signatureHash, false, false, duplicateReason, replayReason, 0, AiRiskLevel.Low, WebhookDuplicateReplaySuggestedAction.Allow, now);
        }

        if (!string.IsNullOrWhiteSpace(request.EventId) && await _repository.ExistsByEventIdAsync(request.EventId, cancellationToken))
        {
            score += 50; isDuplicate = true; duplicateReason = WebhookDuplicateReplayReason.SameEventId;
        }

        if (!string.IsNullOrWhiteSpace(request.CorrelationId) && await _repository.ExistsByCorrelationIdAsync(request.CorrelationId, cancellationToken))
        {
            score += 25; isDuplicate = true; if (duplicateReason == WebhookDuplicateReplayReason.None) duplicateReason = WebhookDuplicateReplayReason.SameCorrelationId;
        }

        if (!string.IsNullOrWhiteSpace(payloadHash) && await _repository.ExistsByPayloadHashAsync(payloadHash, request.CustomerId, request.SubscriptionId, cancellationToken))
        {
            score += 30; isDuplicate = true; if (duplicateReason == WebhookDuplicateReplayReason.None) duplicateReason = WebhookDuplicateReplayReason.SamePayloadHash;
        }

        if (!string.IsNullOrWhiteSpace(signatureHash) && await _repository.ExistsBySignatureHashAsync(signatureHash, request.ReceivedAtUtc.AddMinutes(-_options.ReplayWindowMinutes), cancellationToken))
        {
            score += 40; isReplay = true; replayReason = WebhookDuplicateReplayReason.SameSignatureHash;
        }

        if (request.EventTimestampUtc is not null)
        {
            if (request.EventTimestampUtc.Value < request.ReceivedAtUtc.AddMinutes(-_options.ReplayWindowMinutes))
            {
                score += 35; isReplay = true; if (replayReason == WebhookDuplicateReplayReason.None) replayReason = WebhookDuplicateReplayReason.EventTimestampTooOld;
            }
            else if (request.EventTimestampUtc.Value > request.ReceivedAtUtc.AddMinutes(_options.FutureTimestampToleranceMinutes))
            {
                score += 25; isReplay = true; if (replayReason == WebhookDuplicateReplayReason.None) replayReason = WebhookDuplicateReplayReason.EventTimestampInFuture;
            }
        }

        if (!string.IsNullOrWhiteSpace(payloadHash))
        {
            var recent = await _repository.SearchSimilarAsync(request.CustomerId, request.SubscriptionId, request.EndpointId, payloadHash, null, request.ReceivedAtUtc.AddSeconds(-_options.HighFrequencyWindowSeconds), _options.HighFrequencyThreshold, cancellationToken);
            if (recent.Count >= _options.HighFrequencyThreshold)
            {
                score += 30; isDuplicate = true; isReplay = true;
                if (duplicateReason == WebhookDuplicateReplayReason.None) duplicateReason = WebhookDuplicateReplayReason.HighFrequencyRepeat;
                if (replayReason == WebhookDuplicateReplayReason.None) replayReason = WebhookDuplicateReplayReason.HighFrequencyRepeat;
            }
        }

        score = Math.Clamp(score, 0, 100);
        var riskLevel = MapRiskLevel(score, payloadHash, request.EventId);
        var action = MapSuggestedAction(riskLevel, duplicateReason, replayReason);
        return BuildResponse(request, payloadHash, signatureHash, isDuplicate, isReplay, duplicateReason, replayReason, score, riskLevel, action, now);
    }

    public static AiRiskLevel MapRiskLevel(int score, string? payloadHash = "data", string? eventId = null)
    {
        if (string.IsNullOrWhiteSpace(payloadHash) && string.IsNullOrWhiteSpace(eventId)) return AiRiskLevel.Unknown;
        return score switch { <= 20 => AiRiskLevel.Low, <= 50 => AiRiskLevel.Medium, <= 80 => AiRiskLevel.High, _ => AiRiskLevel.Critical };
    }

    public static WebhookDuplicateReplaySuggestedAction MapSuggestedAction(AiRiskLevel riskLevel, WebhookDuplicateReplayReason duplicateReason = WebhookDuplicateReplayReason.None, WebhookDuplicateReplayReason replayReason = WebhookDuplicateReplayReason.None)
    {
        if (duplicateReason == WebhookDuplicateReplayReason.SameEventId) return WebhookDuplicateReplaySuggestedAction.IgnoreDuplicate;
        if (replayReason is WebhookDuplicateReplayReason.EventTimestampTooOld or WebhookDuplicateReplayReason.SignatureTimestampExpired or WebhookDuplicateReplayReason.SameSignatureHash) return WebhookDuplicateReplaySuggestedAction.Reject;
        return riskLevel switch
        {
            AiRiskLevel.Low => WebhookDuplicateReplaySuggestedAction.Allow,
            AiRiskLevel.Medium => WebhookDuplicateReplaySuggestedAction.Monitor,
            AiRiskLevel.High => WebhookDuplicateReplaySuggestedAction.RequireManualReview,
            AiRiskLevel.Critical => WebhookDuplicateReplaySuggestedAction.Quarantine,
            _ => WebhookDuplicateReplaySuggestedAction.Monitor
        };
    }

    public static WebhookEventFingerprint CreateFingerprint(WebhookDuplicateReplayDetectionRequestDto request, WebhookDuplicateReplayDetectionResponseDto response, DuplicateReplayDetectionOptions options, DateTime? createdAtUtc = null)
    {
        var created = createdAtUtc ?? DateTime.UtcNow;
        return new WebhookEventFingerprint
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            CustomerId = request.CustomerId,
            CustomerIdType = request.CustomerIdType,
            SubscriptionId = request.SubscriptionId,
            EndpointId = request.EndpointId,
            Environment = request.Environment,
            EventType = request.EventType,
            Source = request.Source,
            TargetUrl = request.TargetUrl,
            PayloadHash = response.PayloadHash,
            SignatureHash = response.SignatureHash,
            EventTimestampUtc = request.EventTimestampUtc,
            ReceivedAtUtc = request.ReceivedAtUtc,
            CreatedAtUtc = created,
            ExpiresAtUtc = created.AddHours(options.FingerprintTtlHours)
        };
    }

    private static WebhookDuplicateReplayDetectionResponseDto BuildResponse(WebhookDuplicateReplayDetectionRequestDto request, string? payloadHash, string? signatureHash, bool isDuplicate, bool isReplay, WebhookDuplicateReplayReason duplicateReason, WebhookDuplicateReplayReason replayReason, int score, AiRiskLevel riskLevel, WebhookDuplicateReplaySuggestedAction action, DateTime detectedAtUtc)
    {
        if (score is < 0 or > 100) throw new InvalidOperationException("DetectionScore must be between 0 and 100.");
        return new WebhookDuplicateReplayDetectionResponseDto
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            CustomerId = request.CustomerId,
            SubscriptionId = request.SubscriptionId,
            EndpointId = request.EndpointId,
            IsDuplicate = isDuplicate,
            IsReplay = isReplay,
            DuplicateReason = duplicateReason,
            ReplayReason = replayReason,
            PayloadHash = payloadHash,
            SignatureHash = signatureHash,
            DetectionScore = score,
            RiskLevel = riskLevel,
            SuggestedAction = action,
            Summary = CreateSummary(isDuplicate, isReplay, duplicateReason, replayReason),
            Recommendation = CreateRecommendation(action),
            DetectedAtUtc = detectedAtUtc
        };
    }

    private static string CreateSummary(bool isDuplicate, bool isReplay, WebhookDuplicateReplayReason duplicateReason, WebhookDuplicateReplayReason replayReason)
        => isDuplicate || isReplay ? $"Deterministic duplicate/replay checks matched duplicate reason {duplicateReason} and replay reason {replayReason}." : "No duplicate or replay indicators were detected.";

    private static string CreateRecommendation(WebhookDuplicateReplaySuggestedAction action) => action switch
    {
        WebhookDuplicateReplaySuggestedAction.IgnoreDuplicate => "Ignore this duplicate event and do not forward it again.",
        WebhookDuplicateReplaySuggestedAction.Reject => "Reject this event because deterministic replay indicators were detected.",
        WebhookDuplicateReplaySuggestedAction.Quarantine => "Quarantine this event for investigation before forwarding.",
        WebhookDuplicateReplaySuggestedAction.RequireManualReview => "Route this event to manual review before forwarding.",
        WebhookDuplicateReplaySuggestedAction.Monitor => "Process with monitoring and retain the fingerprint.",
        _ => "Allow the event and retain the fingerprint."
    };

    private static void ValidateRequest(WebhookDuplicateReplayDetectionRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.EventId) && request.Payload is null) throw new ArgumentException("EventId or Payload must be provided.", nameof(request));
        if (request.ReceivedAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("ReceivedAtUtc must be UTC.", nameof(request));
        if (request.EventTimestampUtc is not null && request.EventTimestampUtc.Value.Kind != DateTimeKind.Utc) throw new ArgumentException("EventTimestampUtc must be UTC when provided.", nameof(request));
        if (!string.IsNullOrWhiteSpace(request.TargetUrl) && (!Uri.TryCreate(request.TargetUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))) throw new ArgumentException("TargetUrl must be a valid URL when provided.", nameof(request));
    }
}
