# Dead-Letter AI Analysis

Dead-Letter AI Analysis explains why webhook events reached dead-letter storage and provides advisory replay guidance. It combines deterministic safety rules with optional AI analysis, but it never replays events or applies production actions directly.

## Purpose

The service accepts dead-letter event context, classifies replay safety, recommends next steps, calculates confidence, evaluates AI Safe Mode, and records the result for operators. Sensitive payload, header, cookie, token, and response body values are masked or truncated before being sent to an AI prompt.

## Replay safety rules

Deterministic fallback rules are always available:

| Condition | Suggested action | Replay safety |
| --- | --- | --- |
| `RetryCount >= MaxRetryCount` | Adds `MaxRetryReached` reason code | Existing classification retained |
| HTTP `429` | `ReplayWithBackoff` | `ReplayWithCaution` |
| HTTP `408` or `504` | `ReplayWithBackoff` | `ReplayWithCaution` |
| HTTP `500`, `502`, or `503` | `ReplayWithBackoff` | `ReplayWithCaution` |
| HTTP `400` | `FixPayloadBeforeReplay` | `RequiresFixBeforeReplay` |
| HTTP `401` or `403` | `FixAuthenticationBeforeReplay` | `RequiresFixBeforeReplay` |
| HTTP `404` | `FixEndpointBeforeReplay` | `RequiresFixBeforeReplay` |
| `IsSuspicious = true` | `Quarantine` | `DoNotReplay` |
| `IsReplay = true` | `Quarantine` | `DoNotReplay` |
| `IsDuplicate = true` | `KeepInDeadLetter` | `DoNotReplay` |
| Unknown status | `RequireManualReview` | `RequiresManualReview` |

Safety constraints prevent suspicious or replay events from being marked safe, prevent direct replay for authentication and payload-contract failures, and force manual approval for high/critical risk or replay-related actions.

## Approval and Safe Mode behavior

Replay recommendations are advisory. Any replay-related action requires human approval by default. High-risk, critical-risk, and suspicious-event recommendations also require approval. AI Safe Mode is evaluated before replay-related recommendations can be considered allowed, and blocked or approval-required Safe Mode decisions keep `isActionAllowed` false.

## Kafka and MongoDB

- Kafka consume topic: `hookbridge.ai.deadletter-analysis`
- AI decision publish topic: `hookbridge.ai.decisions`
- MongoDB collection: `dead_letter_ai_analysis_results`

## API endpoints

- `GET /api/ai-deadletter/events/{eventId}`
- `GET /api/ai-deadletter/{deadLetterId}`
- `POST /api/ai-deadletter/analyze`

## Example request

```json
{
  "deadLetterId": "dlq_1001",
  "eventId": "evt_429_001",
  "correlationId": "corr_429_001",
  "customerId": "cust_123",
  "subscriptionId": "sub_456",
  "endpointId": "endpoint_789",
  "environment": "qa",
  "eventType": "WebhookDeliveryFailed",
  "targetUrl": "https://customer.example.com/webhook",
  "httpMethod": "POST",
  "statusCode": 429,
  "failureReason": "Too Many Requests",
  "retryCount": 5,
  "maxRetryCount": 5,
  "failedAtUtc": "2026-05-14T10:30:00Z",
  "movedToDeadLetterAtUtc": "2026-05-14T10:45:00Z"
}
```

## Example response

```json
{
  "deadLetterId": "dlq_1001",
  "eventId": "evt_429_001",
  "correlationId": "corr_429_001",
  "rootCause": "Target endpoint returned HTTP 429 rate limiting responses.",
  "summary": "Event reached dead-letter due to rate limiting.",
  "recommendation": "Replay only after reducing delivery concurrency and using exponential backoff.",
  "replaySafety": "ReplayWithCaution",
  "suggestedAction": "ReplayWithBackoff",
  "riskLevel": "Medium",
  "confidenceScore": 0.78,
  "confidenceLevel": "High",
  "requiresApproval": true,
  "safeModeDecision": "RequiresApproval",
  "isActionAllowed": false,
  "reasonCodes": ["MaxRetryReached", "RateLimited", "ManualReviewRequired"],
  "generatedAtUtc": "2026-05-14T10:46:00Z",
  "promptName": "DeadLetterAiAnalysis",
  "promptVersion": "v1.0.0"
}
```
