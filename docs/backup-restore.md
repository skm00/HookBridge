# Backup and Restore Strategy

This document defines the **manual** production backup/restore approach for HookBridge MongoDB data.

## Scope: what data is backed up

HookBridge stores critical SaaS data in MongoDB. Backups should include:

- Tenants
- API keys
- Subscriptions
- Events
- Logs
- Notifications
- Audit logs

For tenant-level API export/import, HookBridge includes tenant-scoped data for:

- Tenant
- Subscriptions
- API keys (no plain API key value)
- Events (default export cap: last 100)
- Failed events
- Notifications
- Audit logs

## Full database backup strategy (mongodump)

Use `mongodump` for operational full backups of the MongoDB database.

```bash
mongodump --uri="mongodb://..." --out=backup/
```

Recommended notes:
- Run from a trusted admin host/runner.
- Use TLS-enabled MongoDB connection strings in production.
- Encrypt backup artifacts at rest and in transit.
- Restrict backup file access to operations/security personnel only.

## Restore strategy (mongorestore)

Use `mongorestore` for full database restores.

```bash
mongorestore --uri="mongodb://..." backup/
```

Recommended restore workflow:
1. Restore first to a staging environment.
2. Validate tenant counts, subscription counts, and sample event integrity.
3. Execute production restore in a controlled maintenance window.
4. Rotate any credentials as needed after recovery operations.

## Backup frequency recommendations

- **Daily full backup** (minimum baseline)
- **Optional hourly snapshots** for reduced RPO on critical workloads

## Retention recommendations

- **7 daily backups**
- **4 weekly backups**
- **3 monthly backups**

## API-based tenant export/import (admin-triggered)

HookBridge provides minimal tenant-scoped backup hooks for admin operations:

- `GET /api/v1/admin/tenants/{tenantId}/backup`
- `POST /api/v1/admin/tenants/{tenantId}/restore`

Behavior:
- JWT required
- `OwnerOnly` policy required
- Tenant isolation enforced
- Restore upload limit: 10MB
- Backup output is gzip-compressed JSON

## Security constraints

- Do **not** export plain API key values.
- Subscription secrets remain as stored (encrypted where applicable).
- Do **not** log backup payload content.
- Validate tenant identity on restore before importing records.
- Default import behavior is additive (no overwrite of existing records).

## Current limitations

This strategy intentionally does **not** include full cloud automation yet (e.g., scheduled object storage uploads, lifecycle policies, or cross-region replication workflows). Use external infrastructure tooling for those concerns until native automation is implemented.
