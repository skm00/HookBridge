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
