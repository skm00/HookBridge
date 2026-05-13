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
    "HealthCheckPrompt": "Say HookBridge AI is ready"
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
| `AI:HealthCheckPrompt` | `AI__HealthCheckPrompt` | `Say HookBridge AI is ready` | No | Lightweight prompt reserved for future provider health checks. |

Options are validated during startup with the .NET options pattern using data annotations, custom conditional validation, and `ValidateOnStart()`. When AI is enabled, `Provider`, `Model`, and `Endpoint` must be present. `TimeoutSeconds` must be greater than `0`, and `MaxRetries` must be `0` or greater. Invalid configuration fails startup clearly before the worker begins processing.

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
    "HealthCheckPrompt": "Say HookBridge AI is ready"
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
dotnet run --project src/HookBridge.AI.Worker/HookBridge.AI.Worker.csproj
```

To disable AI safely while keeping the worker process available:

```bash
AI__Enabled=false \
dotnet run --project src/HookBridge.AI.Worker/HookBridge.AI.Worker.csproj
```

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
