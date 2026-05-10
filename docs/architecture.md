# HookBridge Architecture

HookBridge is a self-hosted webhook infrastructure and event-processing platform built around API-based ingestion, Kafka-backed event distribution, worker-driven delivery, persistent audit records, retry/DLQ handling, and operational observability.

```mermaid
flowchart LR
  ClientApps["Client Applications"] -->|POST events / CloudEvents| API["HookBridge API<br/>ASP.NET Core"]
  API -->|Validate tenant, auth, schema, rate limits| Kafka[(Kafka<br/>Event Topic)]
  Kafka -->|Consume events| Workers["Workers<br/>Delivery & Retry Consumers"]
  Workers -->|HTTP delivery| WebhookEndpoints["External Webhook Endpoints"]

  Workers -->|Transient failure| RetryQueue[["Retry Queue<br/>Backoff schedule"]]
  RetryQueue -->|Requeue for retry| Kafka
  Workers -->|Max attempts exceeded| DLQ[["DLQ<br/>Dead Letter Queue"]]
  DLQ -->|Inspect / replay| Workers

  API -->|Persist tenants, events, audit records| MongoDB[(MongoDB)]
  Workers -->|Record attempts, outcomes, failures| MongoDB
  DLQ -->|Failed event records| MongoDB

  API -.->|Logs, metrics, traces, health| Monitoring["Monitoring Stack<br/>Observability & alerting"]
  Kafka -.->|Broker metrics / lag| Monitoring
  Workers -.->|Delivery metrics / errors| Monitoring
  MongoDB -.->|Storage health / query telemetry| Monitoring

  classDef client fill:#1e3a8a,stroke:#60a5fa,color:#eff6ff,stroke-width:2px;
  classDef api fill:#0e7490,stroke:#22d3ee,color:#ecfeff,stroke-width:2px;
  classDef stream fill:#4c1d95,stroke:#a78bfa,color:#f5f3ff,stroke-width:2px;
  classDef worker fill:#065f46,stroke:#34d399,color:#ecfdf5,stroke-width:2px;
  classDef retry fill:#78350f,stroke:#fbbf24,color:#fffbeb,stroke-width:2px;
  classDef dlq fill:#7f1d1d,stroke:#f87171,color:#fef2f2,stroke-width:2px;
  classDef data fill:#064e3b,stroke:#10b981,color:#ecfdf5,stroke-width:2px;
  classDef observe fill:#312e81,stroke:#818cf8,color:#eef2ff,stroke-width:2px;

  class ClientApps,WebhookEndpoints client;
  class API api;
  class Kafka stream;
  class Workers worker;
  class RetryQueue retry;
  class DLQ dlq;
  class MongoDB data;
  class Monitoring observe;
```

## Flow Summary

1. Client applications submit webhook events to the HookBridge API.
2. The API validates tenant access, authentication, payload shape, rate limits, and endpoint configuration.
3. Accepted events are persisted and published to Kafka.
4. Worker consumers process Kafka events and deliver webhook requests to external endpoints.
5. Transient delivery failures move through the retry queue with backoff before being reprocessed.
6. Exhausted events are placed in the DLQ for inspection, audit, and replay workflows.
7. MongoDB stores tenants, events, delivery attempts, audit records, and failed-event state.
8. The monitoring stack observes API health, Kafka lag, worker errors, delivery metrics, and storage health.
