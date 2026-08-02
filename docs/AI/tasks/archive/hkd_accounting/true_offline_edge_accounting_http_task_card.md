# TASK CARD: True Offline Edge — Accounting via Gateway HTTP API

## 1. GOAL & CONTEXT

- **Mục tiêu cốt lõi:** Khi ShopERP Edge node chạy KHÔNG có PostgreSQL local (true 2-server architecture), accounting queries phải đi qua Gateway HTTP API thay vì direct `IAccountingDbContext` (PostgreSQL)
- **Nghiệp vụ áp dụng:** ADR-001 compliance — accounting always online trên PostgreSQL (Central server), Edge node offline-capable cho business (SQLite)
- **Status:** ⏳ PENDING — Debt recorded, not yet planned for implementation
- **Trigger:** Khi triển khai true 2-server Edge mode (ShopERP trên Server A, PostgreSQL + Gateway trên Server B)
- **Priority:** Thấp — hiện tại cả 3 compose files đều chạy PostgreSQL trên cùng máy

---

## 2. WHY THIS DEBT EXISTS

### Background
Wave 1 (commit `9d589bd`) đã split `IVanAnDbContext` → `IAccountingDbContext` để enforce ADR-001 (accounting on PostgreSQL). ShopERP `Program.cs` luôn đăng ký `VanAnDbContext` với `UseNpgsql` — không có conditional logic.

### Current state (verified 2026-07-09)
- `docker-compose.yml` (dev): PostgreSQL + ShopERP cùng máy → OK
- `docker-compose.prod.yml` (SaaS): PostgreSQL + ShopERP cùng VPS → OK
- `docker-compose.edge.yml` (edge): PostgreSQL + ShopERP cùng máy, `shoperp.depends_on: postgres: condition: service_healthy` → OK

### When debt triggers
Khi `docker-compose.edge.yml` được tách thành 2 server thật:
- **Server A (Edge):** ShopERP + SQLite + NATS sync worker — KHÔNG có PostgreSQL
- **Server B (Central):** Gateway (in-process CoreHub) + PostgreSQL + KhachLink

Lúc đó ShopERP không thể kết nối PostgreSQL trực tiếp → `IAccountingDbContext` (UseNpgsql) sẽ throw khi resolve.

---

## 3. PROPOSED SOLUTION — Option C (Gateway HTTP API)

### Architecture
```
=== True Offline Edge (2 servers) ===

Server A (Edge — no PostgreSQL):         Server B (Central — PostgreSQL):
  ShopERP → SQLite (business)              Gateway → PostgreSQL (accounting)
  ShopERP → HTTP → Gateway (accounting)    [in-process CoreHub services]
       ↓ NATS Outbox sync ↓
  ───────────────→ NATS ───────────────→ Gateway
```

### Implementation approach
1. **Tạo `AccountingHttpService : IAccountingDbContext`** — implementation mới gọi Gateway HTTP API thay vì direct DbContext
2. **Conditional DI trong ShopERP Program.cs:**
   ```csharp
   if (bool.Parse(builder.Configuration["EdgeMode:TrueOffline"] ?? "false"))
   {
       // True offline edge: accounting via HTTP
       builder.Services.AddScoped<IAccountingDbContext, AccountingHttpService>();
   }
   else
   {
       // SaaS / Edge-with-PostgreSQL: accounting via direct DbContext
       builder.Services.AddDbContext<VanAnDbContext>(options => options.UseNpgsql(...));
       builder.Services.AddScoped<IAccountingDbContext>(sp => sp.GetRequiredService<VanAnDbContext>());
   }
   ```
3. **Gateway HTTP endpoints:** Expose accounting query endpoints (JournalEntries, AccountingEntries, AccountCharts, PeriodClosingStatuses, AuditLogs, PendingInvoiceQueues) — Gateway đã có in-process CoreHub services + PostgreSQL
4. **Feature flag disable accounting UI** khi offline (ShopERP không reach Gateway) — KHÔNG trả empty data, mà disable UI + show "Accounting unavailable — connect to central server"

### What NOT to do (rejected approaches)
- ❌ **Throw stub / null IAccountingDbContext** — Option A rejected (master plan v2)
- ❌ **Service Locator (IServiceProvider)** — anti-pattern, phá testability
- ❌ **Graceful degradation (return empty data)** — vi phạm TT 152/2025/TT-BTC (báo cáo sai)
- ❌ **Conditional DI without HTTP fallback** — accounting services không resolve được → crash

---

## 4. IMPACT ANALYSIS (RESERVE — ghi nhận để khi implement lưu ý)

### 4.1. Files cần tạo mới
| File | Mục đích |
|------|----------|
| `3_CoreHub/Services/Http/AccountingHttpService.cs` | `IAccountingDbContext` implementation qua HTTP |
| `3_CoreHub/Services/Http/IAccountingHttpApi.cs` | Interface cho Gateway HTTP endpoints (Refit hoặc HttpClient) |
| `2_Gateway/Controllers/AccountingQueryController.cs` | HTTP endpoints expose accounting queries |
| `5_WebApps/ShopERP/appsettings.EdgeOffline.json` | Config cho true offline edge mode |

### 4.2. Files cần sửa
| File | Thay đổi | Rủi ro |
|------|----------|--------|
| `5_WebApps/ShopERP/Program.cs` | Conditional DI (EdgeMode:TrueOffline flag) | Thấp — additive, không phá SaaS path |
| `docker-compose.edge.yml` | Tách thành 2 compose files hoặc dùng profile | Trung bình — cần test cả 2 mode |
| `5_WebApps/ShopERP/Components/Pages/Accounting/*.razor` | Disable UI khi accounting unavailable | Trung bình — cần feature flag check |

### 4.3. Services KHÔNG cần đổi (key insight)
14 services/repos đã swap sang `IAccountingDbContext` trong Wave 1 **KHÔNG cần sửa** — chúng inject `IAccountingDbContext` (interface), không quan tâm implementation là `VanAnDbContext` (direct) hay `AccountingHttpService` (HTTP). Đây là giá trị của interface segregation (Option B).

### 4.4. Test impact
| Test type | Impact | Mitigation |
|-----------|--------|------------|
| Unit tests (Core.Tests) | Không ảnh hưởng — dùng `VanAnDbContext` concrete (implements cả 2 interfaces) | None |
| Integration tests | Cần thêm test cho `AccountingHttpService` | Tạo mock Gateway HTTP |
| Architecture tests (Wave 3 Rule J/K/L/M) | Rule M check `UseNpgsql` — cần update để allow HTTP path | Add exception for `EdgeMode:TrueOffline` |

### 4.5. Performance considerations
| Operation | Direct DbContext (SaaS) | HTTP via Gateway (Edge) |
|-----------|------------------------|------------------------|
| Query JournalEntries | ~5ms (local PostgreSQL) | ~50-100ms (HTTP + PostgreSQL) |
| Query AccountingEntries (large) | ~10ms | ~100-500ms + serialization overhead |
| SaveChangesAsync | ~5ms | N/A — Edge không write accounting trực tiếp (sync qua NATS) |

**Mitigation:** Caching layer trong `AccountingHttpService` (IMemoryCache) cho read-heavy queries. Write operations đi qua NATS Outbox (đã có infrastructure).

### 4.6. Regulatory compliance
| Requirement | How satisfied |
|-------------|---------------|
| ADR-001: accounting always online | ✅ PostgreSQL trên Central server, luôn online |
| ADR-003: AccountingEntry immutable | ✅ Gateway enforce immutability, Edge không write trực tiếp |
| TT 152/2025/TT-BTC | ✅ Báo cáo luôn từ PostgreSQL (qua HTTP), không bao giờ từ SQLite |
| Offline-capable | ✅ Business (orders, inventory) trên SQLite, accounting qua HTTP khi online |

### 4.7. Dependencies / Prerequisites
- Gateway phải expose accounting query HTTP endpoints (hiện chưa có — Gateway chỉ có CoreHub services in-process)
- `AccountingHttpService` cần `HttpClient` configured với Gateway base URL
- NATS sync worker đã có (docker-compose.edge.yml `shoperp-nats-sync`) — write path đã ready
- Feature flag infrastructure (hiện chưa có — cần thêm `EdgeMode:TrueOffline` config)

---

## 5. ESTIMATED EFFORT

| Phase | Description | Sessions | Dependency |
|-------|-------------|----------|------------|
| Phase 1 | Gateway accounting HTTP endpoints | 2-3 | None |
| Phase 2 | AccountingHttpService + conditional DI | 1-2 | Phase 1 |
| Phase 3 | UI feature flag + disable accounting when offline | 1 | Phase 2 |
| Phase 4 | Tests + verify both modes | 1-2 | Phase 3 |
| **Total** | | **5-8 sessions** | |

**Priority:** Thấp — hiện tại không có deployment true 2-server. Trigger khi có khách hàng cần edge node không có PostgreSQL.

---

## 6. ANTI-PATTERNS TO AVOID (reference)

| # | Anti-pattern | Tại sao sai | Nguồn |
|---|-------------|-------------|-------|
| 1 | Throw stub `IAccountingDbContext` | Option A rejected — crash runtime, không compile-time safety | Master plan v2 |
| 2 | Service Locator (`IServiceProvider`) | Ẩn dependencies, phá testability, scope overhead | DI best practices |
| 3 | Graceful degradation (return empty) | Vi phạm TT 152 — báo cáo tài chính sai = rủi ro pháp lý | ADR-003 |
| 4 | Null check boilerplate × 55 methods | Over-engineering cho kịch bản không tồn tại | Governance "Simple & Idiomatic" |
| 5 | `AddDbContext<IAccountingDbContext, VanAnDbContext>` | Phá pattern consistency, không expose concrete type | Codebase convention |

---

## 7. REFERENCES

- Master plan: `docs/AI/tasks/accounting_postgresql_online_master_plan.md`
- Wave 1 task card: `docs/AI/tasks/accounting_pg_wave1_interface_split_task_card.md`
- Wave 2 task card: `docs/AI/tasks/accounting_pg_wave2_services_di_config_task_card.md`
- Deployment modes: `docs/AI/project_state.md` §5a
- ADR-001: SQLite local + NATS sync + PostgreSQL cloud (accounting always online)
- ADR-003: AccountingEntry immutable, TT 200/2014/TT-BTC + TT 152/2025/TT-BTC compliance
- Debt ledger: `5_WebApps/ShopERP/TECHNICAL_DEBT_LEDGER.md` (Tier 5)
