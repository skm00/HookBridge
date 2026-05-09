# HookBridge

Initial production-style SaaS solution scaffold for a multi-tenant webhook delivery platform.

## Support This Project

If HookBridge helps your team, please consider supporting ongoing development.

<iframe src="https://github.com/sponsors/skm00/card" title="Sponsor skm00" height="225" width="600" style="border: 0;"></iframe>

[💖 Sponsor this project](https://github.com/sponsors/skm00)

## Stack
- .NET 8
- Clean Architecture
- MongoDB
- Kafka (planned)
- React dashboard (planned)

## Run the Demo

For a complete guided demo flow (local setup, seeded login, success/failure webhook scenarios, DLQ retry, and dashboard walkthrough), use:

- Demo script: [`docs/demo.md`](docs/demo.md)
- Reusable API examples: [`docs/api-examples.md`](docs/api-examples.md)
- Postman collection: [`docs/postman/hookbridge.postman_collection.json`](docs/postman/hookbridge.postman_collection.json)
- Thunder Client collection: [`docs/thunder-client/hookbridge.json`](docs/thunder-client/hookbridge.json)

## Dashboard Public Website Routes

HookBridge Dashboard now includes a public marketing website for product discovery and self-serve signup.

Public routes (no auth token required):
- `/` — Landing page with product overview, feature highlights, FAQ, and CTA buttons.
- `/pricing` — Public pricing plans (Free, Starter, Pro, Enterprise).
- `/docs` — Documentation home (Quickstart).
- `/docs/quickstart` — Account setup, API key creation, first subscription, first event, and delivery logs.
- `/docs/events` — Ingestion endpoint, headers, request/response schemas, and curl example.
- `/docs/subscriptions` — Subscription model, event matching, target URLs, headers, and auth options.
- `/docs/authentication` — Inbound `x-api-key` and outbound Basic/API-key/OAuth2/HMAC authentication.
- `/docs/retries` — Fixed and exponential retry behavior, DLQ flow, and manual replay.
- `/docs/errors` — Common API error codes (`400/401/403/429/500`) and troubleshooting.
- `/login` — Admin sign-in page.
- `/register` — Admin registration page.

Protected dashboard routes (auth required):
- `/overview` and all operational pages such as tenants, subscriptions, events, delivery logs, billing, and settings.

## Dashboard Theme & Responsive UI Notes

The React dashboard frontend now uses a centralized Tailwind theme and shared UI primitives for a more consistent, production-style experience.

- **Theme tokens** in `tailwind.config.js` define primary/background/surface/border/text scales plus success/warning/error semantic colors.
- **Global UI baselines** in `src/HookBridge.Dashboard/src/index.css` provide:
  - Improved dashboard background gradient
  - Font smoothing defaults
  - Shared focus-visible ring styles
  - Reusable utility classes (`hb-card`, `hb-btn-*`, `hb-input`, `hb-select`, `hb-table-wrap`)
- **Responsive shell improvements**:
  - Mobile sidebar drawer with overlay + close behavior
  - Grouped navigation sections for Product, Monitoring, Administration, and System
  - Sticky header with current page context, user identity hints, notifications, and styled logout action
- **Reusable UI components** (dependency-free):
  - `Button`, `Badge`, `Card`, `Input`, `Select`, `Modal`, `TableContainer`
- **Responsive content behavior**:
  - Data tables remain horizontally scrollable on small screens
  - Card grids naturally stack on mobile breakpoints
  - Forms continue to support single-column layout at small widths

## API Versioning

HookBridge APIs are versioned using URL segments for long-term compatibility.

- Current stable version: `v1`
- Route format: `/api/v{version}/...` (for example, `/api/v1/events/{tenantId}` and `/api/v1/admin/subscriptions`)
- Future versions will follow the same pattern, such as `/api/v2/...`

## Event Ingestion Formats (Raw + CloudEvents)

HookBridge supports multiple ingestion formats. CloudEvents support is **optional** and additive; existing raw webhook payload mode continues to work.

### A) Raw JSON payload

```json
{
  "username": "abc"
}
```

### B) HookBridge envelope

```json
{
  "eventType": "invoice.created",
  "payload": { "invoiceId": "INV-001" }
}
```

### C) CloudEvents v1.0 structured

```json
{
  "specversion": "1.0",
  "id": "evt_123",
  "source": "/example",
  "type": "invoice.created",
  "data": { "invoiceId": "INV-001" }
}
```

### D) CloudEvents v1.0 binary

- Include CloudEvents attributes as headers:
  - `ce-specversion`
  - `ce-id`
  - `ce-source`
  - `ce-type`
  - `ce-time` (optional)
- Send event data in the HTTP body.

### Mapping and routing behavior

- `CloudEvents.type` maps to HookBridge `EventType`.
- If `EventType` is missing, HookBridge uses `"default"`.
- Subscription matching supports:
  - exact event type,
  - wildcard `"*"`,
  - empty event type (treated as wildcard).

### Optional strict validation

CloudEvents strict validation can be toggled via configuration and defaults to disabled:

```bash
CloudEvents__StrictValidation=false
```

## Swagger / OpenAPI

HookBridge publishes OpenAPI documentation for local development to help with discovery, SDK generation, and API testing.

- Swagger UI (Development): `http://localhost:5000/swagger`
- OpenAPI document (v1): `http://localhost:5000/swagger/v1/swagger.json`

### Authentication in Swagger

- **Bearer JWT**: used by admin APIs under `/api/v1/admin/...`.
  - Set `Authorization: Bearer <token>`
- **x-api-key**: used by event ingestion under `/api/v1/events/{tenantId}`.
  - Set `x-api-key: <tenant-api-key>`
- Public endpoints do not require auth:
  - `/api/v1/auth/login`
  - `/api/v1/auth/register`
  - `/health` and `/api/v1/health/*`
  - `/api/v1/billing/stripe/webhook`

### API versioning in Swagger

- Swagger is version-aware and serves `/swagger/v1/swagger.json`.
- Swagger UI includes the `v1` document in the dropdown selector.

## API Key IP Allowlist

For inbound event ingestion, each API key can optionally define an IP allowlist. When configured, HookBridge will only accept requests from matching client IPs.

### Supported formats

- Exact IPv4/IPv6 addresses (for example, `192.168.1.10`)
- CIDR ranges (for example, `10.0.0.0/24`)
- Maximum 50 entries per API key

### Examples

```json
{
  "name": "Production ingestion key",
  "allowedIpAddresses": [
    "192.168.1.10",
    "10.0.0.0/24"
  ]
}
```

### Security recommendations

- Use explicit static egress IPs from your webhook source whenever possible.
- Prefer narrow CIDR blocks over broad ranges.
- Keep allowlists per environment (dev/stage/prod) and rotate keys if network ownership changes.
- If HookBridge runs behind a reverse proxy/load balancer, ensure `X-Forwarded-For` is preserved correctly.

### Export `swagger.json`

Use curl locally:

```bash
curl http://localhost:5000/swagger/v1/swagger.json -o swagger.v1.json
```

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


## Data Retention and Automated Cleanup

HookBridge includes configurable retention windows to prevent unbounded growth in MongoDB collections.

### Default retention

```json
{
  "DataRetention": {
    "Enabled": true,
    "IncomingEventsDays": 30,
    "DeliveryLogsDays": 30,
    "FailedEventsDays": 90,
    "AuditLogsDays": 90,
    "NotificationsDays": 30
  }
}
```

### How to configure

Set the `DataRetention` section in appsettings or environment variables:

```bash
DataRetention__Enabled=true
DataRetention__IncomingEventsDays=30
DataRetention__DeliveryLogsDays=30
DataRetention__FailedEventsDays=90
DataRetention__AuditLogsDays=90
DataRetention__NotificationsDays=30
```

### Cleanup job behavior

- The worker executes cleanup every 24 hours.
- Cleanup is skipped when `DataRetention:Enabled=false`.
- Each cleanup operation deletes records older than `UtcNow - RetentionDays`.
- Safety guard: the most recent 24 hours of data is never deleted, regardless of retention values.
- A warning is logged for retention values below 7 days.
- Structured logs include entity type, deleted count, retention days, and cutoff date.

## Production Readiness Checklist

Use the readiness endpoint to verify whether a deployment is ready to go live.

### Endpoint

```bash
curl -H "Authorization: Bearer <owner-jwt>" \
  http://localhost:5000/api/v1/admin/system/production-readiness
```

- Requires JWT authentication.
- Requires the `OwnerOnly` authorization policy.
- Returns `isReady` (overall) and a per-check `checks` array with `name`, `isReady`, and `message`.

### Checklist items

The endpoint validates:

1. MongoDB connection configured and reachable.
2. Kafka configured (`Kafka:BootstrapServers`).
3. JWT secret length is at least 32 characters.
4. Encryption master key length is at least 32 characters.
5. Stripe `SecretKey` and `WebhookSecret` configured (required in Production).
6. CORS configured, with no wildcard origin in Production.
7. Rate limiting enabled (`RateLimit:Enabled=true`).
8. Elastic configured when `Elastic:EnableElasticsearchSink=true`.
9. Elastic APM configured when `ElasticApm:Enabled=true`.
10. HTTPS and HSTS enabled in Production.
11. Email settings configured when `Email:Enabled=true`.
12. Demo data seeding disabled in Production (`DemoData:Enabled=false`).

### Critical vs warning-only behavior

- **Critical checks** fail the overall `isReady` value.
- **Warning-only checks** are still reported, but do not block overall readiness when they fail (for example optional integrations that are disabled).

### Required before go-live

Before a Production cutover, ensure at minimum:

- MongoDB, Kafka, JWT, and encryption settings are valid.
- Stripe production secrets are set.
- CORS allowlist is explicit (no `*`).
- Rate limiting is enabled.
- HTTPS/HSTS is enabled in your production host configuration.
- Demo data seeding is disabled.

## Email Notifications Setup

HookBridge supports SMTP-backed email delivery for high-severity notifications.

### SMTP configuration

Configure the `Email` section in `appsettings` or environment variables:

```json
{
  "Email": {
    "Enabled": false,
    "Provider": "Smtp",
    "SmtpHost": "smtp.example.com",
    "SmtpPort": 587,
    "SmtpUsername": "smtp-user",
    "SmtpPassword": "smtp-password",
    "FromEmail": "noreply@hookbridge.local",
    "FromName": "HookBridge",
    "UseSsl": true
  }
}
```

Environment variable equivalents:

```bash
Email__Enabled=true
Email__Provider=Smtp
Email__SmtpHost=smtp.example.com
Email__SmtpPort=587
Email__SmtpUsername=smtp-user
Email__SmtpPassword=smtp-password
Email__FromEmail=noreply@hookbridge.local
Email__FromName=HookBridge
Email__UseSsl=true
```

### Enabled flag behavior

- When `Email:Enabled=false`, notification emails are skipped.
- When `Email:Enabled=true`, HookBridge sends via SMTP with HTML email bodies.

### Notification email rules

- Only `Critical` and `Error` notifications trigger email.
- Recipients are selected from `Tenant.NotificationEmails`.
- If `NotificationEmails` is empty, HookBridge falls back to `Tenant.ContactEmail`.
- Failures in email delivery are logged and never block notification creation.

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

## Demo Data Seed (Development)

HookBridge can seed end-to-end demo data (tenant, admin, API key, subscriptions, events, delivery logs, failed events, notifications, and audit logs) for product demos.

### Enable demo data

Set the `DemoData` config section (for example in `appsettings.Development.json`):

```json
{
  "DemoData": {
    "Enabled": true,
    "AdminEmail": "demo@hookbridge.local",
    "AdminPassword": "DemoPassword123!",
    "TenantName": "Demo Company",
    "TenantSlug": "demo-company"
  }
}
```

Notes:
- Auto-seeding only runs on API startup in `Development` when `DemoData:Enabled=true`.
- Production does not auto-run demo seed logic.

### Demo login

- Email: `demo@hookbridge.local`
- Password: `DemoPassword123!`

### Demo API key

- Development demo key (printed to API console logs only in Development): `hb_live_demo_key_for_local_testing`
- Stored in database as a hash, not plain text.

### Trigger seed endpoint manually

Development-only endpoint:

```bash
curl -X POST http://localhost:5000/api/v1/dev/demo/seed
```

The endpoint is unavailable in Production and returns summary counts for seeded demo records in Development.

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
- Register at `/register` by providing email, password, and optional organization name. TenantId is generated automatically and the first user becomes Owner.
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

## Audit Logs Dashboard Page

The `/audit-logs` dashboard route now integrates with admin audit-log APIs and includes:
- Audit log listing backed by `GET /api/v1/admin/audit-logs`.
- Filters for user email, action, resource type, resource id, and created-at date range.
- Loading/error/empty states with refresh and clear-filters actions.
- Sortable columns for created time, user email, action, and resource type, plus server-backed pagination controls.
- Action/resource-type badges, truncation for long resource id + description values in table rows, and a details modal backed by `GET /api/v1/admin/audit-logs/{id}`.
- Defensive metadata rendering that masks values for sensitive keys (for example password/secret/token/authorization/apiKey/clientSecret).

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

### Troubleshooting: Kafka and Worker are healthy, but no webhook reaches target URL

If the incoming event is accepted but your endpoint never receives a webhook, verify these in order:

1. Confirm at least one **enabled subscription** exists for the same tenant and event type.
   - If no subscription matches, the incoming event is stored but delivery is skipped (status can become `NoSubscriptions`).
2. Confirm the subscription `targetUrl` is reachable from the worker container/pod network.
   - Test from inside the worker runtime, not just from your laptop.
3. Confirm worker logs show `WebhookEventConsumerWorker` consumption activity after ingestion.
   - A healthy worker endpoint only confirms process availability, not per-message delivery success.
4. Confirm Kafka topic and consumer-group settings are aligned between API and worker.
   - Topic should be `webhook-events`.
   - Consumer group should match your deployed configuration (`Kafka:ConsumerGroupId`).
5. Check delivery attempts and failed events in admin APIs/UI.
   - `GET /api/v1/admin/delivery-logs`
   - `GET /api/v1/admin/failed-events`
6. Check whether first delivery failed and was moved into retry flow.
   - Retry messages are produced to `webhook-retry` and later consumed by `WebhookRetryConsumerWorker`.

#### Exactly where to check (copy/paste)

Use these admin endpoints to see the full path from ingestion to delivery:

```bash
# 1) Incoming event record and status (Accepted/Delivered/Failed/NoSubscriptions/PartiallyFailed)
curl "http://localhost:5000/api/v1/admin/events?eventId=<your-event-id>&pageNumber=1&pageSize=20"

# 2) Delivery attempts (HTTP response code, error, duration, target URL)
curl "http://localhost:5000/api/v1/admin/delivery-logs?eventId=<your-event-id>&pageNumber=1&pageSize=20"

# 3) Failed-event/DLQ record if retries were exhausted
curl "http://localhost:5000/api/v1/admin/failed-events?eventId=<your-event-id>&pageNumber=1&pageSize=20"
```

And filter logs by correlation id or event id in both API and worker services:

- API logs to confirm ingestion + Kafka publish.
- Worker logs to confirm Kafka consume + webhook send attempt.

If you only see API logs (like `GET /api/v1/admin/subscriptions`) but no worker consume/delivery logs, the event is likely not being consumed by the worker consumer group yet.

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
    "name": "Ingestion Key",
    "enableSignatureValidation": true,
    "signatureSecret": "replace-with-random-secret",
    "signatureHeaderName": "x-hookbridge-signature"
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
  -H "x-hookbridge-signature: sha256=<hex-or-base64-signature>" \
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

### Webhook signature verification (optional but recommended)

If `enableSignatureValidation` is set to `true` on the API key, HookBridge validates the incoming request payload signature:
- Algorithm: `HMACSHA256`
- Header format: `sha256=<hex-or-base64-signature>`
- Default header name: `x-hookbridge-signature` (configurable per API key)

Python example for generating the header:

```python
import hmac
import hashlib
import base64

payload = b'{"eventType":"order.created","eventId":"evt_123","data":{"orderId":"1001"}}'
secret = b'replace-with-random-secret'

digest = hmac.new(secret, payload, hashlib.sha256).digest()
hex_signature = digest.hex()
base64_signature = base64.b64encode(digest).decode("utf-8")

print(f"x-hookbridge-signature: sha256={hex_signature}")
# or
print(f"x-hookbridge-signature: sha256={base64_signature}")
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
VITE_API_BASE_URL=http://localhost:52865
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

- Updated pages: `/subscriptions`, `/delivery-logs`, `/failed-events`, `/events`, and `/audit-logs`.
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

## Notification System Foundation

HookBridge now includes an in-app notification foundation for tenant admins.

### Notification types
- `WebhookFailure`
- `DlqCreated`
- `BillingPaymentFailed`
- `UsageLimitWarning`
- `UsageLimitExceeded`

### Severity levels
- `Info`
- `Warning`
- `Error`
- `Critical`

### Admin notification APIs (JWT required, ViewerOrAbove)
- `GET /api/v1/admin/notifications`
- `GET /api/v1/admin/notifications/{id}`
- `POST /api/v1/admin/notifications/{id}/read`
- `GET /api/v1/admin/notifications/unread-count`

Notes:
- All notification APIs are tenant-scoped to the current JWT tenant.
- Cross-tenant access is blocked.
- Email delivery is not implemented yet; this is in-app notification persistence and retrieval only.

## Notifications Dashboard Page

The dashboard now includes a dedicated `/notifications` route with in-app notification management:

- Notification table with tenant-scoped data: created time, severity, type, title, message, resource type/id, and read state.
- Filter support for `type`, `severity`, `isRead`, `fromDate`, and `toDate`.
- Server-driven pagination plus sortable headers for `createdAt`, `type`, `severity`, and `isRead`.
- Detail modal (`View Details`) for full notification context including `TenantId`, `ReadAt`, and timestamps.
- `Mark as Read` action for unread entries with success/error feedback and automatic list refresh.
- Header notification indicator that shows `Notifications (N)` when unread items exist and navigates to `/notifications`.
- Sidebar navigation now includes a direct Notifications link.

Error-handling behavior:
- Notification search failures show: `Unable to load notifications.`
- Mark-as-read failures show: `Unable to mark notification as read.`
- Header unread-count failures fail silently and show no unread badge count.

## API response format

HookBridge API responses now follow a consistent envelope for successful and error responses.

### Success response (`ApiResponse<T>`)

```json
{
  "success": true,
  "message": "Optional message",
  "data": {
    "id": "tenant_123"
  },
  "traceId": "0HMV..."
}
```

### Error response (`ApiErrorResponse`)

```json
{
  "success": false,
  "message": "An unexpected error occurred.",
  "statusCode": 500,
  "traceId": "0HMV..."
}
```

### Validation error response

```json
{
  "success": false,
  "message": "Validation failed.",
  "statusCode": 400,
  "traceId": "0HMV...",
  "errors": {
    "field": ["Field is required."]
  }
}
```

### Standard auth and throttling errors

- `401`: `Unauthorized.`
- `403`: `Forbidden.`
- `429`: `Rate limit exceeded. Please try again later.`

## Frontend Error Handling

The React dashboard standardizes API error handling using the backend `ApiErrorResponse` contract (`success`, `message`, `statusCode`, `traceId`, `errors`).

- Shared utility helpers parse backend errors and network failures into consistent user-facing messages.
- `traceId` values from API errors are preserved and shown in shared error alerts for support/debugging.
- Validation errors are surfaced at both form level and field level, with support for camelCase/PascalCase backend field names.
- `401` responses clear auth and redirect to `/login`, while `403` and `429` show friendly inline messages without logging users out.

## Frontend UX Components

The dashboard includes reusable frontend UX components for consistent loading and empty-state behavior:

- `LoadingSpinner` — Tailwind spinner with size variants (`sm`, `md`, `lg`) and accessible label support.
- `SkeletonCard` — pulse-based metric/card placeholder for dashboard-style cards.
- `SkeletonTable` — configurable table skeleton placeholder with `rows` and `columns` props.
- `EmptyState` — reusable empty-state container with title, optional description, and optional action slot.
- `PageHeader` — standardized page title/description/actions header used across dashboard pages.

## Backup and Restore

HookBridge now includes a documented manual backup/restore strategy and minimal tenant-level export/import hooks.

### Manual MongoDB backup/restore

- Backup command:

```bash
mongodump --uri="mongodb://..." --out=backup/
```

- Restore command:

```bash
mongorestore --uri="mongodb://..." backup/
```

See full operational guidance in [`docs/backup-restore.md`](docs/backup-restore.md).

### API-based tenant export/import

Owner admins can use tenant-scoped endpoints:

- `GET /api/v1/admin/tenants/{tenantId}/backup`
- `POST /api/v1/admin/tenants/{tenantId}/restore` (multipart file upload, 10MB max)

Behavior notes:
- Requires JWT authentication and `OwnerOnly` policy.
- Enforces tenant isolation.
- Export omits plain secrets (for example, plain API keys are never included).

### Limitations

- Current implementation is intended for manual/export workflows and does **not** replace full infrastructure backup automation.
- Full-environment disaster recovery (database snapshots, object storage replication, cross-region failover) should still be handled via infrastructure tooling.

## Feature Flags

HookBridge supports lightweight configuration-based feature flags for environment-level rollout, with optional per-tenant overrides.

### Configuration

Add a `FeatureFlags` section in `appsettings` (or environment variables):

```json
{
  "FeatureFlags": {
    "Flags": {
      "EnableBilling": true,
      "EnableEmailNotifications": false,
      "EnableAdvancedDashboard": true,
      "EnableAuditLogs": true
    },
    "TenantFeatureOverrides": [
      {
        "TenantId": "tenant-beta",
        "FlagName": "EnableAdvancedDashboard",
        "IsEnabled": false
      }
    ]
  }
}
```

Environment variable equivalents:

```bash
FeatureFlags__Flags__EnableBilling=true
FeatureFlags__Flags__EnableEmailNotifications=false
FeatureFlags__Flags__EnableAdvancedDashboard=true
FeatureFlags__TenantFeatureOverrides__0__TenantId=tenant-beta
FeatureFlags__TenantFeatureOverrides__0__FlagName=EnableAdvancedDashboard
FeatureFlags__TenantFeatureOverrides__0__IsEnabled=false
```

### Runtime usage in code

Use `IFeatureFlagService`:

- `IsEnabled("EnableBilling")` for global feature checks.
- `IsEnabled("EnableBilling", tenantId)` for tenant-aware checks.

Behavior:
- Missing flags default to `false`.
- Lookup is case-insensitive.
- Tenant override takes precedence over global flag when a match exists.

### Endpoint gating with attribute

Use `[RequireFeature("FeatureName")]` on controllers/actions.

When disabled, HookBridge returns `404 Not Found` so gated endpoints remain hidden.

Examples in this repository:
- `BillingController` is gated by `EnableBilling`.
- `DashboardController` is gated by `EnableAdvancedDashboard`.
- Notification email dispatch is gated by `EnableEmailNotifications` in `NotificationService`.

## Endpoint Validation

Use `POST /api/v1/admin/endpoint-validation` to test webhook target connectivity before saving a subscription. The endpoint requires JWT auth with `DeveloperOrAbove` policy, validates HTTP/HTTPS URLs, enforces timeout range (1-30s), and blocks localhost/private network targets in Production unless `Security:AllowPrivateNetworkTargetUrls=true`.

The response includes success/failure, status code, duration, and a truncated response body (up to 2000 chars). This action is non-persistent and does not create or update subscriptions.
