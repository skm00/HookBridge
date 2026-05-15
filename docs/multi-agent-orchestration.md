# Multi-Agent AI Orchestration

HookBridge multi-agent orchestration coordinates multiple deterministic and AI-assisted agents for a webhook event and stores one unified decision result.

## Purpose

The orchestrator centralizes agent execution so operators can inspect one response that includes:

- A short overall summary.
- The highest risk level returned by participating agents.
- A recommended next action that does **not** execute production changes automatically.
- Per-agent summaries, confidence, fallback use, duration, and isolated failures.
- Approval metadata when human review is required.

## Enabled agents

By default, orchestration is enabled and runs these agents:

- Retry Recommendation Agent
- Security Analysis Agent
- Duplicate/Replay Detection Agent
- Payload Schema Detection Agent
- Endpoint Risk Scoring Agent
- Anomaly Detection Agent

Log Summarization and Transformation Recommendation are disabled by default and can be enabled with `AiAgentOrchestration:EnableLogSummaryAgent` and `AiAgentOrchestration:EnableTransformationAgent`.

## Sequential vs parallel mode

`AiAgentOrchestration:Mode` controls execution:

- `Sequential` (default): runs agents one at a time for deterministic ordering and simpler debugging.
- `Parallel`: runs enabled agents concurrently to reduce orchestration latency.

Each agent has an independent timeout configured by `AiAgentOrchestration:AgentTimeoutSeconds` (default `30`). A timeout records a failed agent result and does not fail the whole orchestration.

## Decision rules

The orchestrator applies deterministic decision rules after collecting agent results:

- Critical security findings recommend `Quarantine`.
- High-risk replay findings recommend `Quarantine`.
- Duplicate findings that should be ignored are converted to `MoveToDeadLetter` because the orchestration action enum does not include an ignore action.
- HTTP `429` or retry-agent backoff recommendations map to `RetryWithBackoff`.
- Events at or above the max retry count map to `MoveToDeadLetter`.
- High or Critical overall risk maps to manual review when a stronger action was not already selected.

Overall risk is the highest successful agent risk. `Critical` overrides all other levels. `Unknown` is returned only when every agent fails or reports `Unknown`.

Confidence is the average confidence from successful agents, with small penalties for failed agents and fallback results, clamped between `0` and `1`.

## Approval behavior

Human approval is required when:

- Overall risk is `High` and `AiAgentOrchestration:RequireApprovalForHighRisk` is `true`.
- Overall risk is `Critical` and `AiAgentOrchestration:RequireApprovalForCriticalRisk` is `true`.

Both settings default to `true`. The worker creates an approval record when `RequiresApproval` is true and stores the generated `ApprovalId` with the orchestration result.

## Kafka and MongoDB

- Kafka orchestration topic: `hookbridge.ai.orchestration`
- MongoDB collection: `ai_agent_orchestration_results`

The worker consumes orchestration requests from Kafka, runs the orchestrator, persists the result, creates approval records as needed, and publishes an anomaly event for High/Critical results.

## API

Retrieve a stored orchestration result by event id:

```http
GET /api/ai-orchestration/events/{eventId}
```

Responses:

- `200 OK` when a result exists.
- `400 Bad Request` when `eventId` is empty.
- `404 Not Found` when no result exists.
- `500 Internal Server Error` for unexpected errors.

## Example request

```json
{
  "eventId": "evt_12345",
  "correlationId": "corr_789",
  "customerId": "cust_42",
  "customerIdType": "Tenant",
  "subscriptionId": "sub_123",
  "endpointId": "end_456",
  "environment": "Production",
  "eventType": "invoice.created",
  "source": "stripe",
  "targetUrl": "https://example.com/webhooks/stripe",
  "statusCode": 429,
  "failureReason": "Too Many Requests",
  "headers": {
    "content-type": "application/json"
  },
  "payload": {
    "id": "evt_12345"
  },
  "retryCount": 1,
  "maxRetryCount": 5,
  "receivedAtUtc": "2026-05-14T10:29:00Z"
}
```

## Example response

```json
{
  "eventId": "evt_12345",
  "correlationId": "corr_789",
  "overallSummary": "Webhook delivery failed due to rate limiting. No critical security issue was detected. Overall risk is Medium; recommended action is RetryWithBackoff.",
  "overallRiskLevel": "Medium",
  "recommendedAction": "RetryWithBackoff",
  "confidenceScore": 0.82,
  "requiresApproval": false,
  "approvalId": null,
  "agentResults": [
    {
      "agentName": "RetryRecommendationAgent",
      "isSuccessful": true,
      "summary": "HTTP 429 indicates rate limiting.",
      "riskLevel": "Medium",
      "suggestedAction": "RetryWithBackoff",
      "confidenceScore": 0.86,
      "usedFallback": false,
      "errorMessage": null,
      "durationMs": 420
    },
    {
      "agentName": "SecurityAnalysisAgent",
      "isSuccessful": true,
      "summary": "No suspicious payload pattern detected.",
      "riskLevel": "Low",
      "suggestedAction": "Allow",
      "confidenceScore": 0.78,
      "usedFallback": true,
      "errorMessage": null,
      "durationMs": 35
    }
  ],
  "generatedAtUtc": "2026-05-14T10:30:00Z"
}
```

## Logging and safety

Orchestration logs structured metadata for lifecycle events: orchestration started/completed, agent started/completed/failed/timed out, approval required, and anomaly published. Logs intentionally avoid full payloads, headers, secrets, and tokens.
