# HookBridge API Examples

Reusable cURL examples for common HookBridge operations.

## Variables
Set these once in your shell:

```bash
BASE_URL="http://localhost:5000/api/v1"
TENANT_ID="{{TENANT_ID}}"
JWT_TOKEN="{{JWT_TOKEN}}"
API_KEY="{{API_KEY}}"
SUBSCRIPTION_ID="{{SUBSCRIPTION_ID}}"
FAILED_EVENT_ID="{{FAILED_EVENT_ID}}"
```

---

## Auth

### Register admin
```bash
curl -X POST "$BASE_URL/auth/register" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "{{TENANT_ID}}",
    "email": "admin+sample@hookbridge.local",
    "password": "DemoPassword123!",
    "fullName": "Sample Admin",
    "role": 3
  }'
```

### Login
```bash
curl -X POST "$BASE_URL/auth/login" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "demo@hookbridge.local",
    "password": "DemoPassword123!"
  }'
```

---

## API keys

### Create API key
```bash
curl -X POST "$BASE_URL/admin/tenants/$TENANT_ID/api-keys" \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Sample API Key"
  }'
```

### List API keys
```bash
curl "$BASE_URL/admin/tenants/$TENANT_ID/api-keys" \
  -H "Authorization: Bearer $JWT_TOKEN"
```

---

## Subscriptions

### Create subscription
```bash
curl -X POST "$BASE_URL/admin/subscriptions" \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "{{TENANT_ID}}",
    "eventType": "order.created",
    "targetUrl": "https://webhook.site/<your-url>",
    "retryPolicy": {
      "maxAttempts": 3,
      "initialDelaySeconds": 10,
      "backoffType": "Exponential"
    },
    "timeoutSeconds": 30
  }'
```

### Search subscriptions
```bash
curl "$BASE_URL/admin/subscriptions?pageNumber=1&pageSize=20&eventType=order.created" \
  -H "Authorization: Bearer $JWT_TOKEN"
```

---

## Events

### Send event
```bash
curl -X POST "$BASE_URL/events/$TENANT_ID" \
  -H "x-api-key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "eventType": "order.created",
    "eventId": "evt-sample-001",
    "timestamp": "2026-04-28T12:30:00Z",
    "data": {
      "orderId": "ORD-5001",
      "amount": 75.50,
      "currency": "USD"
    }
  }'
```

### Search incoming events
```bash
curl "$BASE_URL/admin/events?pageNumber=1&pageSize=20" \
  -H "Authorization: Bearer $JWT_TOKEN"
```

---

## Delivery logs

### Search delivery logs
```bash
curl "$BASE_URL/admin/delivery-logs?pageNumber=1&pageSize=20" \
  -H "Authorization: Bearer $JWT_TOKEN"
```

---

## Failed events

### Search failed events
```bash
curl "$BASE_URL/admin/failed-events?pageNumber=1&pageSize=20&status=DLQ" \
  -H "Authorization: Bearer $JWT_TOKEN"
```

### Manual retry
```bash
curl -X POST "$BASE_URL/admin/failed-events/$FAILED_EVENT_ID/retry" \
  -H "Authorization: Bearer $JWT_TOKEN"
```

---

## Notifications

### Search notifications
```bash
curl "$BASE_URL/admin/notifications?pageNumber=1&pageSize=20" \
  -H "Authorization: Bearer $JWT_TOKEN"
```

### Get unread notification count
```bash
curl "$BASE_URL/admin/notifications/unread-count" \
  -H "Authorization: Bearer $JWT_TOKEN"
```

---

## Audit logs

### Search audit logs
```bash
curl "$BASE_URL/admin/audit-logs?pageNumber=1&pageSize=20" \
  -H "Authorization: Bearer $JWT_TOKEN"
```

---

## Billing

### Get billing status
```bash
curl "$BASE_URL/admin/tenants/$TENANT_ID/billing/status" \
  -H "Authorization: Bearer $JWT_TOKEN"
```

### Create checkout session
```bash
curl -X POST "$BASE_URL/admin/tenants/$TENANT_ID/billing/checkout" \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "plan": 1
  }'
```

---

## Dashboard overview

### Get dashboard overview
```bash
curl "$BASE_URL/admin/dashboard/overview" \
  -H "Authorization: Bearer $JWT_TOKEN"
```
