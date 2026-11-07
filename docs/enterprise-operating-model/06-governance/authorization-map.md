# Authorization Map — P4

> Roles × permissions × tenant scope. Evidence: `UserRole.cs`, controllers, feature flags @`64789992`.

## Tenant-scope roles (per merchant — UserRole enum)

| Role | Access (from enum comments) | Scope |
|---|---|---|
| Owner (ACTR-002) | Full access | own tenant |
| StoreKeeper (003) | inventory | own tenant |
| Staff (004) | order management | own tenant |
| Masterchef (005) | kitchen ops | own tenant |
| ChiefAccountant (006) | inventory+accounting+finance full | own tenant |
| DebtAccountant (007) | accounting only — no history/import/close/HKD-books | own tenant, restricted |
| Guard (008) | check-in/out | own tenant |

## Platform-scope actors

| Actor | Access | Scope |
|---|---|---|
| Vạn An Ecommerce op (ACTR-014) | tenant admin, approvals, community admin | cross-tenant (admin controllers) |
| Vạn An Tech op (ACTR-015) | system admin, reconcile, AI knowledge ops | cross-tenant |
| vanan-metering (ACTR-023) | wallet/quota API | all tenants (caller-scoped) |
| vanan-ai (ACTR-030) | tenant knowledge only — signed ctx | **single-tenant per request** |

## Public/anonymous surfaces

`/api/public/*` (orders, booking, tenant-ai widget, metering public) — anonymous + rate-limited, tenant resolved server-side (never client-supplied tenant trust — CTRL-005 pattern).

## Cross-VPS internal

`X-Internal-Api-Key` (CTRL-006) — internal endpoints only (reconcile, mirror reads).

## Mechanisms → CTRL registry

TenantId filter (CTRL-002) · JWT (API-112) · metering keys (CTRL-003) · AI signed ctx (CTRL-005) · DevToken gate (CTRL-007/008) · AccountingEntry immutability (CTRL-001).
