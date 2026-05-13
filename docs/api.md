# API

HookBridge exposes ASP.NET Core endpoints for event ingestion, administration, health, and stored webhook analysis results. Swagger/OpenAPI is available in development at `/swagger`.

## AI Analysis Result by Event ID

### Endpoint

```http
GET /api/ai-analysis/events/{eventId}
```

### Purpose

Retrieves the stored AI-generated analysis result for a webhook event. Use this endpoint when an operator or admin needs to inspect the AI summary, suspected root cause, retry recommendation, risk level, confidence score, and model metadata that were persisted after a webhook delivery failure was analyzed.

The response is a DTO designed for API consumers and does not expose the internal MongoDB entity or webhook payload data.

### Example request

```bash
curl http://localhost:5000/api/ai-analysis/events/evt_12345
```

### Example response

```json
{
  "success": true,
  "message": null,
  "data": {
    "id": "663f0c7a9f1e2a5a12345678",
    "eventId": "evt_12345",
    "correlationId": "corr_789",
    "source": "HookBridge.Worker",
    "eventType": "WebhookDeliveryFailed",
    "failureReason": "Too Many Requests",
    "aiSummary": "The target endpoint is rate limiting webhook delivery requests.",
    "rootCause": "HTTP 429 indicates the receiver is rejecting requests due to rate limits.",
    "aiRecommendation": "Retry using exponential backoff and reduce delivery concurrency for this endpoint.",
    "riskLevel": "Medium",
    "confidenceScore": 0.86,
    "suggestedRetryAction": "RetryWithBackoff",
    "isRetryRecommended": true,
    "model": "llama3",
    "provider": "Ollama",
    "createdAtUtc": "2026-05-13T10:30:00Z"
  },
  "errors": null,
  "traceId": "..."
}
```

### Response status codes

| Status | Meaning |
| --- | --- |
| `200 OK` | An AI analysis result exists for the supplied `eventId`. |
| `400 Bad Request` | The `eventId` route value is empty or invalid. |
| `404 Not Found` | No AI analysis result exists for the supplied `eventId`. |
| `500 Internal Server Error` | An unexpected error occurred while retrieving the AI analysis result. |
