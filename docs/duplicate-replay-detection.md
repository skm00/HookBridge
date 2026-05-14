# Webhook duplicate/replay detection

HookBridge AI Worker includes deterministic duplicate and replay detection for webhook events. The feature does not call an LLM: it compares stable identifiers, normalized payload hashes, signature hashes, and UTC timestamps against MongoDB fingerprints.

## Purpose

Duplicate/replay detection helps prevent already-processed webhook events from being forwarded again and flags replay attempts where a valid-looking message or signature is reused outside the accepted time window.

## Kafka and MongoDB

- Input Kafka topic: `hookbridge.ai.duplicate-replay-detection`
- Optional high-risk anomaly output topic: `hookbridge.ai.anomalies`
- MongoDB fingerprint collection: `webhook_event_fingerprints`
- Fingerprints expire through a TTL index on `expiresAtUtc`.

## Configuration

```json
{
  "DuplicateReplayDetection": {
    "Enabled": true,
    "FingerprintTtlHours": 72,
    "ReplayWindowMinutes": 15,
    "FutureTimestampToleranceMinutes": 5,
    "HighFrequencyThreshold": 5,
    "HighFrequencyWindowSeconds": 60,
    "HashAlgorithm": "SHA256"
  }
}
```

## Detection rules and score formula

Scores are additive and clamped between `0` and `100`.

| Rule | Score |
| --- | ---: |
| Same `EventId` within TTL | +50 |
| Same `CorrelationId` within TTL | +25 |
| Same normalized `PayloadHash` for the same customer/subscription within TTL | +30 |
| Same `SignatureHash` inside the replay window | +40 |
| `EventTimestampUtc` older than `ReplayWindowMinutes` | +35 |
| `EventTimestampUtc` beyond `FutureTimestampToleranceMinutes` | +25 |
| Same payload hash repeated at high frequency in the short window | +30 |

## Risk level thresholds

| DetectionScore | RiskLevel |
| --- | --- |
| 0-20 | Low |
| 21-50 | Medium |
| 51-80 | High |
| 81-100 | Critical |
| Insufficient identifying data | Unknown |

## Suggested actions

- Clear same-EventId duplicate: `IgnoreDuplicate`
- Expired timestamp or signature replay: `Reject`
- Low risk: `Allow`
- Medium risk: `Monitor`
- High risk: `RequireManualReview`
- Critical risk: `Quarantine`

## Example request

```json
{
  "eventId": "evt_1001",
  "correlationId": "corr_2001",
  "customerId": "cust_123",
  "customerIdType": "MDM",
  "subscriptionId": "sub_456",
  "endpointId": "endpoint_789",
  "environment": "qa",
  "eventType": "OrderCreated",
  "source": "HookBridge.API",
  "targetUrl": "https://customer.example.com/webhook",
  "signature": "sha256=abc123",
  "payload": {
    "orderId": "ORD-1001",
    "status": "Created"
  },
  "eventTimestampUtc": "2026-05-14T10:25:00Z",
  "receivedAtUtc": "2026-05-14T10:30:00Z"
}
```

## Example response

```json
{
  "eventId": "evt_1001",
  "correlationId": "corr_2001",
  "customerId": "cust_123",
  "subscriptionId": "sub_456",
  "endpointId": "endpoint_789",
  "isDuplicate": true,
  "isReplay": false,
  "duplicateReason": "SameEventId",
  "replayReason": "None",
  "payloadHash": "sha256:...",
  "signatureHash": "sha256:...",
  "detectionScore": 50,
  "riskLevel": "Medium",
  "suggestedAction": "IgnoreDuplicate",
  "summary": "Deterministic duplicate/replay checks matched duplicate reason SameEventId and replay reason None.",
  "recommendation": "Ignore this duplicate event and do not forward it again.",
  "detectedAtUtc": "2026-05-14T10:30:01Z"
}
```

## Privacy and safety

The hash service normalizes JSON payloads when possible before SHA-256 hashing; if parsing fails it hashes the raw payload string. The worker logs structured metadata only and does not log raw payloads, signatures, headers, or secrets.
