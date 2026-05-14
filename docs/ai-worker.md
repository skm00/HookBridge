# HookBridge AI Worker

`HookBridge.AI.Worker` is a .NET 8 Worker Service for future Agentic AI background processing in HookBridge. The worker starts cleanly, binds AI configuration through `IOptions<AiOptions>`, registers Microsoft Semantic Kernel services, verifies kernel creation when AI is enabled, exposes an in-process health status class, and remains idle until the host is cancelled.

## Purpose

The AI worker gives HookBridge a separate runtime for AI-oriented background jobs without coupling those jobs to the API gateway or existing webhook delivery workers. Future capabilities can include event enrichment, summarization, intelligent routing, anomaly classification, and operational assistant workflows.

## Semantic Kernel setup

HookBridge uses Microsoft Semantic Kernel as the AI orchestration layer. The worker registers `IKernelFactory` through `AddAiKernelServices()` and uses `SemanticKernelFactory` to build a `Kernel` configured for the selected AI provider.

Current provider support:

- **Ollama** for local model hosting.

When `AI:Enabled` is `true`, worker startup calls `IKernelFactory.CreateKernel()` once to verify that Semantic Kernel can be constructed from configuration. This does not send a prompt to Ollama; it only validates configuration and builds the in-process kernel. When `AI:Enabled` is `false`, Semantic Kernel initialization is skipped and the worker logs that AI is disabled.

## Required configuration

The worker uses the `AI` configuration section. `Host.CreateApplicationBuilder` loads `appsettings.json`, environment-specific files such as `appsettings.Development.json` or `appsettings.Production.json`, and environment variables, so every setting can be overridden with the .NET double-underscore environment variable format.

```json
{
  "AI": {
    "Enabled": true,
    "Provider": "Ollama",
    "Model": "llama3",
    "Endpoint": "http://localhost:11434",
    "TimeoutSeconds": 30,
    "MaxRetries": 3,
    "SystemPrompt": "You are HookBridge AI, an assistant for webhook failure analysis and event processing.",
    "EnablePromptLogging": false,
    "EnableFallback": true,
    "LlmRequestTimeoutSeconds": 30,
    "MaxFallbackSummaryLength": 1000,
    "HealthCheckPrompt": "Say HookBridge AI is ready",
    "MaxPromptPayloadLength": 4000,
    "MaskSensitiveValues": true,
    "MaxLogEntriesForSummary": 100,
    "MaxLogMessageLength": 2000
  }
}
```

Configuration keys:

| Key | Environment variable | Default | Required when enabled | Description |
| --- | --- | --- | --- | --- |
| `AI:Enabled` | `AI__Enabled` | `true` in base and development, `false` in production | Yes | Enables or disables Semantic Kernel initialization. Set to `false` to run the worker without AI services. |
| `AI:Provider` | `AI__Provider` | `Ollama` | Yes | AI provider name. Currently supported value: `Ollama`. |
| `AI:Model` | `AI__Model` | `llama3` | Yes | Ollama model name to use for chat completion. |
| `AI:Endpoint` | `AI__Endpoint` | `http://localhost:11434` | Yes | Absolute HTTP or HTTPS URL for the Ollama API endpoint. |
| `AI:TimeoutSeconds` | `AI__TimeoutSeconds` | `30` | Always validated | Timeout budget, in seconds, for AI provider operations. Must be greater than `0`. |
| `AI:MaxRetries` | `AI__MaxRetries` | `3` | Always validated | Maximum retry attempts for AI provider operations. Must be `0` or greater. |
| `AI:SystemPrompt` | `AI__SystemPrompt` | HookBridge webhook analysis assistant prompt | No | System instruction used by future AI analysis prompts. |
| `AI:EnablePromptLogging` | `AI__EnablePromptLogging` | `false` | No | Enables prompt logging for troubleshooting. Keep disabled by default to avoid leaking event payloads or sensitive data into logs. |
| `AI:EnableFallback` | `AI__EnableFallback` | `true` | Always validated | Enables deterministic fallback responses when AI is disabled or the LLM provider/model is unavailable. |
| `AI:LlmRequestTimeoutSeconds` | `AI__LlmRequestTimeoutSeconds` | `30` | Always validated | Timeout budget, in seconds, for each LLM generation request before fallback is used. Must be greater than `0`. |
| `AI:MaxFallbackSummaryLength` | `AI__MaxFallbackSummaryLength` | `1000` | Always validated | Maximum characters retained in deterministic fallback summary and recommendation fields. Must be greater than `0`. |
| `AI:HealthCheckPrompt` | `AI__HealthCheckPrompt` | `Say HookBridge AI is ready` | No | Lightweight prompt reserved for future provider health checks. |
| `AI:MaxPromptPayloadLength` | `AI__MaxPromptPayloadLength` | `4000` | Always validated | Maximum characters retained for each prompt payload, response body, and header value before truncation. Must be greater than `0`. |
| `AI:MaskSensitiveValues` | `AI__MaskSensitiveValues` | `true` | No | Masks sensitive header and log values before prompt construction. Keep enabled to avoid exposing secrets. |
| `AI:MaxLogEntriesForSummary` | `AI__MaxLogEntriesForSummary` | `100` | Always validated | Maximum log entries included in each AI log summarization prompt. Must be greater than `0`. |
| `AI:MaxLogMessageLength` | `AI__MaxLogMessageLength` | `2000` | Always validated | Maximum characters retained for each log message and exception before truncation. Must be greater than `0`. |

Options are validated during startup with the .NET options pattern using data annotations, custom conditional validation, and `ValidateOnStart()`. When AI is enabled, `Provider`, `Model`, and `Endpoint` must be present. `TimeoutSeconds`, `MaxPromptPayloadLength`, `MaxLogEntriesForSummary`, and `MaxLogMessageLength` must be greater than `0`, and `MaxRetries` must be `0` or greater. Invalid configuration fails startup clearly before the worker begins processing.

### Development example

`appsettings.Development.json` enables AI with local Ollama defaults and prompt logging disabled:

```json
{
  "AI": {
    "Enabled": true,
    "Provider": "Ollama",
    "Model": "llama3",
    "Endpoint": "http://localhost:11434",
    "TimeoutSeconds": 30,
    "MaxRetries": 3,
    "SystemPrompt": "You are HookBridge AI, an assistant for webhook failure analysis and event processing.",
    "EnablePromptLogging": false,
    "EnableFallback": true,
    "LlmRequestTimeoutSeconds": 30,
    "MaxFallbackSummaryLength": 1000,
    "HealthCheckPrompt": "Say HookBridge AI is ready",
    "MaxPromptPayloadLength": 4000,
    "MaskSensitiveValues": true,
    "MaxLogEntriesForSummary": 100,
    "MaxLogMessageLength": 2000
  }
}
```

### Production recommendation

`appsettings.Production.json` keeps AI disabled by default. Production deployments should enable AI only after a provider endpoint, model, timeouts, retries, logging policy, and data-handling review are explicitly configured through deployment-specific configuration or secrets. Do not enable prompt logging in production unless a short-lived incident response process explicitly requires it.

### Environment variable examples

```bash
AI__Enabled=true \
AI__Provider=Ollama \
AI__Model=llama3 \
AI__Endpoint=http://localhost:11434 \
AI__TimeoutSeconds=30 \
AI__MaxRetries=3 \
AI__EnablePromptLogging=false \
AI__EnableFallback=true \
AI__LlmRequestTimeoutSeconds=30 \
AI__MaxFallbackSummaryLength=1000 \
AI__MaxPromptPayloadLength=4000 \
AI__MaskSensitiveValues=true \
AI__MaxLogEntriesForSummary=100 \
AI__MaxLogMessageLength=2000 \
dotnet run --project src/HookBridge.AI.Worker/HookBridge.AI.Worker.csproj
```

To disable AI safely while keeping the worker process available:

```bash
AI__Enabled=false \
dotnet run --project src/HookBridge.AI.Worker/HookBridge.AI.Worker.csproj
```

## Deterministic fallback behavior

Fallback exists so webhook processing remains safe when local AI is unavailable, slow, misconfigured, or produces unusable output. The worker treats expected LLM availability failures as data through `LlmResponseResult` instead of crashing the worker. Fallback responses include `fallback` metadata with `usedFallback`, `fallbackReason`, `fallbackMessage`, `provider`, `model`, and a UTC generation timestamp.

Fallback is used for these conditions:

- AI is disabled (`AiDisabled`).
- The provider cannot be reached (`ProviderUnavailable`).
- The configured model is missing or Ollama reports a model-related error (`ModelUnavailable`).
- The request times out (`Timeout`).
- The provider returns a non-success status, empty response, or malformed response (`InvalidResponse`).
- The response body is not valid JSON when JSON is required (`InvalidJson`).
- Required provider/model/endpoint configuration is invalid (`ConfigurationError`).

### Fallback decision table

| Signal | Suggested action | Risk | Notes |
| --- | --- | --- | --- |
| HTTP `429` | `RetryWithBackoff` | `Medium` | Rate limiting should never retry immediately. |
| HTTP `408` or `504` | `RetryWithBackoff` | `Medium` | Timeout-style failures are treated as transient. |
| HTTP `500`, `502`, or `503` | `RetryWithBackoff` | `High` | Server-side failures are retryable with backoff but higher risk. |
| HTTP `400` | `RequireManualReview` | `Medium` | Bad requests may indicate payload/schema issues. |
| HTTP `401` or `403` | `RequireManualReview` | `High` | Authentication and authorization failures require operator review. |
| HTTP `404` | `MoveToDeadLetter` | `High` | The endpoint may be removed or misconfigured. |
| `RetryCount >= MaxRetryCount` | `MoveToDeadLetter` | `Critical` | Max retry budget takes precedence over status-code rules. |
| Unknown status | `RequireManualReview` | `Unknown` | Avoid unsafe automatic retries when evidence is incomplete. |

Log summary fallback is also deterministic: empty logs return a safe “no logs available” summary, error logs summarize the latest error after sensitive-value masking, warning-only logs summarize warning count, and logs without errors or warnings are marked low risk. Endpoint health scoring remains deterministic and does not require an LLM; fallback health summaries use `EndpointHealthScoringService`.

### Example fallback response

```json
{
  "eventId": "evt_12345",
  "correlationId": "corr_789",
  "aiSummary": "Fallback analysis was used. LLM provider was unavailable. Deterministic fallback rules were used.",
  "rootCause": "The target endpoint returned HTTP 429, which usually indicates rate limiting.",
  "aiRecommendation": "LLM provider was unavailable. Retry with exponential backoff and reduce delivery concurrency if rate limiting or transient failures continue.",
  "riskLevel": "Medium",
  "confidenceScore": 0.65,
  "suggestedRetryAction": "RetryWithBackoff",
  "isRetryRecommended": true,
  "generatedAtUtc": "2026-05-13T10:30:00Z",
  "model": "llama3",
  "provider": "Ollama",
  "fallback": {
    "usedFallback": true,
    "fallbackReason": "ProviderUnavailable",
    "fallbackMessage": "LLM provider was unavailable. Deterministic fallback rules were used.",
    "provider": "Ollama",
    "model": "llama3",
    "generatedAtUtc": "2026-05-13T10:30:00Z"
  }
}
```

### Disabling fallback

Fallback is enabled by default. To disable the setting for experiments or strict provider-validation environments, set:

```bash
AI__EnableFallback=false
```

The production recommendation is to keep fallback enabled, keep prompt logging disabled, set a conservative `AI__LlmRequestTimeoutSeconds`, and alert on fallback usage by reason/provider/model/duration. Fallback logs are structured and intentionally exclude full prompts, payloads, secrets, and headers.

## Ollama model example

Install and run Ollama locally, then pull a model:

```bash
ollama pull llama3
ollama serve
```

Run the worker with the matching model and endpoint:

```bash
AI__Enabled=true \
AI__Provider=Ollama \
AI__Model=llama3 \
AI__Endpoint=http://localhost:11434 \
dotnet run --project src/HookBridge.AI.Worker/HookBridge.AI.Worker.csproj
```

To disable AI safely while keeping the worker process available:

```bash
AI__Enabled=false \
dotnet run --project src/HookBridge.AI.Worker/HookBridge.AI.Worker.csproj
```

## Running locally

From the repository root:

```bash
dotnet run --project src/HookBridge.AI.Worker/HookBridge.AI.Worker.csproj
```

To override the model or endpoint for a local Ollama instance:

```bash
AI__Provider=Ollama \
AI__Model=llama3 \
AI__Endpoint=http://localhost:11434 \
dotnet run --project src/HookBridge.AI.Worker/HookBridge.AI.Worker.csproj
```

The worker logs a startup message, verifies Semantic Kernel creation when AI is enabled, waits for host cancellation, and logs a shutdown message during graceful termination.

## Health status

Because this project is a worker process rather than an ASP.NET Core web app, it exposes `AiWorkerHealthStatus` as an in-process health status class. The status reports whether AI processing is enabled and whether the provider, model, and endpoint configuration are usable.

## Kafka AI analysis topic

The AI worker consumes AI analysis events from Kafka topic `hookbridge.ai.analysis`. This topic carries normalized webhook failure and event-analysis requests from HookBridge services into the AI worker so analysis can happen asynchronously without slowing webhook ingestion or delivery retries.

Kafka settings are bound with `IOptions<AiKafkaOptions>` from the `AiKafka` configuration section:

```json
{
  "AiKafka": {
    "BootstrapServers": "localhost:9092",
    "SecurityProtocol": "Plaintext",
    "SaslMechanism": "",
    "SaslUsername": "",
    "SaslPassword": "",
    "AiAnalysisTopic": "hookbridge.ai.analysis",
    "ConsumerGroupId": "hookbridge-ai-worker",
    "EnableAutoCommit": false
  }
}
```

| Key | Environment variable | Default | Description |
| --- | --- | --- | --- |
| `AiKafka:BootstrapServers` | `AiKafka__BootstrapServers` | `localhost:9092` in local settings | Kafka bootstrap broker list. |
| `AiKafka:SecurityProtocol` | `AiKafka__SecurityProtocol` | `Plaintext` | Confluent.Kafka security protocol, for example `Plaintext`, `Ssl`, `SaslPlaintext`, or `SaslSsl`. |
| `AiKafka:SaslMechanism` | `AiKafka__SaslMechanism` | Empty | SASL mechanism when a SASL security protocol is used. |
| `AiKafka:SaslUsername` | `AiKafka__SaslUsername` | Empty | SASL username when a SASL security protocol is used. |
| `AiKafka:SaslPassword` | `AiKafka__SaslPassword` | Empty | SASL password when a SASL security protocol is used. Store as a secret outside source control. |
| `AiKafka:AiAnalysisTopic` | `AiKafka__AiAnalysisTopic` | `hookbridge.ai.analysis` | AI analysis event topic name. |
| `AiKafka:ConsumerGroupId` | `AiKafka__ConsumerGroupId` | `hookbridge-ai-worker` | Consumer group used by `HookBridge.AI.Worker`. |
| `AiKafka:EnableAutoCommit` | `AiKafka__EnableAutoCommit` | `false` | Whether Kafka offsets are auto-committed by the client. |

Example `AiAnalysisEventDto` message payload:

```json
{
  "eventId": "evt_01HXZ9R8J6K8BNK7Y5J6W5N4M2",
  "correlationId": "corr_01HXZ9R8J6K8BNK7Y5J6W5N4M2",
  "source": "hookbridge.worker",
  "eventType": "webhook.delivery.failed",
  "failureReason": "Target endpoint returned HTTP 500 after retry budget was exhausted.",
  "payload": "{\"tenantId\":\"tenant_123\",\"subscriptionId\":\"sub_456\",\"statusCode\":500}",
  "createdAtUtc": "2026-05-13T10:15:30+00:00"
}
```

The AI analysis producer serializes this DTO as JSON and uses `CorrelationId` as the Kafka message key when it is present; otherwise it falls back to `EventId`. Producer and consumer abstractions (`IAiAnalysisProducer` and `IAiAnalysisConsumer`) are registered in DI and are mockable for unit or integration tests.

### Local Kafka test command

With a local Kafka broker listening on `localhost:9092`, create the topic and publish a sample message with the standard Kafka CLI tools:

```bash
kafka-topics --bootstrap-server localhost:9092 --create --if-not-exists --topic hookbridge.ai.analysis --partitions 3 --replication-factor 1
printf '%s\n' '{"eventId":"evt-local-1","correlationId":"corr-local-1","source":"local-cli","eventType":"webhook.delivery.failed","failureReason":"HTTP 500","payload":"{}","createdAtUtc":"2026-05-13T10:15:30+00:00"}' \
  | kafka-console-producer --bootstrap-server localhost:9092 --topic hookbridge.ai.analysis --property parse.key=false
```

## MongoDB AI analysis result storage

The AI worker stores each consumed Kafka AI analysis event as an `AiAnalysisResult` document in MongoDB. The default collection name is `ai_analysis_results`.

MongoDB settings are bound with `IOptions<AiMongoOptions>` from the `AiMongo` configuration section and validated during startup:

```json
{
  "AiMongo": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "hookbridge_ai",
    "AiAnalysisResultsCollectionName": "ai_analysis_results"
  }
}
```

| Key | Environment variable | Default/local example | Description |
| --- | --- | --- | --- |
| `AiMongo:ConnectionString` | `AiMongo__ConnectionString` | `mongodb://localhost:27017` | Required MongoDB connection string for the AI worker. Store production credentials as secrets outside source control. |
| `AiMongo:DatabaseName` | `AiMongo__DatabaseName` | `hookbridge_ai` | Required database containing AI worker collections. |
| `AiMongo:AiAnalysisResultsCollectionName` | `AiMongo__AiAnalysisResultsCollectionName` | `ai_analysis_results` | Required collection used for stored AI analysis result documents. |

The worker creates indexes for AI analysis lookups at startup:

- `eventId` ascending
- `correlationId` ascending
- `createdAtUtc` descending

Example stored document:

```json
{
  "_id": "6644a3f65d3f2e0d9f9a8b01",
  "eventId": "evt_01HXZ9R8J6K8BNK7Y5J6W5N4M2",
  "correlationId": "corr_01HXZ9R8J6K8BNK7Y5J6W5N4M2",
  "source": "hookbridge.worker",
  "eventType": "webhook.delivery.failed",
  "failureReason": "Target endpoint returned HTTP 500 after retry budget was exhausted.",
  "aiSummary": "AI analysis placeholder created for failure: Target endpoint returned HTTP 500 after retry budget was exhausted.",
  "aiRecommendation": "Review the webhook payload, delivery history, and target endpoint health before retrying.",
  "riskLevel": "Unknown",
  "confidenceScore": 0,
  "model": "llama3",
  "provider": "Ollama",
  "createdAtUtc": "2026-05-13T10:16:00Z"
}
```

Local MongoDB example using Docker:

```bash
docker run --name hookbridge-mongo -p 27017:27017 -d mongo:7
AiMongo__ConnectionString=mongodb://localhost:27017 \
AiMongo__DatabaseName=hookbridge_ai \
AiMongo__AiAnalysisResultsCollectionName=ai_analysis_results \
dotnet run --project src/HookBridge.AI.Worker/HookBridge.AI.Worker.csproj
```

## Webhook failure analysis DTOs

HookBridge AI uses dedicated webhook failure analysis DTOs to keep the LLM-facing contract clean, reusable, and decoupled from Kafka and MongoDB storage details. `WebhookFailureAnalysisRequestDto` represents the sanitized failure context sent for analysis, including event identifiers, subscription/customer context, endpoint details, retry state, HTTP status, headers, payload/body snippets, and the UTC failure timestamp. `WebhookFailureAnalysisResponseDto` captures the AI output, including summary, root cause, recommendation, risk level, retry guidance, confidence, generation timestamp, model, and provider.

Header fields are intended for already-sanitized metadata only. Avoid storing or logging sensitive header values such as `Authorization`, API keys, cookies, or signatures. Keep prompt logging disabled unless an incident response workflow explicitly requires it and sensitive data has been redacted.

Validation enforces that `eventId` and `eventType` are present, `statusCode` is between `100` and `599` when supplied, retry counts are non-negative, `failedAtUtc` uses UTC, and `targetUrl` is an absolute HTTP or HTTPS URL when supplied.

### Example failure analysis request

```json
{
  "eventId": "evt_01HXAMPLE123",
  "correlationId": "corr_01HXAMPLE123",
  "subscriptionId": "sub_123",
  "customerId": "tenant_456",
  "customerIdType": "TenantId",
  "eventType": "webhook.delivery.failed",
  "source": "hookbridge.worker",
  "targetUrl": "https://api.example.com/webhooks/orders",
  "httpMethod": "POST",
  "statusCode": 500,
  "errorMessage": "Internal Server Error",
  "failureReason": "Target endpoint returned HTTP 500 after retry attempt 2.",
  "retryCount": 2,
  "maxRetryCount": 5,
  "requestHeaders": {
    "content-type": "application/json"
  },
  "responseHeaders": {
    "retry-after": "30"
  },
  "requestPayload": "{\"orderId\":\"ord_123\",\"status\":\"paid\"}",
  "responseBody": "{\"error\":\"temporarily unavailable\"}",
  "failedAtUtc": "2026-05-13T10:15:30Z"
}
```

### Example failure analysis response

```json
{
  "eventId": "evt_01HXAMPLE123",
  "correlationId": "corr_01HXAMPLE123",
  "aiSummary": "The target endpoint returned repeated HTTP 500 responses, which indicates a likely server-side outage or unhandled exception.",
  "rootCause": "The downstream webhook receiver appears temporarily unavailable.",
  "aiRecommendation": "Check the receiver health and retry with exponential backoff. Move the event to dead-letter if the endpoint continues to fail after the configured retry limit.",
  "riskLevel": "High",
  "confidenceScore": 0.91,
  "suggestedRetryAction": "RetryWithBackoff",
  "isRetryRecommended": true,
  "generatedAtUtc": "2026-05-13T10:16:02Z",
  "model": "llama3.1",
  "provider": "Ollama"
}
```

## Webhook failure explanation prompt

`WebhookFailurePromptBuilder` creates the reusable prompt used to ask an AI provider to explain failed webhook deliveries. The builder accepts a `WebhookFailureAnalysisRequestDto`, normalizes optional values, masks configured sensitive headers, truncates large payload fields, and emits a deterministic instruction that asks the model to return a `WebhookFailureAnalysisResponseDto`-compatible JSON object.

The prompt tells the model to analyze:

- HTTP status code, error message, and failure reason.
- Retry count and max retry count.
- Target URL and event type.
- Request headers, response headers, request payload, and response body context.
- Whether another retry is safe.
- Whether manual review is required.

### Prompt safety rules

The prompt builder and prompt template are designed for operational safety:

- Missing or null optional DTO fields are rendered as `[not provided]` so the model is told not to invent data.
- Sensitive header values are masked by default for header names containing `Authorization`, `Cookie`, `Set-Cookie`, `X-API-Key`, `Api-Key`, `Token`, `Secret`, or `Password`.
- `AI:MaskSensitiveValues` controls whether sensitive header masking is applied. Keep this enabled outside narrow local debugging scenarios.
- `AI:MaxPromptPayloadLength` limits each large prompt value, including request payloads and response bodies. The default is `4000` characters.
- The prompt explicitly instructs the model not to expose secrets and not to reconstruct masked values.
- The prompt asks for strict JSON only, with no markdown, prose, comments, or code fences.
- `riskLevel` must be one of `Unknown`, `Low`, `Medium`, `High`, or `Critical`.
- `suggestedRetryAction` must be one of `None`, `RetryImmediately`, `RetryWithBackoff`, `MoveToDeadLetter`, `PauseEndpoint`, or `RequireManualReview`.
- `confidenceScore` must be a number between `0` and `1`.
- Recommendations should remain short and actionable.

Prompt safety configuration is part of the `AI` section:

```json
{
  "AI": {
    "MaxPromptPayloadLength": 4000,
    "MaskSensitiveValues": true,
    "MaxLogEntriesForSummary": 100,
    "MaxLogMessageLength": 2000
  }
}
```

### Example input DTO

```json
{
  "eventId": "evt_01HXZ9R8J6K8BNK7Y5J6W5N4M2",
  "correlationId": "corr_01HXZ9R8J6K8BNK7Y5J6W5N4M2",
  "subscriptionId": "sub_456",
  "customerId": "tenant_123",
  "customerIdType": "TenantId",
  "eventType": "payment.succeeded",
  "source": "hookbridge.worker",
  "targetUrl": "https://merchant.example/webhooks",
  "httpMethod": "POST",
  "statusCode": 429,
  "errorMessage": "Too Many Requests",
  "failureReason": "Target endpoint returned HTTP 429 after several delivery attempts.",
  "retryCount": 3,
  "maxRetryCount": 5,
  "requestHeaders": {
    "Content-Type": "application/json",
    "Authorization": "[masked before prompt]"
  },
  "responseHeaders": {
    "Retry-After": "60"
  },
  "requestPayload": "{\"eventType\":\"payment.succeeded\"}",
  "responseBody": "Too Many Requests",
  "failedAtUtc": "2026-05-13T10:15:30Z"
}
```

### Example expected AI JSON response

```json
{
  "eventId": "evt_01HXZ9R8J6K8BNK7Y5J6W5N4M2",
  "correlationId": "corr_01HXZ9R8J6K8BNK7Y5J6W5N4M2",
  "aiSummary": "The webhook target rejected delivery due to rate limiting after multiple attempts.",
  "rootCause": "HTTP 429 indicates the target endpoint is throttling requests, and the retry count shows repeated attempts.",
  "aiRecommendation": "Retry with backoff and honor any Retry-After guidance before escalating.",
  "riskLevel": "Medium",
  "confidenceScore": 0.86,
  "suggestedRetryAction": "RetryWithBackoff",
  "isRetryRecommended": true,
  "generatedAtUtc": "2026-05-13T10:16:00Z"
}
```

## AI retry recommendation service

`IAiRetryRecommendationService` provides deterministic, testable webhook retry recommendations for failed deliveries. The implementation, `AiRetryRecommendationService`, accepts a `WebhookFailureAnalysisRequestDto`, builds a sanitized prompt through `IWebhookFailurePromptBuilder`, asks the configured local LLM for strict JSON, validates the result, applies safety rules, normalizes metadata, and returns a `WebhookFailureAnalysisResponseDto`.

The service is recommendation-only. It does **not** execute retries, pause endpoints, or move messages itself. Downstream systems can inspect the recommendation and decide how to apply production policy.

### Safety behavior

The service never trusts the model blindly:

- The LLM must return strict JSON that matches `WebhookFailureAnalysisResponseDto` fields used by the prompt.
- Required text fields must be present and non-empty.
- `riskLevel` must be a valid `AiRiskLevel` value.
- `suggestedRetryAction` must be a valid `SuggestedRetryAction` value.
- `confidenceScore` is normalized to the range `0..1`.
- `eventId` and `correlationId` are always copied from the original request, not from model output.
- `generatedAtUtc` is normalized to UTC.
- `model` and `provider` are populated from `AiOptions` when the model omits them.
- Full payloads and secrets are not logged.

Hard safety overrides are applied after successful AI parsing:

| Condition | Forced safe action | Reason |
| --- | --- | --- |
| `StatusCode == 429` and the model suggests `RetryImmediately` | `RetryWithBackoff` | Rate-limited endpoints must not be retried immediately. |
| `RetryCount >= MaxRetryCount` when `MaxRetryCount > 0` | `MoveToDeadLetter` | The retry budget has been exhausted. |
| `StatusCode == 401` or `StatusCode == 403` | `RequireManualReview` | Authentication and authorization failures need operator review or credential fixes. |

### Fallback decision table

Rule-based fallback is used when AI is disabled, the LLM call fails, the LLM returns invalid JSON, or the JSON is missing required/valid fields.

| Failure context | Fallback `suggestedRetryAction` | Retry recommended |
| --- | --- | --- |
| `RetryCount >= MaxRetryCount` and `MaxRetryCount > 0` | `MoveToDeadLetter` | No |
| HTTP `429` | `RetryWithBackoff` | Yes |
| HTTP `408` or `504` | `RetryWithBackoff` | Yes |
| HTTP `500`, `502`, or `503` | `RetryWithBackoff` | Yes |
| HTTP `401` or `403` | `RequireManualReview` | No |
| HTTP `400` or `404` | `RequireManualReview` | No |
| Unknown or missing status | `RequireManualReview` | No |

### Example input

```json
{
  "eventId": "evt_01HXZ9R8J6K8BNK7Y5J6W5N4M2",
  "correlationId": "corr_01HXZ9R8J6K8BNK7Y5J6W5N4M2",
  "subscriptionId": "sub_456",
  "eventType": "webhook.delivery.failed",
  "source": "hookbridge.worker",
  "targetUrl": "https://example.test/webhooks",
  "httpMethod": "POST",
  "statusCode": 500,
  "failureReason": "Target endpoint returned HTTP 500.",
  "retryCount": 2,
  "maxRetryCount": 5,
  "failedAtUtc": "2026-05-13T10:15:30Z"
}
```

### Example AI response

```json
{
  "eventId": "evt_01HXZ9R8J6K8BNK7Y5J6W5N4M2",
  "correlationId": "corr_01HXZ9R8J6K8BNK7Y5J6W5N4M2",
  "aiSummary": "The target endpoint returned a transient server error during webhook delivery.",
  "rootCause": "The downstream service responded with HTTP 500, which usually indicates a temporary server-side failure.",
  "aiRecommendation": "Retry with exponential backoff and monitor subsequent endpoint health.",
  "riskLevel": "Medium",
  "confidenceScore": 0.82,
  "suggestedRetryAction": "RetryWithBackoff",
  "isRetryRecommended": true,
  "generatedAtUtc": "2026-05-13T10:16:00Z"
}
```

### Example fallback response

```json
{
  "eventId": "evt_01HXZ9R8J6K8BNK7Y5J6W5N4M2",
  "correlationId": "corr_01HXZ9R8J6K8BNK7Y5J6W5N4M2",
  "aiSummary": "Rule-based analysis evaluated failed webhook delivery status code 429.",
  "rootCause": "The target endpoint reported rate limiting.",
  "aiRecommendation": "LLM analysis was unavailable; rule-based retry recommendation was used.",
  "riskLevel": "Medium",
  "confidenceScore": 0.65,
  "suggestedRetryAction": "RetryWithBackoff",
  "isRetryRecommended": true,
  "generatedAtUtc": "2026-05-13T10:16:00Z",
  "model": "llama3",
  "provider": "Ollama"
}
```

### Worker flow

The AI worker now consumes `AiAnalysisEventDto` messages from `hookbridge.ai.analysis`, maps the envelope and payload hints into `WebhookFailureAnalysisRequestDto`, calls `IAiRetryRecommendationService`, maps the returned `WebhookFailureAnalysisResponseDto` into `AiAnalysisResult`, and stores the document in the MongoDB `ai_analysis_results` collection. Unit tests mock the LLM client, prompt builder, Kafka consumer, and Mongo repository, so tests do not require real Ollama, Kafka, or MongoDB.

## AI log summarization service

`IAiLogSummarizationService` summarizes webhook-related logs into a short, actionable support explanation. It accepts an `AiLogSummaryRequestDto` containing `EventId`, `CorrelationId`, optional `Source`, optional `Environment`, optional `FromUtc`/`ToUtc`, and a collection of `AiLogEntryDto` records. Each log entry can include `TimestampUtc`, `Level`, `Message`, `Exception`, `ServiceName`, `TraceId`, and `SpanId`.

The service is designed for integration tests and local development: callers can mock `ILocalLlmClient` and `IAiLogSummaryPromptBuilder`, and unit tests do not require Ollama, Kafka, MongoDB, Elasticsearch, or Kibana. AI-generated output remains recommendation-only; operators should review delivery attempts, target endpoint health, and retry history before taking production action.

### Log summary configuration

| Key | Environment variable | Default | Description |
| --- | --- | --- | --- |
| `AI:MaxLogEntriesForSummary` | `AI__MaxLogEntriesForSummary` | `100` | Maximum number of log entries included in a summary prompt. Must be greater than `0`. |
| `AI:MaxLogMessageLength` | `AI__MaxLogMessageLength` | `2000` | Maximum characters retained for each log message or exception before truncation. Must be greater than `0`. |

### Example log summary request

```json
{
  "eventId": "evt_12345",
  "correlationId": "corr_789",
  "source": "hookbridge.worker",
  "environment": "qa",
  "fromUtc": "2026-05-13T10:00:00Z",
  "toUtc": "2026-05-13T10:15:00Z",
  "logs": [
    {
      "timestampUtc": "2026-05-13T10:10:00Z",
      "level": "Error",
      "message": "Webhook delivery failed with HTTP 429 Too Many Requests",
      "exception": null,
      "serviceName": "HookBridge.Worker",
      "traceId": "trace_123",
      "spanId": "span_456"
    }
  ]
}
```

### Example log summary response

```json
{
  "eventId": "evt_12345",
  "correlationId": "corr_789",
  "summary": "Webhook delivery failed because the target endpoint returned HTTP 429.",
  "rootCause": "The receiver is rate limiting requests.",
  "impact": "Webhook delivery may be delayed until retries succeed.",
  "recommendation": "Retry with exponential backoff and reduce delivery concurrency for this endpoint.",
  "riskLevel": "Medium",
  "confidenceScore": 0.85,
  "generatedAtUtc": "2026-05-13T10:15:00Z",
  "model": "llama3",
  "provider": "Ollama"
}
```

### Fallback behavior

The log summarization service returns a deterministic fallback response when AI is disabled, logs are empty, the LLM is unavailable, or the LLM returns invalid or unsafe JSON. Fallback logic counts error and warning log entries, selects the most recent error as the likely root cause when one exists, returns a conservative recommendation to review sanitized logs and delivery history, and uses a lower confidence score than an accepted AI-generated response.

### Safety rules for sensitive data

The log summary prompt builder masks sensitive values before prompt construction and truncates oversized messages. Sensitive keys include `Authorization`, `Cookie`, `Set-Cookie`, `Token`, `Secret`, `Password`, `Api-Key`, `X-API-Key`, and `ConnectionString`. Prompt logging should stay disabled in production unless explicitly approved for short-lived diagnostics.

## Endpoint health scoring service

`IEndpointHealthScoringService` provides deterministic endpoint reliability scoring for webhook target endpoints. It accepts an `EndpointHealthScoreRequestDto` with delivery counts, retry count, HTTP error categories, latency metrics, dead-letter counts, last failure context, and a UTC evaluation window. The implementation is pure and integration-test friendly: it uses only the in-memory DTO and a caller-supplied UTC `calculatedAtUtc` timestamp, and it does not call Kafka, MongoDB, Ollama, or external APIs.

### Score formula

The health score starts at `100` and subtracts deterministic penalties before clamping the final integer score to `0..100`:

- Failure rate: up to `50` points, proportional to `FailedDeliveries / TotalDeliveries`.
- Timeout failures: `2` points each, capped at `15`.
- HTTP `429` rate limit failures: `3` points each, capped at `15`.
- HTTP `5xx` server failures: `3` points each, capped at `20`.
- HTTP `4xx` client failures: `2` points each, capped at `15`.
- Retry count: `1` point each, capped at `10`.
- Average latency above `1000ms`: `(AverageLatencyMs - 1000) / 200`, capped at `10`.
- P95 latency above `2000ms`: `(P95LatencyMs - 2000) / 300`, capped at `15`.
- Dead-letter records: `10` points each, capped at `25`.
- Recent last failure: `10` points if within the last hour, `5` points if within the last 24 hours.

Requests are validated before scoring. Delivery counts and latency metrics must be non-negative; `SuccessfulDeliveries + FailedDeliveries` cannot exceed `TotalDeliveries` when `TotalDeliveries > 0`; `EvaluationWindowToUtc` must be greater than `EvaluationWindowFromUtc`; and all date values must be UTC. A request with `TotalDeliveries == 0` returns `Unknown` because there is insufficient delivery data.

### Health status thresholds

| Score | Health status | AI risk level |
| --- | --- | --- |
| Insufficient data | `Unknown` | `Unknown` |
| `90` to `100` | `Healthy` | `Low` |
| `70` to `89` | `Degraded` | `Medium` |
| `40` to `69` | `Unhealthy` | `High` |
| `0` to `39` | `Critical` | `Critical` |

### Recommendation rules

Recommendations are generated from the same deterministic signals used for scoring:

- `429` failures recommend exponential backoff and reduced concurrency.
- Timeout failures recommend increasing timeouts or checking receiver availability.
- `5xx` failures recommend retry with backoff and receiver health monitoring.
- `4xx` failures recommend manual review of endpoint configuration, authentication, and payload compatibility.
- High average or P95 latency recommends receiver performance investigation.
- Dead-letter records recommend manual review before replay.

### Example endpoint health score request

```json
{
  "endpointId": "endpoint_123",
  "subscriptionId": "sub_456",
  "customerId": "cust_789",
  "customerIdType": "internal",
  "targetUrl": "https://customer.example.com/webhook",
  "environment": "qa",
  "totalDeliveries": 100,
  "successfulDeliveries": 85,
  "failedDeliveries": 15,
  "timeoutCount": 1,
  "rateLimitCount": 0,
  "clientErrorCount": 0,
  "serverErrorCount": 2,
  "retryCount": 2,
  "deadLetterCount": 0,
  "averageLatencyMs": 1200,
  "p95LatencyMs": 2300,
  "lastFailureStatusCode": 503,
  "lastFailureReason": "Receiver returned Service Unavailable",
  "lastSuccessfulDeliveryAtUtc": "2026-05-13T10:10:00Z",
  "lastFailedDeliveryAtUtc": "2026-05-13T09:45:00Z",
  "evaluationWindowFromUtc": "2026-05-13T09:30:00Z",
  "evaluationWindowToUtc": "2026-05-13T10:30:00Z"
}
```

### Example endpoint health score response

```json
{
  "endpointId": "endpoint_123",
  "subscriptionId": "sub_456",
  "customerId": "cust_789",
  "targetUrl": "https://customer.example.com/webhook",
  "environment": "qa",
  "healthScore": 72,
  "healthStatus": "Degraded",
  "riskLevel": "Medium",
  "summary": "Endpoint is degraded due to recent delivery failures and increased latency.",
  "recommendation": "Monitor the endpoint and use retry with backoff. Review receiver performance if latency continues.",
  "calculatedAtUtc": "2026-05-13T10:30:00Z"
}
```

## Structured logging and observability

`HookBridge.AI.Worker` uses `Microsoft.Extensions.Logging` with structured placeholders so logs remain queryable in console, OpenTelemetry, Elasticsearch, Application Insights, or any other configured provider. The worker does not write directly to `Console`; tests and integrations can replace the logger through the normal `ILogger<T>` abstractions.

### Logged lifecycle and processing events

The AI worker emits structured logs for:

- Worker startup and shutdown.
- AI enabled/disabled status, provider, and model.
- Kafka consumer startup and message receipt from `hookbridge.ai.analysis`.
- Kafka message processing start, completion, and failure.
- Invalid Kafka messages skipped without logging payload content.
- AI prompt generation start/completion with prompt duration, but not the prompt body.
- LLM request start/completion and expected provider failures with request duration.
- Deterministic fallback usage, including fallback reason.
- Mongo insert start/completion/failure with persistence duration.
- Cancellation requests during graceful shutdown.

### Important log fields

Common structured fields include:

| Field | Description |
| --- | --- |
| `EventId` | HookBridge event identifier when available. |
| `CorrelationId` | Cross-service correlation identifier when available. |
| `EventType` | Event name, such as `WebhookDeliveryFailed`. |
| `Source` | Producer/source system for the analysis event. |
| `Provider` | AI provider, such as `Ollama`. |
| `Model` | Configured model, such as `llama3`. |
| `KafkaTopic` | Kafka topic being consumed. |
| `ConsumerGroupId` | AI worker Kafka consumer group. |
| `DurationMs` | Elapsed duration in milliseconds for processing, LLM, prompt, or Mongo operations. |
| `FallbackUsed` | Whether deterministic fallback produced the recommendation. |
| `FallbackReason` | Reason fallback was used, such as `ProviderUnavailable` or `AiDisabled`. |
| `RiskLevel` | AI/fallback risk level for the failed delivery. |
| `SuggestedRetryAction` | Recommended next retry action. |
| `Operation` | Operation name on exception logs, such as `MessageProcessing` or `MongoInsert`. |

Message processing also opens a logging scope containing `EventId` and `CorrelationId`, allowing providers that support scopes to attach those fields to all nested logs for the event.

### Example logs

```text
HookBridge AI Worker starting. Enabled: true, Provider: Ollama, Model: llama3, KafkaTopic: hookbridge.ai.analysis, ConsumerGroupId: hookbridge-ai-worker
AI analysis processing started. EventId: evt_12345, CorrelationId: corr_789, EventType: WebhookDeliveryFailed, Source: hookbridge-worker, Provider: Ollama, Model: llama3, KafkaTopic: hookbridge.ai.analysis, ConsumerGroupId: hookbridge-ai-worker
AI prompt generation completed. EventId: evt_12345, CorrelationId: corr_789, EventType: WebhookDeliveryFailed, Source: hookbridge-worker, Provider: Ollama, Model: llama3, DurationMs: 2
LLM request completed. Provider: Ollama, Model: llama3, Attempt: 1, Attempts: 4, DurationMs: 842
Mongo insert completed. EventId: evt_12345, CorrelationId: corr_789, EventType: WebhookDeliveryFailed, Source: hookbridge-worker, Provider: Ollama, Model: llama3, DurationMs: 14
AI analysis processing completed. EventId: evt_12345, CorrelationId: corr_789, EventType: WebhookDeliveryFailed, Source: hookbridge-worker, Provider: Ollama, Model: llama3, KafkaTopic: hookbridge.ai.analysis, ConsumerGroupId: hookbridge-ai-worker, FallbackUsed: false, FallbackReason: None, RiskLevel: Medium, SuggestedRetryAction: RetryWithBackoff, DurationMs: 860
```

Fallback usage is logged at `Warning` level:

```text
AI fallback used. EventId: evt_12345, CorrelationId: corr_789, EventType: WebhookDeliveryFailed, Source: hookbridge-worker, Provider: Ollama, Model: llama3, FallbackUsed: true, FallbackReason: ProviderUnavailable, RiskLevel: Medium, SuggestedRetryAction: RetryWithBackoff
```

Processing or persistence failures are logged at `Error` level and include the exception object plus `Operation`, `EventId`, and `CorrelationId` when available.

### Sensitive-data logging rules

AI worker logs intentionally use metadata only. Do not log:

- Full webhook request payloads.
- Full response bodies.
- Authorization headers.
- Cookies or `Set-Cookie` values.
- API keys, tokens, secrets, passwords, or connection strings.
- Full prompts or raw LLM responses.

`SensitiveLogSanitizer` masks values whose field/header names indicate sensitive data, and prompt builders also mask sensitive headers and truncate payload-like fields before building prompts. Keep `AI:EnablePromptLogging` disabled in production unless an approved, short-lived incident response process requires it.

## AI Worker tests and coverage

The AI service layer is covered by `HookBridge.AI.Worker.Tests`, and CI also runs `HookBridge.AI.Worker.IntegrationTests` to validate the AI Worker dependency registration path used by the processing pipeline. The unit tests use xUnit, Moq, FluentAssertions, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Logging.Abstractions`, and Coverlet. They mock local LLM, Kafka, and MongoDB dependencies, so they do not require a running Ollama instance, Kafka broker, or MongoDB server.

Run the AI Worker unit tests from the repository root:

```bash
dotnet test tests/HookBridge.AI.Worker.Tests/HookBridge.AI.Worker.Tests.csproj
```

Run the AI Worker integration tests from the repository root:

```bash
dotnet test tests/HookBridge.AI.Worker.IntegrationTests/HookBridge.AI.Worker.IntegrationTests.csproj
```

Generate Coverlet XPlat coverage files for the same AI Worker unit and integration test projects used by CI:

```bash
dotnet build HookBridge.sln --configuration Release

dotnet test tests/HookBridge.AI.Worker.Tests/HookBridge.AI.Worker.Tests.csproj \
  --configuration Release \
  --no-build \
  --collect:"XPlat Code Coverage" \
  --settings coverlet.runsettings \
  --results-directory ./TestResults/Unit

dotnet test tests/HookBridge.AI.Worker.IntegrationTests/HookBridge.AI.Worker.IntegrationTests.csproj \
  --configuration Release \
  --no-build \
  --collect:"XPlat Code Coverage" \
  --settings coverlet.runsettings \
  --results-directory ./TestResults/Integration
```

Install ReportGenerator and create the readable local coverage report:

```bash
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator \
  -reports:"TestResults/**/coverage.cobertura.xml" \
  -targetdir:"CoverageReport" \
  -reporttypes:"Html;Cobertura;MarkdownSummaryGithub;TextSummary"
```

Open `CoverageReport/index.html` for the HTML report, use `CoverageReport/Cobertura.xml` for coverage tooling, or read `CoverageReport/SummaryGithub.md` for the Markdown summary. The GitHub Actions workflow uploads the same directory as the `hookbridge-coverage-report` artifact and appends the Markdown/text summary to the workflow job summary.

Coverage must stay at or above 80% line coverage and 70% branch coverage. The shared `coverlet.runsettings` file excludes only safe generated/build artifacts (`bin/**`, `obj/**`, `Migrations/**`, `Generated/**`, `*.g.cs`) and `Program.cs`; it does not exclude core AI Worker service, prompt, Kafka, Mongo repository, or LLM client code. The integration test stage runs in GitHub Actions by default. If maintainers need an emergency skip, set the GitHub Actions variable `SKIP_INTEGRATION_TESTS=true`; do not use that variable for normal pull request validation.

## Payload Schema Detection Agent

HookBridge AI Worker includes a Payload Schema Detection Agent for inspecting incoming webhook JSON payloads before operators or downstream services formalize DTO contracts. The agent combines local LLM analysis with deterministic fallback rules so unit and integration tests can mock the LLM client and never require a real Ollama, Kafka, or MongoDB dependency.

### Purpose

The agent analyzes a webhook payload and returns:

- the likely schema name and event type;
- a short human-readable summary;
- important fields with JSON paths, inferred types, required hints, sample values, and descriptions;
- missing fields and validation issues;
- a suggested DTO name;
- confidence, risk, model, provider, and fallback metadata.

Schema detection requests are consumed from Kafka topic `hookbridge.ai.schema-detection` and results are persisted to MongoDB collection `payload_schema_detection_results`.

### Example request

```json
{
  "eventId": "evt_1001",
  "correlationId": "corr_2001",
  "source": "HookBridge.API",
  "eventType": "OrderCreated",
  "customerId": "cust_123",
  "payload": {
    "orderId": "ORD-1001",
    "status": "Created",
    "customer": {
      "id": "C001",
      "name": "Test Customer"
    },
    "items": [
      {
        "sku": "SKU-001",
        "quantity": 2
      }
    ]
  },
  "headers": {
    "X-API-Key": "[MASKED]"
  },
  "receivedAtUtc": "2026-05-14T10:30:00Z"
}
```

### Example response

```json
{
  "eventId": "evt_1001",
  "correlationId": "corr_2001",
  "detectedSchemaName": "OrderCreated",
  "detectedEventType": "OrderCreated",
  "summary": "Payload appears to represent an order creation event with customer and item details.",
  "importantFields": [
    {
      "fieldName": "orderId",
      "jsonPath": "$.orderId",
      "inferredType": "string",
      "isRequired": true,
      "sampleValue": "ORD-1001",
      "description": "Unique order identifier."
    }
  ],
  "missingFields": [],
  "validationIssues": [],
  "suggestedDtoName": "OrderCreatedDto",
  "confidenceScore": 0.86,
  "riskLevel": "Low",
  "generatedAtUtc": "2026-05-14T10:31:00Z",
  "model": "llama3",
  "provider": "Ollama",
  "fallback": {
    "usedFallback": false,
    "fallbackReason": "None",
    "fallbackMessage": "",
    "provider": "Ollama",
    "model": "llama3",
    "generatedAtUtc": "2026-05-14T10:31:00Z"
  }
}
```

### Fallback behavior

The agent falls back to safe rule-based detection when AI is disabled, the LLM is unavailable, the payload is invalid JSON, or the LLM returns invalid JSON. Fallback uses `System.Text.Json` to detect root object/array payloads, infer visible field names and basic types, generate a DTO name from `eventType` when available, lower the confidence score, and mark risk as `Unknown` or `Medium` depending on payload quality.

### Security and masking rules

Full payloads and secrets must not be logged. Prompt construction masks sensitive header and payload values before sending data to the LLM and truncates large payloads with an explicit truncation marker. The following names are treated as sensitive case-insensitively: `Authorization`, `Cookie`, `Set-Cookie`, `Token`, `Secret`, `Password`, `Api-Key`, `X-API-Key`, `ClientSecret`, and `AccessToken`.

## FluentValidation Rule Generation Agent

The FluentValidation Rule Generation Agent analyzes a webhook payload, detected schema hints, and generated DTO source code, then suggests a .NET 8-compatible `FluentValidation` validator for the DTO. It is designed for integration-test-friendly background processing: unit tests can mock `ILocalLlmClient`, and deterministic fallback does not require Ollama, Kafka, or MongoDB.

The worker consumes validation rule generation messages from Kafka topic `hookbridge.ai.validation-rule-generation` when `AiKafka:ValidationRuleGenerationTopic` is configured. Results are persisted to MongoDB collection `fluent_validation_rule_generation_results` through `FluentValidationRuleGenerationResult`. Runtime logs include structured metadata such as event id, correlation id, validator class name, rule count, confidence, risk, and fallback status; logs intentionally exclude full payloads and generated validator code.

### Example request

```json
{
  "eventId": "evt_1001",
  "correlationId": "corr_2001",
  "eventType": "OrderCreated",
  "source": "orders",
  "customerId": "customer_123",
  "rootClassName": "OrderCreatedDto",
  "namespace": "HookBridge.Contracts.Events",
  "payload": {
    "orderId": "ORD-1001",
    "status": "Created",
    "totalAmount": 129.5,
    "customerEmail": "customer@example.com",
    "callbackUrl": "https://customer.example.com/webhook",
    "items": [
      {
        "sku": "SKU-001",
        "quantity": 2
      }
    ]
  },
  "generatedDtoCode": "public sealed class OrderCreatedDto { public string? OrderId { get; set; } }",
  "detectedFields": [
    {
      "fieldName": "orderId",
      "jsonPath": "$.orderId",
      "inferredType": "string",
      "isRequired": true,
      "sampleValue": "ORD-1001",
      "description": "Order identifier."
    }
  ],
  "requiredFields": ["orderId", "status", "items"],
  "receivedAtUtc": "2026-05-14T10:30:00Z"
}
```

### Example response

```json
{
  "eventId": "evt_1001",
  "correlationId": "corr_2001",
  "validatorClassName": "OrderCreatedDtoValidator",
  "namespace": "HookBridge.Contracts.Events",
  "generatedValidatorCode": "using FluentValidation;\n\nnamespace HookBridge.Contracts.Events;\n\npublic sealed class OrderCreatedDtoValidator : AbstractValidator<OrderCreatedDto>\n{\n    public OrderCreatedDtoValidator()\n    {\n        RuleFor(x => x.OrderId)\n            .NotEmpty()\n            .WithMessage(\"OrderId is required.\");\n    }\n}",
  "rules": [
    {
      "propertyName": "OrderId",
      "ruleType": "NotEmpty",
      "ruleExpression": ".NotEmpty()",
      "errorMessage": "OrderId is required.",
      "severity": "Error",
      "description": "Required or identifier string should not be empty."
    }
  ],
  "summary": "Rule-based fallback generated FluentValidation rules.",
  "validationNotes": [],
  "confidenceScore": 0.45,
  "riskLevel": "Low",
  "generatedAtUtc": "2026-05-14T10:31:00Z",
  "model": "llama3",
  "provider": "Ollama",
  "fallback": {
    "usedFallback": true,
    "fallbackReason": "AiDisabled",
    "fallbackMessage": "AI is disabled; deterministic FluentValidation rules were generated.",
    "provider": "Ollama",
    "model": "llama3",
    "generatedAtUtc": "2026-05-14T10:31:00Z"
  }
}
```

### Example generated validator code

```csharp
using FluentValidation;

namespace HookBridge.Contracts.Events;

public sealed class OrderCreatedDtoValidator : AbstractValidator<OrderCreatedDto>
{
    public OrderCreatedDtoValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty()
            .WithMessage("OrderId is required.");

        RuleFor(x => x.Status)
            .NotEmpty()
            .WithMessage("Status is required.");

        RuleFor(x => x.TotalAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("TotalAmount must be greater than or equal to 0.");

        RuleFor(x => x.CustomerEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.CustomerEmail))
            .WithMessage("CustomerEmail must be a valid email address.");

        RuleFor(x => x.CallbackUrl)
            .Must(value => Uri.TryCreate(value, UriKind.Absolute, out _))
            .When(x => !string.IsNullOrWhiteSpace(x.CallbackUrl))
            .WithMessage("CallbackUrl must be a valid absolute URL.");

        RuleFor(x => x.Items)
            .NotNull()
            .WithMessage("Items cannot be null.");
    }
}
```

### Fallback behavior

The agent uses deterministic fallback when AI is disabled, the LLM call fails, the payload is invalid JSON, the LLM returns invalid JSON, or `GeneratedDtoCode` is missing. Fallback parses the visible payload with `System.Text.Json` and infers conservative rules:

- string identifiers and required fields: `NotEmpty()`;
- email-like fields: `EmailAddress()`;
- URL/URI-like fields: absolute URI validation with `Uri.TryCreate`;
- quantity/count/amount/price/total numeric fields: `GreaterThanOrEqualTo(0)`;
- date/time fields: UTC-oriented validation notes/rules where possible;
- arrays: `NotNull()` and `NotEmpty()` when marked required.

Fallback responses use a lower confidence score and include `fallback` metadata so dashboards and alerts can distinguish AI-generated suggestions from rule-based suggestions.

### Security and masking rules

The prompt builder masks sensitive values before prompt creation and safely truncates large payloads and generated DTO code. Masked keys include `Authorization`, `Cookie`, `Set-Cookie`, `Token`, `Secret`, `Password`, `Api-Key`, `X-API-Key`, `ClientSecret`, `AccessToken`, and `ConnectionString`. Generated validator code must never include secret sample values, and worker logs must not include full payloads or generated code.
