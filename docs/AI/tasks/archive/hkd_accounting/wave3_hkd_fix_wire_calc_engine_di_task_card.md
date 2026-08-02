# TASK CARD: HKD Book Fix - Wave 3 - Wire Calculation Engine into DI

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Đăng ký 5 service calc engine (ProductionFormulaEngine, ScopedDataProvider, SmartPreAggregationService, TemplateFactory mới, HKDBookGenerationService) vào DI container — để Wave 4 có thể inject `IHKDBookGenerationService` vào `HKDBookService`
- **Nghiệp vụ áp dụng:** DI wiring — block Wave 4 (routing)
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/hkd-fix-wave3-wire-calc-engine-di`
- **Estimated Sessions:** 1

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (IMPLEMENT phase — DI registration only)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 3 of 9
- **Dependency:** Wave 1 (encoding fix — TemplateFactory mới không còn mojibake), Wave 2 (data — có data test)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md` (READ)
- `3_CoreHub/Program.cs` (UPDATE — add 5 DI registrations)
- `5_WebApps/ShopERP/Program.cs` (READ — verify if calc engine needed here too)
- `3_CoreHub/Services/Template/HKDBookGenerationService.cs` (READ — verify constructor deps)
- `3_CoreHub/Services/Template/TemplateFactory.cs` (READ — verify constructor deps)
- `3_CoreHub/Services/Formula/ProductionFormulaEngine.cs` (READ — verify constructor deps)
- `3_CoreHub/Services/Data/ScopedDataProvider.cs` (READ — verify constructor deps)
- `3_CoreHub/Services/PreAggregation/SmartPreAggregationService.cs` (READ — verify constructor deps)
- `3_CoreHub/Services/Cache/IBookResultCache.cs` (READ — verify if registered)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa logic service — chỉ thêm `AddScoped<...>` lines trong Program.cs
- KHÔNG thay đổi constructor signatures
- KHÔNG xóa DI registration cũ (đặc biệt `ITemplateFactory` bản cũ cho OrderService)
- KHÔNG sửa `1_Shared/Domain/*.cs`

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **DI Resolution:** Sau register, tất cả 5 service resolvable từ DI container (verify ở Wave 7 smoke test)
- [ ] **No Conflict:** `ITemplateFactory` (bản cũ) vẫn resolvable cho `OrderService` — không break
- [ ] **Dependency Order:** Register theo dependency order: `IFormulaEngine` → `IDataProvider` → `IPreAggregationService` → `TemplateFactory` → `HKDBookGenerationService`
- [ ] **Cache Registration:** `IBookResultCache` + `IMemoryCache` phải đã đăng ký (verify, nếu chưa thì thêm)
- [ ] **Build Check:** `dotnet build VanAn.sln` Release pass

---

## 5. SUCCESS CRITERIA (ĐO LƯỢNG ĐƯỢC)
- [ ] **SC1:** `ProductionFormulaEngine` registered as `IFormulaEngine` (Scoped)
- [ ] **SC2:** `ScopedDataProvider` registered as `IDataProvider` (Scoped)
- [ ] **SC3:** `SmartPreAggregationService` registered as `IPreAggregationService` (Scoped)
- [ ] **SC4:** `TemplateFactory` (mới, `Services/Template/`) registered as self (Scoped)
- [ ] **SC5:** `HKDBookGenerationService` registered as `IHKDBookGenerationService` (Scoped)
- [ ] **SC6:** `IBookResultCache` confirmed registered (grep — nếu chưa, thêm)
- [ ] **SC7:** `IMemoryCache` confirmed registered (`AddMemoryCache()` — nếu chưa, thêm)
- [ ] **SC8:** `ITemplateFactory` (bản cũ) vẫn resolvable — `OrderService` không break
- [ ] **SC9:** `dotnet build VanAn.sln` Release — 0 errors
- [ ] **SC10:** guard-check.ps1 PASSED

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — Verify DI dependency graph
- `build-error-analysis` — Fix DI resolution error nếu có

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 7
- **Verified Facts:**
  - Fact 1: `ProductionFormulaEngine(IDataProvider, ILogger<>)` — deps: IDataProvider, ILogger
  - Fact 2: `ScopedDataProvider(IPreAggregationService, IBookResultCache, ILogger<>, IMemoryCache)` — deps: 4
  - Fact 3: `SmartPreAggregationService(VanAnDbContext, IFormulaEngine, ILogger<>)` — deps: 3
  - Fact 4: `TemplateFactory` mới `(IFormulaEngine, IDataProvider, ILoggerFactory)` — deps: 3
  - Fact 5: `HKDBookGenerationService(VanAnDbContext, TemplateFactory, IBookResultCache, ILogger<>)` — deps: 4 (lưu ý: cần `TemplateFactory` concrete, không phải interface)
  - Fact 6: `3_CoreHub/Program.cs` L118 đã có `_ = services.AddScoped<ITemplateFactory, TemplateFactory>();` (bản cũ `Services/TemplateFactory.cs`)
  - Fact 7: Bản mới `Services/Template/TemplateFactory.cs` là class khác, không implement `ITemplateFactory`
- **Assumptions:**
  - `IBookResultCache` đã đăng ký (verify)
  - `IMemoryCache` đã đăng ký via `AddMemoryCache()` (verify)
  - `VanAnDbContext` đã đăng ký (confirmed — dùng everywhere)
- **Open Questions:**
  - Q1: `ITemplateFactory` conflict — bản cũ dùng cho OrderService, bản mới dùng cho HKDBookGenerationService. Resolve: giữ bản cũ cho ITemplateFactory, register bản mới as concrete `TemplateFactory` (HKDBookGenerationService inject concrete, không phải interface)
  - Q2: Có cần register ở `5_WebApps/ShopERP/Program.cs` không? (Likely no — CoreHub services dùng in-process)
- **Recommended Action:** PROCEED — risk thấp, chỉ thêm DI registration

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `3_CoreHub/Program.cs` | 5 service mới resolvable — tăng DI container size | No mitigation needed — additive only |
| `OrderService` (dùng ITemplateFactory bản cũ) | Không ảnh hưởng — ITemplateFactory vẫn map bản cũ | Verify OrderService vẫn build |
| `HKDBookGenerationService` (sẽ resolvable) | Sẵn sàng cho Wave 4 inject | No mitigation |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** N/A — DI registration, verify ở Wave 7 smoke test
- **Integration tests:** N/A (Wave 7)
- **E2E tests:** N/A
- **Verification:** `dotnet build VanAn.sln` Release pass

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: Sequential register theo dependency order
1. Verify `IBookResultCache` + `IMemoryCache` đã đăng ký (grep)
2. Register `IFormulaEngine` → `ProductionFormulaEngine`
3. Register `IPreAggregationService` → `SmartPreAggregationService`
4. Register `IDataProvider` → `ScopedDataProvider`
5. Register `TemplateFactory` (mới) as self (concrete, không interface)
6. Register `IHKDBookGenerationService` → `HKDBookGenerationService`
7. Build verify

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Đọc 5 service constructors (verify deps)<br>- Grep `IBookResultCache` + `AddMemoryCache`<br>- Chốt: resolve ITemplateFactory conflict (giữ bản cũ, thêm bản mới as concrete)<br>- Chốt: register ở CoreHub Program.cs hay ShopERP Program.cs | - Add 5 `AddScoped<...>` lines trong `3_CoreHub/Program.cs` (sau L118)<br>- Verify `IBookResultCache` + `IMemoryCache` (thêm nếu thiếu)<br>- Run `dotnet build VanAn.sln` Release<br>- Verify OrderService vẫn build<br>- Commit |

### Rules
- 1 service register tại 1 thời điểm — build verify sau mỗi cái (nếu fail, dễ isolate)
- KHÔNG xóa `ITemplateFactory` bản cũ
- Register theo dependency order (IFormulaEngine trước, HKDBookGenerationService sau)

---

## 11. ESTIMATED EFFORT
- 1 session (5 DI registrations + verify deps + build)
- **BLOCKER:** None — risk thấp, chỉ thêm DI
- **CRITICAL:** Block Wave 4 (cần IHKDBookGenerationService resolvable)

---

## 12. ITemplateFactory Conflict Resolution (from Wave 0 T8 — propagated 2026-07-03)

- **Old TemplateFactory:** `3_CoreHub/Services/TemplateFactory.cs`
  - Implements `ITemplateFactory`: **YES** (L10: `public class TemplateFactory : ITemplateFactory`)
  - Consumers: `OrderService.cs` (L20: `ITemplateFactory? templateFactory = null`, L32: field)
  - DI: `3_CoreHub/Program.cs` L118: `services.AddScoped<ITemplateFactory, TemplateFactory>()`
  - Namespace: `VanAn.CoreHub.Services`
- **New TemplateFactory:** `3_CoreHub/Services/Template/TemplateFactory.cs`
  - Implements `ITemplateFactory`: **NO** — uses primary constructor `(IFormulaEngine, IDataProvider, ILoggerFactory)`, no interface
  - Class name: `TemplateFactory` (same name, different namespace `VanAn.CoreHub.Services.Template`)
  - Creates `S1aHKDTemplateImpl`...`S3aHKDTemplateImpl` via `CreateTemplate(HKDGroup, templateCode)`
- **DECISION: Keep both — register new as concrete `TemplateFactory` (no interface conflict)**
  - Old `ITemplateFactory` → old `Services/TemplateFactory.cs` (for OrderService) — **UNCHANGED, do NOT remove**
  - New `Services/Template/TemplateFactory.cs` → register as `AddScoped<TemplateFactory>()` (concrete, `HKDBookGenerationService` injects concrete, not interface)
  - Different namespaces: `VanAn.CoreHub.Services` (old) vs `VanAn.CoreHub.Services.Template` (new) — no namespace collision
- **Rationale:** New TemplateFactory does NOT implement `ITemplateFactory` → no DI conflict. Both can coexist. No rename needed. No Tech Lead escalation needed.
- **Wave 3 action:** Register new `TemplateFactory` as `AddScoped<TemplateFactory>()` (concrete, NOT `AddScoped<ITemplateFactory, TemplateFactory>`). Keep old `AddScoped<ITemplateFactory, TemplateFactory>()` at L118 unchanged.

---

## 12. ITemplateFactory Conflict Resolution (from Wave 0 T8 — propagated 2026-07-03)

- Old TemplateFactory: `3_CoreHub/Services/TemplateFactory.cs`
  - Implements ITemplateFactory: **YES** (L10: `public class TemplateFactory : ITemplateFactory`)
  - Consumers: `OrderService.cs` (L20: `ITemplateFactory? templateFactory = null`, L32: field)
  - DI: `3_CoreHub/Program.cs` L118: `services.AddScoped<ITemplateFactory, TemplateFactory>()`
- New TemplateFactory: `3_CoreHub/Services/Template/TemplateFactory.cs`
  - Implements ITemplateFactory: **NO** — uses primary constructor `(IFormulaEngine, IDataProvider, ILoggerFactory)`, no interface
  - Class name: `TemplateFactory` (same name, different namespace `VanAn.CoreHub.Services.Template`)
  - Creates `S1aHKDTemplateImpl`...`S3aHKDTemplateImpl` via `CreateTemplate(HKDGroup, templateCode)`
- **DECISION: Keep both — register new as concrete `TemplateFactory` (no interface conflict)**
  - Old `ITemplateFactory` → old `Services/TemplateFactory.cs` (for OrderService) — **UNCHANGED, do NOT remove L118 registration**
  - New `Services/Template/TemplateFactory.cs` → register as `AddScoped<TemplateFactory>()` (concrete, `HKDBookGenerationService` injects concrete)
  - Different namespaces: `VanAn.CoreHub.Services` (old) vs `VanAn.CoreHub.Services.Template` (new) — no namespace collision
- **Rationale:** New TemplateFactory does NOT implement `ITemplateFactory` → no DI conflict. Both can coexist. No rename needed. No Tech Lead escalation needed.
- **Wave 3 implication for SC4:** Register new `TemplateFactory` as **self (concrete)** — `services.AddScoped<TemplateFactory>();` (use full namespace `VanAn.CoreHub.Services.Template.TemplateFactory` if ambiguity). **Do NOT register as `ITemplateFactory`** (would break OrderService).
