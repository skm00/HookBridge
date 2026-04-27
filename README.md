# HookBridge

Initial production-style SaaS solution scaffold for a multi-tenant webhook delivery platform.

## Stack
- .NET 8
- Clean Architecture
- MongoDB
- Kafka (planned)
- React dashboard (planned)

## Local MongoDB Setup

### Option 1: Docker
```bash
docker run --name hookbridge-mongodb -p 27017:27017 -d mongo:7
```

### Option 2: Docker Compose (from repo root)
```bash
docker compose -f deploy/docker-compose.yml up -d
```

### Connection Settings
Both API and Worker development settings use:
- Connection string: `mongodb://localhost:27017`
- Database: `hookbridge`

## Local Kafka Configuration

Both API and Worker development settings include a `Kafka` section:
- Bootstrap servers: `localhost:9092`
- Consumer group id: `hookbridge-worker`
- Auto commit: `false`
- Message timeout: `10000`

Kafka topics currently reserved by HookBridge:
- `webhook-events`
- `webhook-retry`
- `webhook-dlq`

## Admin Authentication (JWT)

Admin/dashboard APIs now require JWT Bearer authentication.
Admin JWT tokens include role-based authorization via a `role` claim.

Configure `Jwt` in `src/HookBridge.Api/appsettings.Development.json`:
- `Issuer`
- `Audience`
- `Secret`
- `ExpiryMinutes`

### Admin roles

| Role | Permissions |
|---|---|
| Owner | Full access, including billing and API key revoke actions. |
| Admin | Manage tenants, subscriptions, and API keys (except owner-only actions). |
| Developer | Read logs and debug integrations (delivery logs, failed events, usage, and read APIs). |
| Viewer | Read-only access. |

### Register admin
```bash
curl -X POST http://localhost:5000/api/v1/auth/register   -H "Content-Type: application/json"   -d '{
    "tenantId": "<tenantId>",
    "email": "admin@acme.com",
    "password": "Password123!",
    "fullName": "Acme Admin",
    "role": 3
  }'
```

> Note: role assignment during registration is currently enabled for development/demo.

### Login admin
```bash
curl -X POST http://localhost:5000/api/v1/auth/login   -H "Content-Type: application/json"   -d '{
    "email": "admin@acme.com",
    "password": "Password123!"
  }'
```

### Call protected admin endpoint with Bearer token
```bash
curl http://localhost:5000/api/v1/admin/tenants   -H "Authorization: Bearer <jwt-token>"
```

Event ingestion remains API-key based (`x-api-key`) and is intentionally not JWT-based.

## Admin API Tenant Security

- Admin JWTs include a `tenantId` claim and admin endpoints resolve the current tenant from the JWT on every request.
- Admin APIs are tenant-scoped: tenant route/query inputs are validated or overridden to the authenticated admin tenant.
- Cross-tenant admin access is blocked by design and returns `403 Forbidden`.
- Missing/invalid tenant claims for authenticated admin requests return `401 Unauthorized`.
- Event ingestion remains API-key based and keeps using `x-api-key` + `/api/v1/events/{tenantId}` routing.

## Tenant Management API (Admin)

### Create tenant
```bash
curl -X POST http://localhost:5000/api/v1/admin/tenants \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Acme Inc",
    "slug": "acme-inc",
    "contactEmail": "ops@acme.com"
  }'
```

### Get all tenants
```bash
curl http://localhost:5000/api/v1/admin/tenants
```

### Get tenant by id
```bash
curl http://localhost:5000/api/v1/admin/tenants/{tenantId}
```

### Update tenant
```bash
curl -X PUT http://localhost:5000/api/v1/admin/tenants/{tenantId} \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Acme Corporation",
    "contactEmail": "platform@acme.com"
  }'
```

### Disable tenant
```bash
curl -X DELETE http://localhost:5000/api/v1/admin/tenants/{tenantId}
```

## API Key Management API (Admin)

### Create API key
```bash
curl -X POST http://localhost:5000/api/v1/admin/tenants/{tenantId}/api-keys \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Ingestion Key"
  }'
```

### List API keys
```bash
curl http://localhost:5000/api/v1/admin/tenants/{tenantId}/api-keys
```

### Revoke API key
```bash
curl -X DELETE http://localhost:5000/api/v1/admin/tenants/{tenantId}/api-keys/{keyId}
```

## Event Ingestion API

### Ingest event
```bash
curl -X POST http://localhost:5000/api/v1/events/{tenantId} \
  -H "Content-Type: application/json" \
  -H "x-api-key: <plain-api-key>" \
  -H "x-correlation-id: test-correlation-001" \
  -d '{
    "eventType": "order.created",
    "eventId": "evt_123",
    "timestamp": "2026-04-27T10:00:00Z",
    "data": {
      "orderId": "1001",
      "amount": 250
    }
  }'
```

## Event Ingestion Flow

When a customer calls the ingestion endpoint, HookBridge now follows this sequence:
1. Validate tenant API key and event payload.
2. If the event is not a duplicate, evaluate the tenant monthly usage limit.
3. Store the incoming event in MongoDB first.
4. Increment tenant monthly `EventsReceived` usage counter.
5. Publish a `WebhookEventMessage` to Kafka topic `webhook-events` using the tenant id as key.

If Kafka publish fails after the event is stored, the API still returns `202 Accepted` with message `Event accepted but publishing is delayed.` so customer ingestion is not rejected.

If usage exceeds the tenant plan limit, ingestion returns `429 Too Many Requests` with message:
`Monthly event limit exceeded for the current billing plan.`

## Usage Tracking and Plan Limits

HookBridge tracks monthly usage counters in UTC per tenant:
- `EventsReceived`
- `EventsDelivered`
- `EventsFailed`

Plan defaults:
- **Free**: 1,000 events/month
- **Starter**: 50,000 events/month
- **Pro**: 500,000 events/month
- **Enterprise**: unlimited

Current usage endpoint:
```bash
curl http://localhost:5000/api/v1/admin/tenants/{tenantId}/usage/current
```

## Stripe Billing Setup (Foundation)

Add Stripe settings under `Stripe` in `src/HookBridge.Api/appsettings.Development.json`:
- `SecretKey`
- `WebhookSecret`
- `StarterPriceId`
- `ProPriceId`
- `EnterprisePriceId`
- `SuccessUrl`
- `CancelUrl`

Billing plan limits used by billing webhook updates:
- **Free**: 1,000 events/month
- **Starter**: 50,000 events/month
- **Pro**: 500,000 events/month
- **Enterprise**: 2,147,483,647 events/month (`int.MaxValue`)

Configure Stripe prices:
1. Create recurring Stripe prices in your Stripe dashboard for Starter/Pro/Enterprise.
2. Copy each price id (for example `price_...`) into the matching HookBridge setting.
3. Keep secrets in configuration providers (environment variables, secrets manager, etc.), never in source code.

Test Stripe webhooks locally:
```bash
stripe listen --forward-to localhost:5000/api/v1/billing/stripe/webhook
```

Use the returned webhook signing secret as `Stripe:WebhookSecret`, then trigger a sample checkout/subscription event from Stripe CLI.

## Worker Consumption Flow

`HookBridge.Worker` runs `WebhookEventConsumerWorker`, which:
- Subscribes to Kafka topic `webhook-events` with `Kafka:ConsumerGroupId`.
- Deserializes each message into `WebhookEventMessage`.
- Calls the application delivery flow for first-attempt delivery.
- Continues processing later events if one event fails.

Delivery flow:
1. Event ingestion stores incoming events.
2. Kafka carries `WebhookEventMessage` on `webhook-events`.
3. Worker loads matching active subscriptions by tenant + event type.
4. Worker sends webhook POST requests to each subscription target URL.
5. Worker stores a `DeliveryAttempt` for every first-attempt delivery result.
6. If a first attempt fails and retry attempts remain in the subscription retry policy, worker publishes a `WebhookRetryMessage` to `webhook-retry` with the scheduled `nextRetryAt` time.
7. `WebhookRetryConsumerWorker` consumes `webhook-retry` with consumer group `hookbridge-worker-retry`.
8. Retry worker waits until `nextRetryAt` when needed, then retries delivery for the specific subscription.
9. Worker stores retry `DeliveryAttempt` records and, when retry policy still allows, reschedules by publishing a new `WebhookRetryMessage` to `webhook-retry`.
10. Worker updates `IncomingEvent` status for first-attempt processing.
11. When retry attempts are exhausted, worker writes a record to `failed_events` (Mongo collection `FailedEvent`) and publishes `WebhookDlqMessage` to `webhook-dlq`.

DLQ flow is now: **retry exhausted → `failed_events` collection → `webhook-dlq` topic**.

## Failed Events API (Admin)

### Manually retry a failed event
```bash
curl -X POST http://localhost:5000/api/v1/admin/failed-events/{id}/retry
```

## Outbound Webhook Delivery Request Format

The reusable outbound delivery client sends a POST request per delivery attempt with:

- URL: subscription `targetUrl` (absolute URL required).
- Method: `POST`.
- Body: JSON-serialized event payload.
- Content-Type: `application/json`.
- Timeout: uses subscription/request `timeoutSeconds`.

Default HookBridge headers added on every request:
- `x-hookbridge-event-id`
- `x-hookbridge-event-type`
- `x-hookbridge-tenant-id`
- `x-correlation-id` (only when available)

Custom subscription headers are also added, except `Content-Type` which is intentionally ignored so JSON content type remains authoritative.

Supported outbound authentication modes on subscriptions:
- `None`
- `Basic`
- `ApiKeyHeader`
- `HmacSignature` (`HMACSHA256`, header format `sha256=<signature>`, default header `x-hookbridge-signature`)
- `OAuth2ClientCredentials` (client credentials flow with in-memory token caching)

Delivery result model captured by the client:
- `isSuccess` (true only for HTTP 2xx)
- `httpStatusCode`
- `responseBody`
- `errorMessage`
- `durationMs`

## Running API and Worker Locally

From the repository root:

1. Start infrastructure dependencies (MongoDB + Kafka):
```bash
docker compose -f deploy/docker-compose.yml up -d
```

2. Run API:
```bash
dotnet run --project src/HookBridge.Api
```

3. Run Worker in another terminal:
```bash
dotnet run --project src/HookBridge.Worker
```

4. Publish an event through the API ingestion endpoint; the worker will consume and log the event from `webhook-events`.


## Subscription Management API (Admin)

### Create subscription
```bash
curl -X POST http://localhost:5000/api/v1/admin/subscriptions \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "tenant-id",
    "eventType": "order.created",
    "targetUrl": "https://example.com/webhooks/orders",
    "headers": [
      { "name": "x-callback-source", "value": "hookbridge" }
    ],
    "authentication": {
      "type": "Basic",
      "basic": {
        "username": "hook-user",
        "password": "super-secret"
      }
    },
    "retryPolicy": {
      "maxAttempts": 3,
      "initialDelaySeconds": 30,
      "backoffType": "Exponential"
    },
    "timeoutSeconds": 30
  }'
```

### Outbound auth examples

#### Basic auth
```json
{
  "authentication": {
    "type": "Basic",
    "basic": {
      "username": "hook-user",
      "password": "super-secret"
    }
  }
}
```

#### API key header
```json
{
  "authentication": {
    "type": "ApiKeyHeader",
    "apiKeyHeader": {
      "headerName": "x-api-key",
      "headerValue": "subscription-secret-key"
    }
  }
}
```

#### HMAC signature
```json
{
  "authentication": {
    "type": "HmacSignature",
    "hmacSignature": {
      "secret": "hmac-shared-secret",
      "headerName": "x-hookbridge-signature",
      "algorithm": "HMACSHA256"
    }
  }
}
```

#### OAuth2 client credentials
```json
{
  "authentication": {
    "type": "OAuth2ClientCredentials",
    "oauth2": {
      "tokenUrl": "https://auth.example.com/oauth2/token",
      "clientId": "webhook-client-id",
      "clientSecret": "webhook-client-secret",
      "scope": "webhook.deliver"
    }
  }
}
```

### Get subscription by id
```bash
curl http://localhost:5000/api/v1/admin/subscriptions/{subscriptionId}
```

### Search subscriptions
```bash
curl "http://localhost:5000/api/v1/admin/subscriptions?tenantId={tenantId}&eventType=order.created&targetUrl=example.com&isActive=true"
```

### Update subscription
```bash
curl -X PUT http://localhost:5000/api/v1/admin/subscriptions/{subscriptionId} \
  -H "Content-Type: application/json" \
  -d '{
    "targetUrl": "https://example.com/webhooks/orders-v2",
    "timeoutSeconds": 45,
    "retryPolicy": {
      "maxAttempts": 5,
      "initialDelaySeconds": 15,
      "backoffType": "Fixed"
    }
  }'
```

### Disable subscription
```bash
curl -X POST http://localhost:5000/api/v1/admin/subscriptions/{subscriptionId}/disable
```

### Enable subscription
```bash
curl -X POST http://localhost:5000/api/v1/admin/subscriptions/{subscriptionId}/enable
```

### Delete subscription
```bash
curl -X DELETE http://localhost:5000/api/v1/admin/subscriptions/{subscriptionId}
```


## Delivery Logs API (Admin)

### Search delivery logs
```bash
curl "http://localhost:5000/api/v1/admin/delivery-logs?tenantId={tenantId}&eventId={eventId}&subscriptionId={subscriptionId}&eventType=order.created&status=Failed&httpStatusCode=500&fromDate=2026-04-27T00:00:00Z&toDate=2026-04-27T23:59:59Z&targetUrl=example.com"
```

### Get delivery log by id
```bash
curl http://localhost:5000/api/v1/admin/delivery-logs/{deliveryAttemptId}
```
