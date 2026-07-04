# MASTER PLAN — VAS Enterprise Financial Reports (TT 99/2025 + TT 133/2016 + TT 58/2026)

> **Status:** ✅ W0 COMPLETE & MERGED — 10 waves (W0→W9), W1 next
> **Created:** 2026-07-04 · **Last Updated:** 2026-07-04 (W0 merged `be348ad` → main)
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT) · **Branch:** per-wave feature branch, always-green main
> **Prerequisite audits:** Section 1 (4 BCTC) + Section 1.4 (Order→Accounting flow)

---

## 0. JIT PLANNING STRATEGY (NON-NEGOTIABLE)

**Nguyên tắc cốt lõi:** KHÔNG code mò mẫm — **Investigate trước, Implement sau**. Áp dụng cho mỗi wave.

### 3-Phase per wave
```
Phase 1 (INVESTIGATE): Đọc task card wave + verify codebase hiện tại
  → Confirm file paths, signatures, dependencies vẫn đúng
  → Grep usage của methods/symbols sẽ touch
  → Identify blast radius (ai gọi method này?)
  → Output: confirm task card vẫn accurate, hoặc flag drift

Phase 2 (PLAN): Detail coding plan
  → Liệt kê exact changes (file:line, old→new)
  → Identify test files cần update
  → Identify DI registrations cần thêm
  → Output: checklist implement

Phase 3 (IMPLEMENT): Code + verify
  → Apply changes theo checklist
  → Build + guard + tests pass
  → Commit
```

### Task Card Protocol
- **Mỗi wave có 1 task card** tại `docs/AI/tasks/vas_wave{N}_task_card.md`
- Task card chứa: objective, prerequisites, exact file changes, code snippets, verification, rollback
- **Task card phải được đọc TRƯỚC khi code** (Phase 1)
- **Task card có thể update** nếu INVESTIGATE phát hiện drift (không phải contract immutable)
- **Task card KHÔNG thay thế master plan** — master plan là chiến lược, task card là chiến thuật

### Anti-Guessing Gate (Gate 1 từ .windsurfrules)
- Assumptions ≥ Verified Facts → CẤM code, chuyển Investigate
- Mỗi wave phải có ≥ 3 verified facts trước khi implement:
  1. File path tồn tại (verify bằng read/glob)
  2. Method signature đúng (verify bằng grep)
  3. Dependency chain đúng (verify bằng trace)

---

## 1. EXECUTION RULES

### Dependency chain
```
W0 (Writer Fix) → W1 (Seed) → W2 (Domain) → W3 (Account Map) → W4 (4 Services) → W5 (API) → W6 (UI) → W7 (Tests) → W8 (Feature Flag) → W9 (Regression)
```
- W4 là wave duy nhất song song 4 services (BS+IS+CF+TB)
- Mọi wave khác tuần tự nghiêm ngặt
- Mỗi wave xong: `dotnet build VanAn.sln` Release pass + `guard-check.ps1` pass + commit

### Session protocol
1. Mỗi session làm 1 wave (W4 có thể 2-3 session)
2. Bắt đầu session: đọc `project_state.md` + task card wave
3. Trước session end: build pass + commit
4. Commit format: `[VAS WAVE X] <short description>`

### Branch protocol
```
main ← feature/vas-wave0-order-accounting-writer-fix
main ← feature/vas-wave1-data-audit-seed
main ← feature/vas-wave2-domain-records
main ← feature/vas-wave3-account-code-map
main ← feature/vas-wave4-services-bs-is-cf-tb
main ← feature/vas-wave5-api-endpoints
main ← feature/vas-wave6-ui-pages
main ← feature/vas-wave7-numeric-tests
main ← feature/vas-wave8-feature-flag-tenanttype
main ← feature/vas-wave9-regression-merge
```

---

## 2. AUDIT FINDINGS SUMMARY

### 2.1. Legal Framework
| Văn bản | Thay thế | Đối tượng | Hiệu lực |
|---------|----------|-----------|---------|
| TT 99/2025/TT-BTC | TT 200/2014 + amendments | DN lớn (bắt buộc) + DN vừa/nhỏ (tùy chọn) | 01/01/2026 |
| TT 133/2016 | (vẫn hiệu lực) | DN vừa và nhỏ | đang áp dụng |
| TT 58/2026/TT-BTC | TT 132/2018 | DN siêu nhỏ | 2026 |

### 2.2. 4 BCTC Current State
| Report | Status | Issue chính |
|--------|--------|-------------|
| Balance Sheet | ❌ STUB rỗng | `TemplateFactory.GenerateBalanceSheetAsync` chỉ `await Task.CompletedTask` |
| Income Statement | ❌ MOCK hardcoded | 120M/70M/50M fixed, ignore tenant/period |
| Cash Flow Statement | ❌ MOCK hardcoded | 150M/80M/70M fixed, no opening balance |
| Trial Balance | ⚠️ Logic có, query BROKEN | Pattern #1 (`EF.Property<Guid>`) + #5 (`e.Period.Year`) → runtime fail → empty |

### 2.3. Data Layer Blockers
| # | Blocker | Severity | Wave fix |
|---|---------|----------|----------|
| B1 | JournalEntries table empty (0 rows) | 🔴 | W1 seed |
| B2 | Zero opening balance handling | 🔴 | W1+W2+W4 |
| B3 | AccountCode "621" vs "632" inconsistency | 🟡 | W0 (absorbed) |
| B4 | AccountingEntry single-entry SSoT | 🟡 | W4 (query JournalEntries) |
| B5 | Trial Balance query Pattern #1 + #5 violations | 🔴 | W4 rewrite |
| B6 | Không có AccountCode mapping table | 🟡 | W3 |

### 2.4. Order→Accounting Data Flow (18 vấn đề)
- **3 Critical (C1-C3):** Dual-write COGS mismatch, PaymentMethod lost, VAT không ghi
- **5 High (H1-H5):** Cash hardcode 111, Discount/Shipping bỏ qua, Period UtcNow, no Order ref
- **10 Medium (M1-M10):** COGS TK theo Category, OrderType, CustomerId, AR 131, VAT input 1331, reversal, COGS fallback, JE↔AE link, S2d sai, multi-tenancy query
- **Chi tiết:** Section 1.4 trong git history (commit v2) — hoặc xem `vas_wave0_task_card.md`

---

## 3. SCOPE DECISIONS (APPROVED 2026-07-04)

| # | Quyết định | Lựa chọn |
|---|-------------|----------|
| D1 | Tầng chuẩn kế toán | Cả 3: TT 99 + TT 133 + TT 58 |
| D2 | HKD共存 | VAS = module riêng (feature flag) |
| D3 | Thứ tự 4 BCTC | Cả 4 song song (W4) |
| D4 | Data audit | Verify trước (W0 fix → W1 seed) |
| D5 | Domain modification | Approved W2 |
| D6 | Seed data | Sample DN vừa (TT 133), ~20 entries |
| D7 | Order→Acc writer fix | W0 trước seed |
| D8 | JIT Planning | Investigate trước, Implement sau (mỗi wave) |
| D9 | HKD↔DN conversion | Option B (New Tenant + Link) · Read-only historical qua predecessor · Amend W2+W3+W8 (no new wave) |

---

## 4. WAVE OVERVIEW (10 waves)

| Wave | Tên | Mode | Domain? | Task Card | Status |
|------|-----|------|---------|-----------|--------|
| W0 | Order→Accounting Writer Fix | IMPLEMENT | ❌ | `vas_wave0_task_card.md` | ✅ DONE — merged `be348ad` (9/18 issues: C1-C3, H1-H2, H4-H5, M9, B3) |
| W1 | Data Audit + Seed | IMPLEMENT | ❌ | `vas_wave1_task_card.md` | ⏳ NEXT |
| W2 | Domain Records | IMPLEMENT | ✅ (D5) | `vas_wave2_task_card.md` | ⏳ |
| W3 | Account Code Map | IMPLEMENT | ❌ | `vas_wave3_task_card.md` | ⏳ |
| W4 | 4 Report Services (parallel) | IMPLEMENT | ❌ | `vas_wave4_task_card.md` | ⏳ |
| W5 | API Endpoints | IMPLEMENT | ❌ | `vas_wave5_task_card.md` | ⏳ |
| W6 | UI Pages | IMPLEMENT | ❌ | `vas_wave6_task_card.md` | ⏳ |
| W7 | Tests with Numeric Assertions | IMPLEMENT | ❌ | `vas_wave7_task_card.md` | ⏳ |
| W8 | Feature Flag + TenantType | IMPLEMENT | ❌ | `vas_wave8_task_card.md` | ⏳ |
| W9 | Regression + Merge | REVIEW | ❌ | `vas_wave9_task_card.md` | ⏳ |

**Chi tiết từng wave:** xem task card tương ứng. Master plan chỉ giữ overview.

---

## 5. RISK REGISTER

| # | Risk | Mitigation | Wave |
|---|------|------------|------|
| R1 | TT 99/2025 phụ lục TK chưa có trong codebase | W3 search web hoặc tạm dùng TK TT 200 + TODO | W3 |
| R2 | Opening balance accumulate phức tạp | W4 bắt đầu opening=0, thêm accumulate sau | W4 |
| R3 | 3 standards = ×3 effort W3 | Ưu tiên TT 133 trước, TT 99/58 thêm sau | W3 |
| R4 | Cash Flow indirect method phức tạp | W4-CF bắt đầu direct method, indirect sau | W4 |
| R5 | JournalEntries empty → services empty | W1 seed bắt buộc trước W4 | W1→W4 |
| R6 | Domain modification break architecture tests | W2-T4 verify arch tests pass | W2 |
| R7 | W0 fix break existing OrderServiceTests | W0-V1 verify existing tests + add new assertions | W0 |
| R8 | W0 VAT tách thay đổi revenue calculations | Audit grep `TotalPrice` usage trước fix | W0 |
| R9 | W0 PaymentMethod magic string | Define `PaymentMethodConstants` | W0 |
| R10 | W0 discount/shipping accounting treatment | Net vs gross — quyết định trong W0, document lý do | W0 |
| R11 | W0 COGS Path A/B sync cần refactor | Extract `CalculateCogsAmount(Order)` shared method | W0 |
| R12 | HKD→DN conversion: opening balance mapping HKD single-entry → DN double-entry | W3 thêm HKD→DN account mapping · W1 seed 1 conversion scenario · W8 conversion service | W3+W1+W8 |
| R13 | Historical HKD reports read-only sau conversion | W8 thêm PredecessorTenantId link + read-only gating | W8 |

---

## 6. SUCCESS CRITERIA

**W0 (Writer Fix):** ✅ COMPLETE — merged `be348ad` (2026-07-04)
- ✅ Order confirm payment tạo JE có VAT tách (511 net + 3331) — both AccountingEntry + JournalEntry
- ✅ PaymentMethod truyền đúng → 111 vs 112 map đúng (PaymentMethodConstants class)
- ✅ COGS Path A == Path B (shared `CalculateCogsAmount(Order)`)
- ✅ Period dùng OrderDate, không UtcNow
- ✅ Existing OrderServiceTests pass (no regression) — 828/828 Core.Tests, 31/31 Arch.Tests
- ✅ AccountCode 632 (not 621), Discount net revenue, Order reference, COGS removed from S2d
- ⏸ Deferred (per user decision 2026-07-04): H3 Shipping (pending BA/Kế toán), M1/M6/M2-M5/M7/M8/M10

**W1-W9 (4 BCTC):**
- ✅ 4 BCTC render đúng số liệu từ seed (không mock)
- ✅ Multi-tenant: tenant A không leak tenant B
- ✅ HKD reports (S1a-S3a) vẫn hoạt động
- ✅ Feature flag gating: HKD tenant không truy cập VAS
- ✅ Build 0 errors, guard pass, all tests pass
- ✅ Numeric test assertions (không white-test)
- ✅ UI Platform components (không custom HTML/CSS)
- ✅ Pattern #1 + #5 compliance (no `EF.Property<Guid>`, no `e.Period.Year`)

---

## 7. REFERENCES

- **Legal:** TT 99/2025/TT-BTC (congbao.chinhphu.vn), TT 133/2016, TT 58/2026
- **HKD stream:** `docs/AI/tasks/hkd_book_accounting_fix_master_plan.md`
- **Governance:** `.devin/rules/governance.md`
- **Workflow:** `.devin/workflows/newfeaturebuild.md`
- **UI Platform:** `docs/UI_Platform_Implementation_Guide.md`
- **Task cards:** `docs/AI/tasks/vas_wave{0-9}_task_card.md`
