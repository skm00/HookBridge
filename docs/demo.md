# HookBridge End-to-End Demo Script

## Demo objective
Use this script to run a polished end-to-end HookBridge demo for:
- Local testing
- Investor walkthroughs
- New-user onboarding
- README/live documentation walkthroughs

By the end of this flow, you will:
1. Start the platform locally.
2. Seed realistic demo data.
3. Sign in to the dashboard.
4. Create API credentials and subscriptions.
5. Deliver a successful webhook.
6. Simulate a failing webhook and DLQ lifecycle.
7. Manually retry failed events.
8. Review notifications, audit logs, billing, and observability surfaces.

---

## Prerequisites
- Docker + Docker Compose
- `curl`
- Optional: `jq` (for easier token extraction)
- Optional: a temporary request bin URL from [https://webhook.site](https://webhook.site)

---

## Start local environment
From the repository root:

```bash
cd deploy
docker compose up --build
```

Useful local URLs:
- Dashboard: http://localhost:3000
- API Swagger: http://localhost:5000/swagger
- API Base URL: `http://localhost:5000/api/v1`

---

## Seed demo data
The demo seed endpoint is available in development mode.

```bash
curl -X POST http://localhost:5000/api/v1/dev/demo/seed
```

Expected result: JSON summary with counts for tenant, admin users, subscriptions, events, delivery logs, failed events, notifications, and audit logs.

---

## Login to dashboard
Open: http://localhost:3000/login

Demo credentials:
- Email: `demo@hookbridge.local`
- Password: `DemoPassword123!`

After login, confirm you can access:
- Overview
- API Keys
- Subscriptions
- Events
- Delivery Logs
- Failed Events
- Notifications
- Audit Logs
- Billing

---

## Reusable placeholders
Use these placeholders throughout this doc:
- `{{TENANT_ID}}`
- `{{JWT_TOKEN}}`
- `{{API_KEY}}`
- `{{SUBSCRIPTION_ID}}`
- `{{FAILED_EVENT_ID}}`

---

## Optional: register admin (API flow)
If you want a fresh admin user instead of the seeded one:

```bash
curl -X POST http://localhost:5000/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "{{TENANT_ID}}",
    "email": "admin+demo@hookbridge.local",
    "password": "DemoPassword123!",
    "fullName": "Demo Admin",
    "role": 3
  }'
```

---

## Login (API flow)

```bash
curl -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "demo@hookbridge.local",
    "password": "DemoPassword123!"
  }'
```

Copy `data.token` into `{{JWT_TOKEN}}`.

---

## Create API key

```bash
curl -X POST http://localhost:5000/api/v1/admin/tenants/{{TENANT_ID}}/api-keys \
  -H "Authorization: Bearer {{JWT_TOKEN}}" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Demo Key"
  }'
```

Copy one-time plaintext key into `{{API_KEY}}`.

---

## Create subscription

### Successful webhook demo subscription
Use a request bin URL so you can verify payload delivery.

```bash
curl -X POST http://localhost:5000/api/v1/admin/subscriptions \
  -H "Authorization: Bearer {{JWT_TOKEN}}" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "{{TENANT_ID}}",
    "eventType": "order.created",
    "targetUrl": "https://webhook.site/<your-url>",
    "headers": [
      { "name": "x-demo-source", "value": "hookbridge-demo" }
    ],
    "retryPolicy": {
      "maxAttempts": 3,
      "initialDelaySeconds": 10,
      "backoffType": "Exponential"
    },
    "timeoutSeconds": 30
  }'
```

Copy `data.id` into `{{SUBSCRIPTION_ID}}`.

### Failing webhook demo subscription
Create a second subscription that always fails.

```bash
curl -X POST http://localhost:5000/api/v1/admin/subscriptions \
  -H "Authorization: Bearer {{JWT_TOKEN}}" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "{{TENANT_ID}}",
    "eventType": "payment.failed",
    "targetUrl": "https://httpstat.us/500",
    "retryPolicy": {
      "maxAttempts": 3,
      "initialDelaySeconds": 5,
      "backoffType": "Exponential"
    },
    "timeoutSeconds": 30
  }'
```

---

## Send event

### Successful event (`order.created`)

```bash
curl -X POST http://localhost:5000/api/v1/events/{{TENANT_ID}} \
  -H "x-api-key: {{API_KEY}}" \
  -H "x-correlation-id: demo-success-001" \
  -H "Content-Type: application/json" \
  -d '{
    "eventType": "order.created",
    "eventId": "evt-order-created-001",
    "timestamp": "2026-04-28T12:00:00Z",
    "data": {
      "orderId": "ORD-1001",
      "amount": 149.99,
      "currency": "USD"
    }
  }'
```

### Failed event (`payment.failed`)

```bash
curl -X POST http://localhost:5000/api/v1/events/{{TENANT_ID}} \
  -H "x-api-key: {{API_KEY}}" \
  -H "x-correlation-id: demo-failure-001" \
  -H "Content-Type: application/json" \
  -d '{
    "eventType": "payment.failed",
    "eventId": "evt-payment-failed-001",
    "timestamp": "2026-04-28T12:05:00Z",
    "data": {
      "paymentId": "PAY-2001",
      "reason": "card_declined"
    }
  }'
```

---

## View delivery logs

```bash
curl "http://localhost:5000/api/v1/admin/delivery-logs?pageNumber=1&pageSize=20" \
  -H "Authorization: Bearer {{JWT_TOKEN}}"
```

For the success flow, verify at least one delivery log has success status.

---

## Trigger failed delivery
A failed delivery is triggered by sending `payment.failed` after creating the `https://httpstat.us/500` subscription.

Expected behavior:
1. Delivery attempts fail with HTTP 500.
2. Retry policy executes retries.
3. Event is moved to DLQ after max attempts.

---

## View failed events / DLQ

```bash
curl "http://localhost:5000/api/v1/admin/failed-events?pageNumber=1&pageSize=20&status=DLQ" \
  -H "Authorization: Bearer {{JWT_TOKEN}}"
```

Copy one failed event ID into `{{FAILED_EVENT_ID}}`.

---

## Manual retry

```bash
curl -X POST http://localhost:5000/api/v1/admin/failed-events/{{FAILED_EVENT_ID}}/retry \
  -H "Authorization: Bearer {{JWT_TOKEN}}"
```

Expected result: accepted response (retry re-queued).

---

## Search incoming events

```bash
curl "http://localhost:5000/api/v1/admin/events?pageNumber=1&pageSize=20&eventType=order.created" \
  -H "Authorization: Bearer {{JWT_TOKEN}}"
```

---

## View notifications

```bash
curl "http://localhost:5000/api/v1/admin/notifications?pageNumber=1&pageSize=20" \
  -H "Authorization: Bearer {{JWT_TOKEN}}"
```

Look for warning/error notifications related to failed deliveries or retries.

---

## View audit logs

```bash
curl "http://localhost:5000/api/v1/admin/audit-logs?pageNumber=1&pageSize=20" \
  -H "Authorization: Bearer {{JWT_TOKEN}}"
```

Look for actions such as:
- API key creation
- Subscription creation
- Manual retry requests

---

## Billing page preview
Dashboard: http://localhost:3000/billing

Optional API check:

```bash
curl "http://localhost:5000/api/v1/admin/tenants/{{TENANT_ID}}/billing/status" \
  -H "Authorization: Bearer {{JWT_TOKEN}}"
```

Use this in demos to explain plan, limits, and upgrade flow.

---

## Observability preview
- Open Kibana at `http://localhost:5601`
- Go to **Observability → APM → Services**
- Inspect API + Worker traces during event ingestion and webhook delivery

Use this section in investor demos to highlight operational maturity.

---

## Dashboard demo checklist
Use this as your live-demo runbook:

- [ ] Local stack started (`docker compose up --build`)
- [ ] Demo seed completed (`POST /api/v1/dev/demo/seed`)
- [ ] Dashboard login succeeded
- [ ] API key created
- [ ] Success subscription created (`webhook.site`)
- [ ] `order.created` sent and success delivery visible
- [ ] Failing subscription created (`https://httpstat.us/500`)
- [ ] `payment.failed` sent and retries started
- [ ] Failed event visible in DLQ
- [ ] Manual retry triggered
- [ ] Notifications reviewed
- [ ] Audit logs reviewed
- [ ] Billing page previewed
- [ ] Observability/APM previewed
