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

The worker uses the `AI` configuration section:

```json
{
  "AI": {
    "Enabled": true,
    "Provider": "Ollama",
    "Model": "llama3",
    "Endpoint": "http://localhost:11434"
  }
}
```

Configuration keys:

| Key | Required when enabled | Description |
| --- | --- | --- |
| `AI:Enabled` | Yes | Enables or disables Semantic Kernel initialization. Set to `false` to run the worker without AI services. |
| `AI:Provider` | Yes | AI provider name. Currently supported value: `Ollama`. |
| `AI:Model` | Yes | Ollama model name to use for chat completion. |
| `AI:Endpoint` | Yes | Absolute HTTP or HTTPS URL for the Ollama API endpoint. |

Missing or invalid endpoint/model configuration is logged and surfaced as an `InvalidOperationException` during kernel creation.

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
