# HookBridge AI Worker

`HookBridge.AI.Worker` is a .NET 8 Worker Service reserved for future Agentic AI background processing in HookBridge. The initial service starts cleanly, binds AI configuration through `IOptions<AiOptions>`, logs its lifecycle, exposes an in-process health status class, and remains idle until the host is cancelled.

## Purpose

The AI worker gives HookBridge a separate runtime for AI-oriented background jobs without coupling those jobs to the API gateway or existing webhook delivery workers. Future capabilities can include event enrichment, summarization, intelligent routing, anomaly classification, and operational assistant workflows.

## Local configuration

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

The default provider is Ollama. For local development, run Ollama so the configured endpoint is reachable at `http://localhost:11434`, or override the endpoint with the standard .NET configuration key:

```bash
AI__Endpoint=http://localhost:11434
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

The initial worker logs a startup message, waits for host cancellation, and logs a shutdown message during graceful termination.

## Health status

Because this project is a worker process rather than an ASP.NET Core web app, it exposes `AiWorkerHealthStatus` as an in-process health status class. The status reports whether AI processing is enabled and whether the provider, model, and endpoint configuration are usable.
