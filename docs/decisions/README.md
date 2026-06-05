# Architecture Decision Records (ADRs)

> **ShopERP Architecture Decisions**  
> Mọi quyết định kiến trúc quan trọng đều được ghi lại tại đây.

## ADR Index

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [ADR-001](./ADR-001-SQLite-Offline-First.md) | SQLite + NATS Offline First | Approved | 2026-06-01 |
| [ADR-002](./ADR-002-Multi-Tenancy-Everywhere.md) | Multi-Tenancy Everywhere | Approved | 2026-06-01 |
| [ADR-003](./ADR-003-Accounting-Immutability.md) | Accounting Immutability | Approved | 2026-06-01 |
| [ADR-004](./ADR-004-UI-Platform-Mandatory.md) | UI Platform Mandatory | Approved | 2026-06-01 |
| [ADR-005](./ADR-005-Playwright-Isolation.md) | Playwright Isolation | Approved | 2026-06-01 |

## Status Legend

- **Proposed**: Đang thảo luận, chưa quyết định
- **Approved**: Đã được approve, đang triển khai
- **Deprecated**: Thay thế bởi ADR mới
- **Superseded**: Xem ADR mới để biết quyết định hiện tại

## Quick Reference

| Decision | ADR | Key Constraint |
|----------|-----|----------------|
| Offline sync | ADR-001 | SQLite local → NATS → PostgreSQL |
| Tenant isolation | ADR-002 | `TenantId` required on all entities |
| Financial data | ADR-003 | Append-only, no edit, only reversal |
| UI development | ADR-004 | Use `VanAn.UI.Platform`, no custom CSS |
| E2E testing | ADR-005 | Disabled during IMPLEMENT mode |

---

## How to Create New ADR

1. Copy `ADR-Template.md`
2. Đặt tên: `ADR-XXX-short-description.md`
3. Update `README.md` index
4. Submit PR với label `adr`

## References

- `.windsurf/rules/.windsurfrules` - Core governance
- `docs/knowledge-base/03-architecture/` - Architecture docs
