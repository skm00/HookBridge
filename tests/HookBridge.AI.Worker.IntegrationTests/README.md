# HookBridge AI Worker sample webhook failure integration tests

This project contains end-to-end integration tests for the `HookBridge.AI.Worker` Kafka/MongoDB processing path using realistic webhook failure samples.

## What the tests cover

The sample tests publish webhook failure messages to Kafka topics and assert persisted MongoDB output for:

- HTTP 429 rate limiting retry recommendations.
- HTTP 500 receiver/server-side failures.
- HTTP 401 authentication failures.
- HTTP 404 missing endpoint/resource failures.
- Timeout failures.
- Max retry reached dead-letter recommendations.
- Invalid payload resilience.
- Suspicious payload security findings and anomaly persistence.
- Duplicate and replay detection anomaly records.
- LLM unavailable and invalid JSON fallback behavior.
- Natural language failure summaries backed by seeded AI analysis results.

## Test data

Sample JSON files live in [`TestData/SampleWebhookFailures`](TestData/SampleWebhookFailures):

- `http-429-rate-limit-failure.json`
- `http-500-server-error-failure.json`
- `http-401-auth-failure.json`
- `http-404-not-found-failure.json`
- `timeout-failure.json`
- `max-retry-reached-failure.json`
- `invalid-payload-failure.json`
- `suspicious-payload-failure.json`
- `duplicate-event-failure.json`
- `replay-event-failure.json`

Each test assigns a unique `EventId` at runtime so repeated local and CI runs do not collide.

## Requirements

- .NET 8 SDK.
- Docker available to the current user.
- Testcontainers can pull/start:
  - `confluentinc/cp-kafka:7.6.1`
  - `mongo:7.0`

No real Ollama instance is required. The fixture replaces the real local LLM client with `FakeLocalLlmClient`, which can return deterministic JSON or simulate provider-unavailable, timeout, and invalid-JSON responses.

## Running locally

From the repository root:

```bash
dotnet test tests/HookBridge.AI.Worker.IntegrationTests/HookBridge.AI.Worker.IntegrationTests.csproj \
  --filter Category=Integration \
  --collect:"XPlat Code Coverage"
```

To run only Kafka/Mongo-tagged tests:

```bash
dotnet test tests/HookBridge.AI.Worker.IntegrationTests/HookBridge.AI.Worker.IntegrationTests.csproj \
  --filter "Category=Kafka&Category=Mongo"
```

## Skipping locally

Set `SKIP_INTEGRATION_TESTS=true` to bypass container startup and return early from the integration test methods:

```bash
SKIP_INTEGRATION_TESTS=true dotnet test tests/HookBridge.AI.Worker.IntegrationTests/HookBridge.AI.Worker.IntegrationTests.csproj \
  --filter Category=Integration
```

## Cleanup behavior

The fixture deletes AI-related MongoDB collections before each test, including:

- `ai_analysis_results`
- `ai_anomaly_records`
- `ai_security_analysis_results`
- `webhook_event_fingerprints`
- `ai_recommendation_approvals`
- Other AI worker result collections used by the service registration.

Kafka and MongoDB containers are disposed by the xUnit fixture after the collection completes.
