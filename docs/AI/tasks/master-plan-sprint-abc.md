# MASTER IMPLEMENTATION PLAN — Sprint A / B / C
# Order & Accounting Data Integrity Improvements

**Created:** 2026-06-20
**Last Updated:** 2026-06-19
**Current Status:** Sprint A ✅ DONE | Sprint B ✅ DONE | Sprint C ✅ DONE (C-1+C-2+C-3) | Sprint D ✅ DONE
**Branch strategy:** 1 branch per sprint, merge to `main` giữa các sprint
**Execution principle:** Sequential sprints, JIT Planning + Pure Execution, verify build trước khi sang sprint kế
**Nguồn phân tích:** `docs/AI/phase-next-order-accounting-improvements.md` (đã đối soát source code thực tế 2026-06-20)

---

## 0. EXECUTION RULES

### Session protocol
1. **Mỗi sprint = 1-2 sessions** (A + B: medium effort ~1 session mỗi sprint; C: low effort ~1 session)
2. **Session bắt đầu:** `load-context` skill → đọc `project_state.md` → đọc task card của sprint
3. **Session kết thúc khi:** Sprint SC pass HOẶC context đầy (whichever first)
4. **Sau mỗi session:** Update `project_state.md` (Section 2 + 3 + 11) + commit
5. **Giữa các sprint:** Verify `dotnet build VanAn.sln --configuration Release` → 0 errors trước khi sang sprint kế

### Branch protocol
```
main
  └── feat/sprint-a-accountcode-fields    (Sprint A)
  └── feat/sprint-b-entry-timing          (Sprint B — sau A merged)
  └── feat/sprint-c-service-guards        (Sprint C — sau B merged)
```
- Sprint A: branch mới `feat/sprint-a-accountcode-fields` từ `main`
- Sprint B: branch mới `feat/sprint-b-entry-timing` từ `main` (sau A merged)
- Sprint C: branch mới `feat/sprint-c-service-guards` từ `main` (sau B merged)

### Hard rules
- CẤM chạy 2 sprint song song trên cùng branch (conflict risk với AccountingEntryService)
- CẤM sang sprint kế nếu sprint hiện tại chưa merge + build pass
- CẤM skip `project_state.md` update sau mỗi session
- CẤM modify `AccountingEntry` entity theo hướng mutable (immutable là bất khả xâm phạm)
- **C-3 (COGS):** CẤM implement trước khi có Tech Lead approval thêm `CostPrice` vào Domain

---

## 1. SPRINT A — Data Fields Wiring (P0, Effort: LOW)

**Branch:** `feat/sprint-a-accountcode-fields`
**Estimated sessions:** 1 session
**Priority:** P0 — Sổ sách đang ghi nhận thiếu thông tin

### Context
`AccountingEntryDto` (`1_Shared/DTOs/AccountingEntryDto.cs`) đã có đầy đủ fields:
`AccountCode`, `Vendor`, `Category`, `Reference`.

**Vấn đề:** Pipeline từ UI → API → Service bị đứt:
- `CreateRevenueEntryRequest` / `CreateExpenseEntryRequest` không có các fields này
- `IAccountingService.CreateRevenueEntryAsync()` / `CreateExpenseEntryAsync()` không nhận `accountCode`
- `AccountingEntryService` không map các fields xuống entity

**Domain constraint:** `AccountingEntry` entity hiện **không có `AccountCode` field** trong `1_Shared/Domain.cs`.
→ **Đây là Domain Modeling Defect.** Cần quyết định: (a) thêm `AccountCode` vào Domain entity (cần approval),
hoặc (b) lưu `AccountCode` chỉ trong DTO/DB không qua Domain (workaround).
→ Task card Sprint A sẽ document constraint này và chọn hướng tiếp cận.

### Tasks
| # | Task ID | Task | Files | Task card | Status |
|---|---|---|---|---|---|
| 1 | A-1 | Wire `AccountCode`: `CreateRevenueEntryRequest` → `IAccountingService` → service | `AccountingEntriesController.cs`, `IAccountingService.cs`, `AccountingEntryService.cs` | `task-sprint-a-accountcode-fields.md §A-1` | ⬜ TODO |
| 2 | A-2 | Wire `Vendor`, `Category`, `Reference`: `CreateExpenseEntryRequest` → service | `AccountingEntriesController.cs`, `AccountingEntryService.cs` | `task-sprint-a-accountcode-fields.md §A-2` | ⬜ TODO |

### Entry criteria
- [ ] Branch `feat/sprint-a-accountcode-fields` created from `main`
- [ ] Task card `task-sprint-a-accountcode-fields.md` đọc kỹ — đặc biệt §4 Domain Constraint
- [ ] Tech Lead approval cho Domain change (nếu chọn hướng (a))

### Exit criteria Sprint A
- [ ] SC1: `CreateRevenueEntryRequest` có `AccountCode` field
- [ ] SC2: `IAccountingService.CreateRevenueEntryAsync()` nhận thêm `accountCode` param
- [ ] SC3: `AccountingEntryService` lưu `AccountCode` vào entry (qua Domain hoặc workaround đã approve)
- [ ] SC4: `CreateExpenseEntryRequest` có `Vendor?`, `Category?`, `Reference?`
- [ ] SC5: `AccountingEntryService.CreateExpenseEntryAsync()` map 3 fields xuống entry/DTO
- [ ] SC6: `RevenueEntry.razor` không thay đổi (đã đọc `accountCode` từ form — chỉ cần API nhận đúng)
- [ ] SC7: `ExpenseEntry.razor` không thay đổi (đã đọc `vendor/category/reference` từ form — chỉ cần API nhận đúng)
- [ ] SC8: `dotnet build VanAn.sln --configuration Release` → 0 errors
- [ ] SC9: `guard-check.ps1` → PASS
- [ ] SC10: `project_state.md` updated + committed
- [ ] SC11: Merge to `main`

### Why first
- Không động đến Order flow, không conflict với Sprint B/C
- Effort thấp nhất trong 3 sprint — warm-up tốt
- Data integrity: mọi manual entry từ giờ sẽ có account code đúng
- Không cần thay đổi UI (Razor forms đã đọc đúng, chỉ cần wiring từ API xuống)

---

## 2. SPRINT B — Accounting Entry Timing (P0, Effort: MEDIUM)

**Branch:** `feat/sprint-b-entry-timing`
**Estimated sessions:** 1-2 sessions
**Priority:** P0 — Doanh thu đang ghi nhận sai thời điểm (trước khi khách thanh toán)
**Depends on:** Sprint A merged (không conflict về file nhưng clean baseline tốt hơn)

### Context
`OrderService.CreateOrderFromCommandAsync()` gọi `GenerateAccountingEntriesAsync(newOrder, tenant)`
**ngay sau khi tạo order** — trước bất kỳ payment confirmation nào.

`WebhookController.cs` hiện chỉ xử lý **e-invoice webhook** (Viettel/MISA, trích xuất `invoiceNo`)
— không có payment webhook, không gọi accounting service.

**Hướng implement đề xuất:**
- Option A: Thêm `POST /api/webhooks/payment` trong `WebhookController.cs` → gọi `GenerateAccountingEntriesAsync` sau payment confirm.
  *Rủi ro:* `WebhookController` hiện có `[Authorize(Policy = "RequireTenantAccess")]` nhưng `[AllowAnonymous]` trên `ReceiveWebhook` — cần thiết kế auth cho payment webhook.
- Option B: Thêm `PaymentStatus` enum (`Pending/Paid`) vào `OrderService`. Entry tạo với status `Pending`, chuyển `Posted` khi webhook xác nhận.
  *Rủi ro:* `AccountingEntry` là immutable — không update status. Cần tạo entry `Posted` mới hoặc thiết kế khác.
- **Recommended:** Option A — thêm payment webhook endpoint riêng, tách hoàn toàn khỏi e-invoice webhook.

### Tasks
| # | Task ID | Task | Files | Task card | Status |
|---|---|---|---|---|---|
| 3 | B-1a | Guard `GenerateAccountingEntriesAsync` trong `OrderService` — không gọi trực tiếp sau create order | `3_CoreHub/Services/OrderService.cs` line 80 | `task-sprint-b-entry-timing.md §B-1a` | ⬜ TODO |
| 4 | B-1b | Thêm `POST /api/webhooks/payment` vào `WebhookController` — nhận payment confirm → gọi accounting | `2_Gateway/Controllers/WebhookController.cs` | `task-sprint-b-entry-timing.md §B-1b` | ⬜ TODO |
| 5 | B-1c | Wire payment webhook → `IOrderService.ConfirmPaymentAsync()` → `GenerateAccountingEntriesAsync` | `3_CoreHub/Services/IOrderService.cs`, `OrderService.cs` | `task-sprint-b-entry-timing.md §B-1c` | ⬜ TODO |

### Entry criteria
- [ ] Sprint A merged to `main`
- [ ] Branch `feat/sprint-b-entry-timing` created from `main`
- [ ] Task card `task-sprint-b-entry-timing.md` đọc kỹ — đặc biệt §7 Open Questions về auth cho payment webhook
- [ ] User approve Option A hoặc Option B trước khi IMPLEMENT

### Exit criteria Sprint B
- [ ] SC1: `OrderService.CreateOrderFromCommandAsync()` không còn gọi `GenerateAccountingEntriesAsync()` unconditionally
- [ ] SC2: `POST /api/webhooks/payment` endpoint tồn tại và nhận payment confirmation payload
- [ ] SC3: Sau payment confirm → `GenerateAccountingEntriesAsync` được gọi với đúng `orderId` + `tenantId`
- [ ] SC4: Không có accounting entry được tạo cho order chưa thanh toán (verifiable qua unit test)
- [ ] SC5: `OrderService.CreateOrderFromCommandAsync()` vẫn tạo order thành công (không regression)
- [ ] SC6: Unit test cho `ConfirmPaymentAsync` → gọi accounting entry generation
- [ ] SC7: Unit test: tạo order không trigger accounting entry
- [ ] SC8: `dotnet build VanAn.sln --configuration Release` → 0 errors
- [ ] SC9: `guard-check.ps1` → PASS
- [ ] SC10: `project_state.md` updated + committed
- [ ] SC11: Merge to `main`

### Why after Sprint A
- Không conflict file với Sprint A
- Sprint A là warm-up — Sprint B có rủi ro cao hơn (touch OrderService + WebhookController)
- Clean baseline (Sprint A merged) đơn giản hóa rollback nếu Sprint B fail

---

## 3. SPRINT C — Service Layer Guards (P2, Effort: LOW-MEDIUM)

**Branch:** `feat/sprint-c-service-guards`
**Estimated sessions:** 1 session (C-1 + C-2); C-3 blocked
**Priority:** P2 — Stability fixes, không phải data corruption nhưng cần trước production
**Depends on:** Sprint B merged

### Context
**C-1 Duplicate detection:** Client-only (`_recentEntries` list trong `RevenueEntry.razor`, `ExpenseEntry.razor`).
Direct API call bypass hoàn toàn check này. `AccountingEntryService` không có server-side logic.

**C-2 Period closing guard:** `AccountingEntryService.CreateRevenue/ExpenseEntryAsync()` không check
`IPeriodClosingService.GetPeriodStatusAsync()`. Kỳ đã đóng vẫn nhận entry mới qua API.
`IPeriodClosingService` đã có `GetPeriodStatusAsync()` — chỉ cần wire vào service.

**C-3 COGS từ CostPrice:** `OrderService.cs:119` dùng `order.TotalPrice * 0.7m`.
`Product` entity trong `Domain.cs` **không có `CostPrice` field** → **Domain Modeling Defect**.
→ **BLOCKED** — cần Tech Lead approval thêm `Product.CostPrice` vào `1_Shared/Domain.cs`.

### Tasks
| # | Task ID | Task | Files | Task card | Status |
|---|---|---|---|---|---|
| 6 | C-1 | Server-side duplicate detection trong `AccountingEntryService` | `3_CoreHub/Services/AccountingEntryService.cs` | `task-sprint-c-service-guards.md §C-1` | ⬜ TODO |
| 7 | C-2 | Period closing guard: check `GetPeriodStatusAsync()` trước khi create entry | `3_CoreHub/Services/AccountingEntryService.cs`, `3_CoreHub/Services/IAccountingService.cs` (inject `IPeriodClosingService`) | `task-sprint-c-service-guards.md §C-2` | ⬜ TODO |
| 8 | C-3 | COGS từ `Product.CostPrice` thay 70% hardcode | `3_CoreHub/Services/OrderService.cs`, `1_Shared/Domain.cs` | `task-sprint-c-service-guards.md §C-3` | ✅ DONE (Sprint D) |

### Entry criteria
- [ ] Sprint B merged to `main`
- [ ] Branch `feat/sprint-c-service-guards` created from `main`
- [ ] C-3: Tech Lead approval cho `Product.CostPrice` Domain change (trước khi implement C-3)

### Exit criteria Sprint C (C-1 + C-2 only — C-3 blocked)
- [ ] SC1: `AccountingEntryService` có duplicate check: `entries.Any(e => e.Amount == amount && e.TransactionDate >= now.AddMinutes(-5) && e.AccountCode == accountCode)` (hoặc tương đương)
- [ ] SC2: Duplicate entry → throw `DuplicateEntryException` với message rõ ràng
- [ ] SC3: `AccountingEntryService` inject `IPeriodClosingService` (hoặc `IAccountingEntryRepository` để check period status)
- [ ] SC4: Create entry vào kỳ đã đóng → throw `InvalidOperationException("Kỳ kế toán đã đóng sổ")`
- [ ] SC5: Unit test: duplicate entry trong 5 phút → exception
- [ ] SC6: Unit test: entry vào kỳ closed → exception
- [ ] SC7: Unit test: entry vào kỳ open → success (không regression)
- [ ] SC8: `dotnet build VanAn.sln --configuration Release` → 0 errors
- [ ] SC9: `guard-check.ps1` → PASS
- [ ] SC10: `project_state.md` updated + committed
- [ ] SC11: Merge to `main`

---

## 4. FILE CONFLICT MATRIX

| File zone | Sprint A | Sprint B | Sprint C | Conflict mitigation |
|---|---|---|---|---|
| `AccountingEntriesController.cs` | ✅ A-1/A-2 (DTOs) | — | — | Sequential A trước B |
| `IAccountingService.cs` | ✅ A-1 (signature) | — | ✅ C-2 (inject) | Sequential A→C |
| `AccountingEntryService.cs` | ✅ A-1/A-2 (map fields) | — | ✅ C-1/C-2 (guards) | Sequential A trước C |
| `OrderService.cs` | — | ✅ B-1a/B-1c (timing) | — | Isolated Sprint B |
| `WebhookController.cs` | — | ✅ B-1b (payment endpoint) | — | Isolated Sprint B |
| `IOrderService.cs` | — | ✅ B-1c (new method) | — | Isolated Sprint B |
| `Domain.cs` | ⚠️ A-1 (nếu chọn Domain change) | — | 🚫 C-3 blocked | Cần approval |
| Test files | ✅ Sprint A tests | ✅ Sprint B tests | ✅ Sprint C tests | Separate test files |

**Điểm conflict quan trọng:**
- `AccountingEntryService.cs` bị cả Sprint A (fields) và Sprint C (guards) đụng → **BẮT BUỘC sequential**
- `IAccountingService.cs` signature thay đổi ở Sprint A → Sprint C cần A merged trước

---

## 5. VISUAL TIMELINE

```
Sprint A (1 session):
  [feat/sprint-a-accountcode-fields]
  A-1: Wire AccountCode → API → Service
  A-2: Wire Vendor/Category/Reference → API → Service
  ──→ Build + Guard pass ──→ Merge to main
          │
Sprint B (1-2 sessions):
  [feat/sprint-b-entry-timing]
  B-1a: Guard GenerateAccountingEntries trong OrderService
  B-1b: Add POST /api/webhooks/payment
  B-1c: Wire payment confirm → accounting entry generation
  ──→ Build + Guard + Unit tests pass ──→ Merge to main
          │
Sprint C (1 session):
  [feat/sprint-c-service-guards]
  C-1: Server-side duplicate detection
  C-2: Period closing guard
  C-3: [BLOCKED] COGS từ CostPrice — await Domain approval
  ──→ Build + Guard + Unit tests pass ──→ Merge to main (C-1/C-2 only)
          │
Sprint D (C-3 unblock, 1 session):
  [feat/sprint-d-cogs-costprice]
  D-1: Add Product.CostPrice to Domain.cs (Tech Lead approved 2026-06-19)
  D-2: EF config for CostPrice in ProductConfiguration.cs
  D-3: Fix OrderService COGS — SUM(qty × CostPrice) with fallback to 70%
  D-4: Reload order with includes in ConfirmPaymentAsync for nav property access
  D-5: Unit tests SC14/SC15/SC16 (23/23 OrderServiceTests PASS)
  ──→ Build 0 errors + Guard PASS + 23 tests pass ──→ Merge to main
```

---

## 6. DOMAIN MODELING DEFECTS (TRACKED)

| # | Defect | Entity | Blocking | Approval needed |
|---|---|---|---|---|
| DMD-1 | `AccountingEntry` không có `AccountCode` field | `AccountingEntry` | Sprint A A-1 (nếu chọn Domain approach) | Tech Lead |
| DMD-2 | `Product` không có `CostPrice` field | `Product` | Sprint D D-1 | ✅ RESOLVED (Tech Lead approved 2026-06-19) |

**Workaround cho DMD-1 (Sprint A):**
- Option X (Recommended): Store `AccountCode` trong `Description` với prefix convention `[511]` — không cần Domain change.
- Option Y: Thêm `AccountCode` vào `AccountingEntry` Domain entity (proper fix, cần approval).
- Task card Sprint A sẽ document quyết định này sau khi User approve.

---

## 7. SESSION CHECKLIST (cho mỗi session)

### Before session start
- [ ] `load-context` skill → đọc `project_state.md`
- [ ] Đọc master plan này → xác định sprint hiện tại
- [ ] Đọc task card của sprint hiện tại
- [ ] Verify branch đúng (`git branch`)
- [ ] Verify sprint trước đã merged (`git log --oneline main | head -5`)

### During session
- [ ] JIT Planning: đọc boundary files 1 lần, chốt file cần sửa/tạo
- [ ] Pure Execution: viết code, không re-explore
- [ ] Run build sau mỗi task (A-1, A-2 riêng lẻ)
- [ ] Viết unit test trước implement (TDD cho features mới)

### Before session end
- [ ] Sprint SC pass HOẶC context gần đầy
- [ ] `dotnet build VanAn.sln --configuration Release` → 0 errors
- [ ] `guard-check.ps1` → PASS
- [ ] Update `project_state.md` (Section 2 + 3 + 11)
- [ ] Commit: `feat(sprint-a|b|c): <description>`
- [ ] Nếu sprint hoàn tất: merge to `main` + verify build trên `main`

---

## 8. ROLLBACK PLAN

Nếu sprint fail:
1. **STOP** — không cố fix tiếp
2. `git stash` uncommitted changes
3. `git checkout main` — về baseline sạch
4. Document failure trong `project_state.md` Section 7 (Known Risks)
5. Tạo task card retry với approach khác
6. Không sang sprint kế cho đến khi sprint hiện tại resolve

---

## 9. REFERENCES

- Task cards: `task-sprint-a-accountcode-fields.md`, `task-sprint-b-entry-timing.md`, `task-sprint-c-service-guards.md`
- Phân tích nguồn: `docs/AI/phase-next-order-accounting-improvements.md`
- Project state: `docs/AI/project_state.md` (Section 2 current objective)
- Domain entity: `1_Shared/Domain.cs` (AccountingEntry, Product)
- DTO: `1_Shared/DTOs/AccountingEntryDto.cs` (đã có AccountCode, Vendor, Category, Reference)
- Service interface: `3_CoreHub/Services/IAccountingService.cs`
- Service impl: `3_CoreHub/Services/AccountingEntryService.cs`
- Period closing: `3_CoreHub/Services/IPeriodClosingService.cs`
- Order service: `3_CoreHub/Services/OrderService.cs`
- Webhook: `2_Gateway/Controllers/WebhookController.cs`
- API controller: `2_Gateway/Controllers/AccountingEntriesController.cs`
