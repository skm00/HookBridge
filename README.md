# HookBridge

Initial production-style SaaS solution scaffold for a multi-tenant webhook delivery platform.

## Stack
- .NET 8
- Clean Architecture
- MongoDB
- Kafka (planned)
- React dashboard (planned)

## API Versioning

HookBridge APIs are versioned using URL segments for long-term compatibility.

- Current stable version: `v1`
- Route format: `/api/v{version}/...` (for example, `/api/v1/events/{tenantId}` and `/api/v1/admin/subscriptions`)
- Future versions will follow the same pattern, such as `/api/v2/...`

## Configuration Validation

HookBridge validates critical configuration sections at startup and fails fast with actionable errors (for example, `Jwt:Secret must be at least 32 characters long.`).

### Required production settings

In `Production`, the following settings are required:

- `MongoDb:ConnectionString`
- `MongoDb:DatabaseName`
- `Kafka:BootstrapServers`
- `Kafka:MessageTimeoutMs` (> 0)
- `Kafka:ConsumerGroupId` (Worker service)
- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:Secret` (minimum 32 characters)
- `Jwt:ExpiryMinutes` (> 0)
- `Stripe:SecretKey`
- `Stripe:WebhookSecret`
- `Stripe:StarterPriceId`
- `Stripe:ProPriceId`
- `Stripe:SuccessUrl`
- `Stripe:CancelUrl`
- `Elastic:ServiceName`
- `Elastic:Environment`
- `Elastic:ElasticsearchUrl` (required when `Elastic:EnableElasticsearchSink=true`)
- `ElasticApm:ServerUrl`, `ElasticApm:ServiceName`, `ElasticApm:Environment` (required when `ElasticApm:Enabled=true`)
- `Encryption:MasterKey` (minimum 32 characters)

### Development exceptions

In `Development`, Stripe secrets are allowed to be empty so local startup is not blocked:

- `Stripe:SecretKey`
- `Stripe:WebhookSecret`
- `Stripe:StarterPriceId`
- `Stripe:ProPriceId`

`Stripe:SuccessUrl` and `Stripe:CancelUrl` are still required in all environments.

### Example environment variables

```bash
# MongoDB
MongoDb__ConnectionString=mongodb://localhost:27017
MongoDb__DatabaseName=hookbridge

# Kafka
Kafka__BootstrapServers=localhost:9092
Kafka__ConsumerGroupId=hookbridge-worker
Kafka__MessageTimeoutMs=10000

# JWT
Jwt__Issuer=hookbridge
Jwt__Audience=hookbridge-clients
Jwt__Secret=replace-with-at-least-32-char-secret
Jwt__ExpiryMinutes=60

# Stripe
Stripe__SecretKey=sk_live_...
Stripe__WebhookSecret=whsec_...
Stripe__StarterPriceId=price_...
Stripe__ProPriceId=price_...
Stripe__SuccessUrl=https://app.hookbridge.com/billing/success
Stripe__CancelUrl=https://app.hookbridge.com/billing/cancel

# Elastic
Elastic__EnableElasticsearchSink=true
Elastic__ElasticsearchUrl=http://elasticsearch:9200
Elastic__ServiceName=hookbridge-api
Elastic__Environment=Production

# Elastic APM
ElasticApm__Enabled=true
ElasticApm__ServerUrl=http://apm-server:8200
ElasticApm__ServiceName=hookbridge-api
ElasticApm__Environment=Production

# Encryption
Encryption__MasterKey=replace-with-at-least-32-char-master-key
```

## Secret Encryption at Rest

HookBridge encrypts sensitive outbound subscription authentication fields before they are stored in MongoDB:

- `Authentication.Basic.Password`
- `Authentication.OAuth2.ClientSecret`
- `Authentication.ApiKeyHeader.HeaderValue`
- `Authentication.HmacSignature.Secret`

Notes:
- `Encryption:MasterKey` is required in `Production` and must be at least 32 characters.
- API responses always mask secret values as `********` and never return encrypted payloads.
- Secret decryption happens only in memory right before webhook delivery.
- Do not rotate the master key manually yet (rotation workflow is not implemented).

## Validation and Security Limits

HookBridge enforces strict input validation to reduce abuse and unsafe outbound configuration.

### Event ingestion limits
- `EventType` is required, max 150 chars, and only allows letters/numbers/`.`/`-`/`_`.
- `EventId` is required and max 150 chars.
- `Data` is required and capped at 1,000,000 bytes.

### Subscription and outbound request limits
- `TargetUrl` is required, max 2048 chars, and must be an absolute HTTP/HTTPS URL.
- In `Production`, localhost/private-network target URLs are blocked by default.
- Custom headers are limited to 30 entries.
- Header names/values are required, length-limited (`100` / `1000`), and CR/LF-safe.
- Restricted outbound headers are blocked: `Host`, `Content-Length`, `Transfer-Encoding`, `Connection`.

### Authentication validation
- `Basic`: username/password required.
- `OAuth2ClientCredentials`: token URL/client id/client secret required; HTTPS token URL required in `Production`.
- `ApiKeyHeader`: header name/value required and CR/LF-safe.
- `HmacSignature`: secret required and algorithm must be `HMACSHA256`.

### Delivery attempt response-body storage
- Stored response bodies are truncated to 5000 characters.
- Truncation is tracked via `ResponseBodyTruncated`.

### Security configuration
Use the `Security` section:

```json
{
  "Security": {
    "AllowPrivateNetworkTargetUrls": false
  }
}
```

Defaults:
- `Development`: `true`
- `Production`: `false`

## Local Docker Compose Startup

From the repository root:
```bash
cd deploy
docker compose up --build
```

Local URLs:
- API: `http://localhost:5000/swagger`
- Dashboard: `http://localhost:3000`
- MongoDB: `mongodb://localhost:27017`
- Kafka: `localhost:9092`
- Elasticsearch: `http://localhost:9200`
- Kibana: `http://localhost:5601`
- APM Server: `http://localhost:8200`

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

## Local Elastic Stack Setup

Use Docker Compose from the repository root:
```bash
docker compose -f deploy/docker-compose.yml up -d
```

Local endpoints:
- Elasticsearch: `http://localhost:9200`
- Kibana: `http://localhost:5601`

Enable Elasticsearch shipping in development by setting `Elastic:EnableElasticsearchSink` to `true` in:
- `src/HookBridge.Api/appsettings.Development.json`
- `src/HookBridge.Worker/appsettings.Development.json`


## Elastic APM Setup

HookBridge now supports Elastic APM tracing for API requests, worker message processing, Kafka-related processing spans, and outbound webhook delivery HTTP spans.

### Local APM endpoint
- APM Server: `http://localhost:8200`

### Enable APM
Set `ElasticApm:Enabled` to `true` in:
- `src/HookBridge.Api/appsettings.Development.json`
- `src/HookBridge.Worker/appsettings.Development.json`

Each service has its own service name:
- API: `hookbridge-api`
- Worker: `hookbridge-worker`

### View traces in Kibana
1. Start local stack: `docker compose -f deploy/docker-compose.yml up -d`
2. Open Kibana: `http://localhost:5601`
3. Go to **Observability → APM → Services**.
4. Select `hookbridge-api` or `hookbridge-worker` and inspect traces, transactions, and spans.

Security notes:
- HookBridge does not send webhook payload bodies as APM labels.
- HookBridge does not attach secrets, authorization headers, API keys, tokens, or passwords as labels.
- HookBridge records only target host (not full URL query strings) for outbound webhook labels.

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


## Dashboard Authentication Flow

The React dashboard now includes end-to-end authentication screens and route protection:
- Register at `/register` by providing tenant id, full name, email, password, and role.
- Login at `/login` with email and password.
- On successful login/registration, the JWT is stored in browser local storage.
- Protected dashboard routes (for example `/overview`) require a stored token and redirect unauthenticated users to `/login`.
- Logout clears the local token and returns the user to `/login`.

Set `VITE_API_BASE_URL` (see `src/HookBridge.Dashboard/.env.example`) so the dashboard can call `POST /api/v1/auth/register` and `POST /api/v1/auth/login`.


## Dashboard Overview Page

The `/overview` dashboard route now integrates with `GET /api/v1/admin/dashboard/overview` and includes:
- Tenant summary (tenant name and tenant id).
- Plan badge and monthly usage display (`eventsReceivedThisMonth / monthlyEventLimit`).
- Automatic `Unlimited` usage display for Enterprise (or effectively unlimited) plans.
- Metrics cards for received/delivered/failed events, delivery attempts, DLQ events, and success rate.
- Loading and non-401 error states with a refresh action.
- Formatted date range, locale-formatted numbers, and a success-rate badge with 2 decimal precision.




## API Keys Dashboard Page

The `/api-keys` dashboard route now integrates with tenant API-key admin endpoints and includes:
- API key listing backed by `GET /api/v1/admin/tenants/{tenantId}/api-keys`.
- API key creation backed by `POST /api/v1/admin/tenants/{tenantId}/api-keys` with one-time plain key reveal, copy-to-clipboard action, and clear warning text.
- API key revoke action backed by `DELETE /api/v1/admin/tenants/{tenantId}/api-keys/{keyId}` with confirmation and post-action refresh.
- Loading, error, empty states, manual refresh, status badges (Active/Revoked), and masked key prefix display.
- Tenant context derived from the authenticated admin JWT tenant claim.

## Incoming Events Dashboard Page

The `/events` dashboard route now integrates with admin incoming-event APIs and includes:
- Incoming event listing backed by `GET /api/v1/admin/events`.
- Filters for event id, event type, status, received-at date range, and correlation id.
- Loading/error/empty states with refresh and clear-filters actions.
- Incoming-event status badges (Accepted, Delivered, Failed, PartiallyFailed, NoSubscriptions) and truncation for long values in the table.
- "View Details" modal backed by `GET /api/v1/admin/events/{id}` showing complete event fields (tenant, event metadata, source/received timestamps, correlation data, and full JSON payload).

### Search incoming events
```bash
curl "http://localhost:5000/api/v1/admin/events?eventId=evt_123&eventType=order.created&status=Accepted&fromDate=2026-04-27T00:00:00Z&toDate=2026-04-27T23:59:59Z&correlationId=test-correlation-001" \
  -H "Authorization: Bearer <jwt-token>"
```

### Get incoming event by id
```bash
curl "http://localhost:5000/api/v1/admin/events/incoming-123" \
  -H "Authorization: Bearer <jwt-token>"
```

## Delivery Logs Dashboard Page

The `/delivery-logs` dashboard route now integrates with admin delivery-attempt APIs and includes:
- Delivery log listing backed by `GET /api/v1/admin/delivery-logs`.
- Filters for event id, subscription id, event type, status, HTTP status code, date range, and target URL.
- Loading/error/empty states with refresh and clear-filters actions.
- Delivery status badges (Success, Failed, Pending), formatted timestamps, and duration display in milliseconds.
- "View Details" modal backed by `GET /api/v1/admin/delivery-logs/{id}` showing full attempt payload fields (tenant, event, subscription, status, response body, error message, and correlation data).

## Failed Events / DLQ Dashboard Page

The `/failed-events` dashboard route now integrates with admin failed-event APIs and includes:
- Failed event listing backed by `GET /api/v1/admin/failed-events`.
- Filters for event id, subscription id, event type, status, and failed-at date range.
- Loading/error/empty states with refresh and clear-filters actions.
- Failed-event status badges (DLQ and RetryRequested) and truncation for long target URL, reason, and last error message values in the table.
- "View Details" modal backed by `GET /api/v1/admin/failed-events/{id}` showing complete failed-event fields (tenant, event, subscription, target URL, reason, retry metadata, and correlation id).
- Manual retry action backed by `POST /api/v1/admin/failed-events/{id}/retry` for DLQ records with confirmation and success/error feedback.

## Health Dashboard Page

The `/health` dashboard route now integrates with service health endpoints and includes:
- Health cards for MongoDB, Kafka, Worker, and Elasticsearch.
- Endpoint integration with:
  - `GET /api/v1/health/mongodb`
  - `GET /api/v1/health/kafka`
  - `GET /api/v1/health/worker`
  - `GET /api/v1/health/elasticsearch`
- Per-service healthy/unhealthy badges, status messages, and last-checked timestamps.
- Refresh action that reruns all health checks with loading feedback.
- Partial-failure handling where one failing endpoint marks only that service unhealthy while the rest still render.

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

Dashboard overview endpoint:
```bash
curl http://localhost:5000/api/v1/admin/dashboard/overview \
  -H "Authorization: Bearer <jwt-token>"
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

# Example with pagination + sorting
curl "http://localhost:5000/api/v1/admin/delivery-logs?pageNumber=1&pageSize=50&sortBy=attemptedAt&sortDirection=desc"
```

### Get delivery log by id
```bash
curl http://localhost:5000/api/v1/admin/delivery-logs/{deliveryAttemptId}
```

## Dashboard Frontend (React)

A React + TypeScript + Vite dashboard foundation is available at `src/HookBridge.Dashboard`.

```bash
cd src/HookBridge.Dashboard
npm install
npm run dev
```

The app expects an API base URL in `.env` (see `.env.example`):

```bash
VITE_API_BASE_URL=http://localhost:5000
```

## Dashboard Subscriptions Page

The `/subscriptions` dashboard route now integrates with subscription admin APIs and supports:
- Listing subscriptions from `GET /api/v1/admin/subscriptions` in a table with event type, target URL, active status, timeout, retry policy, and creation time.
- Server-backed filters for `eventType`, `targetUrl`, and `isActive`.
- Creating subscriptions with tenant id, webhook target configuration, retry policy, timeout, custom headers, and authentication setup.
- Basic edit flow using `GET /api/v1/admin/subscriptions/{id}` + `PUT /api/v1/admin/subscriptions/{id}`.
- Operational actions for enable/disable and delete (`POST /enable`, `POST /disable`, `DELETE`).
- Loading, success, and error states, deletion confirmation prompts, and masked rendering of secret values in the UI.

All requests are sent through the shared dashboard `apiClient`, which automatically includes the JWT Bearer token.

## Dashboard Pagination and Sorting

The admin list dashboard pages now use backend paged responses and server-side sorting:

- Updated pages: `/subscriptions`, `/delivery-logs`, `/failed-events`, and `/events`.
- Shared pagination model: `PagedResponse<T>` and `PagedRequest` in `src/HookBridge.Dashboard/src/types/pagination.ts`.
- API calls now send and preserve `pageNumber`, `pageSize`, `sortBy`, and `sortDirection`.
- Reusable UI components:
  - `Pagination` (`Previous` / `Next`, page-size selector, page indicator, and `Showing X–Y of Z records` text)
  - `SortableHeader` (click-to-toggle `asc`/`desc` with arrow indicator)
- Filter and paging UX behavior:
  - Changing filters resets `pageNumber` to `1`
  - Changing page size resets `pageNumber` to `1`
  - Sorting keeps current filters applied
  - `Previous` and `Next` are disabled using backend `hasPreviousPage` and `hasNextPage`

## Billing Dashboard Page

The `/billing` dashboard route now integrates with tenant billing APIs and includes:
- Billing status retrieval backed by `GET /api/v1/admin/tenants/{tenantId}/billing/status`.
- Plan upgrade initiation for paid tiers backed by `POST /api/v1/admin/tenants/{tenantId}/billing/checkout`.
- Current billing summary cards (plan, status badge, monthly event limit, current period start/end).
- Pricing cards for Free, Starter, Pro, and Enterprise with current-plan highlighting.
- Checkout UX states for loading, errors, and in-progress redirect handling.
- Stripe redirect using only backend-provided checkout URL (no Stripe secrets in the dashboard).


## API Rate Limiting

HookBridge API uses ASP.NET Core rate limiting with separate policies for ingestion and admin APIs.

### Default limits (Development)

Configured in `src/HookBridge.Api/appsettings.Development.json`:

- `RateLimit:Enabled=true`
- Event ingestion (`POST /api/v1/events/{tenantId}`): `100` requests per `60` seconds
- Admin APIs (`/api/v1/admin/*`): `300` requests per `60` seconds

### How partitioning works

- **Event ingestion policy (`EventIngestionPolicy`)**
  - Partition key prefers route `tenantId`
  - Falls back to client IP when `tenantId` is unavailable
- **Admin API policy (`AdminApiPolicy`)**
  - Partition key prefers JWT `sub` claim
  - Falls back to client IP when `sub` is unavailable

### Limit exceeded behavior

When a request exceeds the configured limit, HookBridge returns:

- `429 Too Many Requests`
- JSON body:

```json
{
  "message": "Rate limit exceeded. Please try again later.",
  "traceId": "...",
  "statusCode": 429
}
```

- `Retry-After` header when the limiter can determine the next retry window

### Configuration

Set the `RateLimit` section in configuration (for example via environment variables):

```bash
RateLimit__Enabled=true
RateLimit__EventIngestionPermitLimit=100
RateLimit__EventIngestionWindowSeconds=60
RateLimit__AdminApiPermitLimit=300
RateLimit__AdminApiWindowSeconds=60
```

Set `RateLimit__Enabled=false` to disable limiter enforcement without removing policy wiring.

## Audit Logging

HookBridge records audit logs for high-impact admin operations. Audit entries are tenant-scoped and include actor context from JWT + request context (user id/email/role, IP address, user-agent).

### Audited actions
- Tenant created / updated / disabled
- API key created / revoked
- Subscription created / updated / deleted / enabled / disabled
- Failed event manual retry requested
- Billing checkout session created
- Billing plan/status updates triggered by Stripe webhooks

### Audit log API
- `GET /api/v1/admin/audit-logs`
- `GET /api/v1/admin/audit-logs/{id}`

Both endpoints require JWT and `AdminOrOwner` policy, and are enforced to the caller tenant scope.

### Sensitive data handling
Audit metadata is sanitized before persistence. HookBridge does **not** store plain secrets (including API key values, passwords, OAuth client secrets, HMAC secrets, Authorization headers, Stripe secrets, or JWT tokens) in audit logs.
