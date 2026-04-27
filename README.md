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
2. Store the incoming event in MongoDB first.
3. Publish a `WebhookEventMessage` to Kafka topic `webhook-events` using the tenant id as key.

If Kafka publish fails after the event is stored, the API still returns `202 Accepted` with message `Event accepted but publishing is delayed.` so customer ingestion is not rejected.

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
