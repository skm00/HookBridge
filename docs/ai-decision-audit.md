# AI Decision Audit Trail

HookBridge stores a centralized audit trail for AI decisions so operators can trace every AI recommendation, fallback, human approval, safe-mode evaluation, and agent output during debugging, compliance review, and production safety investigations.

## MongoDB collection

Audit records are stored in MongoDB collection `ai_decision_audit_records`.

Each record includes identifiers (`auditId`, `eventId`, `correlationId`, customer/subscription/endpoint identifiers), agent metadata, decision type, decision/risk/confidence fields, approval and safe-mode status, fallback details, prompt/model metadata, summaries, recommendations, reason codes, UTC creation time, and sanitized metadata.

## Audited decision types

Supported `decisionType` values are:

- `RetryDecision`
- `SecurityDecision`
- `TransformationDecision`
- `ObservabilityDecision`
- `OrchestrationDecision`
- `AutoRemediationRecommendation`
- `AnomalyDetection`
- `EndpointRiskScore`
- `PayloadSchemaDetection`
- `JsonToDtoSuggestion`
- `ValidationRuleGeneration`
- `NaturalLanguageQuery`
- `HumanApproval`
- `SafeModeEvaluation`
- `FallbackDecision`

## API endpoints

- `GET /api/ai-audit` searches audit records.
- `GET /api/ai-audit/{auditId}` returns one audit record by audit id.
- `GET /api/ai-audit/events/{eventId}` returns audit records for an event.
- `GET /api/ai-audit/correlations/{correlationId}` returns audit records for a correlation id.

Search supports these filters: `eventId`, `correlationId`, `customerId`, `customerIdType`, `subscriptionId`, `endpointId`, `environment`, `agentName`, `decisionType`, `riskLevel`, `confidenceLevel`, `suggestedAction`, `requiresApproval`, `approvalStatus`, `safeModeDecision`, `isActionAllowed`, `usedFallback`, `fallbackReason`, `promptName`, `promptVersion`, `model`, `provider`, `fromUtc`, `toUtc`, `pageNumber`, and `pageSize`.

## Sensitive-data rules

Audit metadata must not store raw payloads, raw headers, secrets, tokens, cookies, or generated code. HookBridge masks sensitive metadata keys including `Authorization`, `Cookie`, `Set-Cookie`, `Token`, `Secret`, `Password`, `Api-Key`, `X-API-Key`, `ClientSecret`, `AccessToken`, and `ConnectionString`. Metadata is truncated according to `AiDecisionAudit:MaxMetadataLength`.

## Example audit record

```json
{
  "auditId": "aud_1001",
  "eventId": "evt_12345",
  "correlationId": "corr_789",
  "customerId": "cust_123",
  "subscriptionId": "sub_456",
  "endpointId": "endpoint_789",
  "environment": "qa",
  "agentName": "RetryAgent",
  "decisionType": "RetryDecision",
  "decision": "RetryWithExponentialBackoff",
  "riskLevel": "Medium",
  "confidenceScore": 0.82,
  "confidenceLevel": "High",
  "suggestedAction": "RetryWithBackoff",
  "requiresApproval": false,
  "safeModeDecision": "Allowed",
  "isActionAllowed": false,
  "usedAi": false,
  "usedFallback": true,
  "fallbackReason": "AiDisabled",
  "promptName": "WebhookFailureAnalysis",
  "promptVersion": "v1.0.0",
  "promptHash": "sha256:abc123...",
  "model": "llama3",
  "provider": "Ollama",
  "summary": "HTTP 429 indicates rate limiting.",
  "recommendation": "Retry with exponential backoff.",
  "reasonCodes": ["RateLimited"],
  "createdBy": "HookBridge.AI.Worker",
  "createdAtUtc": "2026-05-14T10:31:00Z"
}
```

## Example API request and response

Request:

```http
GET /api/ai-audit?eventId=evt_12345&decisionType=RetryDecision&pageNumber=1&pageSize=50
```

Response shape:

```json
{
  "success": true,
  "data": [
    {
      "auditId": "aud_1001",
      "eventId": "evt_12345",
      "decisionType": "RetryDecision",
      "confidenceScore": 0.82,
      "usedFallback": true,
      "createdAtUtc": "2026-05-14T10:31:00Z"
    }
  ]
}
```
