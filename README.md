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
