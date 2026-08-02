# TASK CARD: [FIX] - Payment Webhook 500 — JournalEntry Duplicate Key Root Cause

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Fix bug 500 trên `POST /api/webhooks/payment` do `JournalEntry` PK duplicate khi `GenerateAccountingEntriesAsync` thêm cùng một entity instance vào 2 HKD book types (S2b + S2c).
- **Nghiệp vụ áp dụng:** Payment webhook là endpoint xác nhận thanh toán cho KhachLink customer orders — bắt buộc trả 200 (idempotent) để customer thấy "Paid". Hiện E2E test `khachlink-full-order-flow-prod.spec.ts` đang `expect([200, 400, 500])` để lách bug — phải khôi phục `expect(200)` sau khi fix.
- **Lý do chưa fix sớm:** `ConfirmPaymentAsync` đã wrap `GenerateAccountingEntriesAsync` trong try-catch (commit `5ba95213`, line 633-640) → order vẫn mark Paid nhưng accounting entries bị thiếu → silent data inconsistency trong sổ cái kế toán. Bug ẩn, không crash UI, nhưng ghi sai sổ = vi phạm TT 152/2025/TT-BTC.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/Fix_Errors.md`
- **Execution Mode:** FIX_ONLY (fix bug gốc, không mở rộng scope, không refactor architecture)

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `3_CoreHub/Services/OrderService.cs` (root cause — `GenerateAccountingEntriesAsync` line 112-196, `CreateRevenueEntryAsync` line 221-252, `CreateCOGSEntryAsync` line 258-284)
  - `3_CoreHub/Repositories/HKDBookRepository.cs` (`AddToBookAsync` line 135-154 — fix candidate)
  - `3_CoreHub/Repositories/IHKDBookRepository.cs` (interface contract — có thể cần update)
  - `6_Testing/e2e-tests/khachlink-full-order-flow-prod.spec.ts` (line 95-111 — revert `expect([200,400,500])` → `expect(200)` sau khi fix)
  - `6_Tests/VanAn.Core.Tests/` (add unit test cho `GenerateAccountingEntriesAsync` không throw khi thêm 2 book types)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa `1_Shared/Domain/JournalEntry.cs` (Domain immutable rule, Fix_ONLY mode cấm)
  - KHÔNG refactor `AddToBookAsync` thành "proper HKD book mapping table" — bug fix only, không mở rộng scope
  - KHÔNG thay đổi `ConfirmPaymentAsync` controller signature / public API
  - KHÔNG bỏ try-catch xung quanh `GenerateAccountingEntriesAsync` (đây là defense-in-depth, giữ lại sau khi fix root cause)

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Domain Immutability:** `JournalEntry` là `sealed partial class : BaseEntity, IMustHaveTenant` — không sửa. `JournalEntryId = new(Guid.NewGuid())` ở constructor line 56 — mỗi instance có PK unique, duplicate chỉ xảy ra khi SAME instance được AddAsync 2 lần.
- [ ] **Idempotency Webhook:** `POST /api/webhooks/payment` phải idempotent — duplicate request cho cùng `orderId` trả 200, không tạo duplicate entries. Hiện controller KHÔNG check idempotency cho payment endpoint (chỉ có cho `{provider}` endpoint qua `IWebhookService.HasBeenProcessedAsync`).
- [ ] **AccountingEntry Immutable Rule (governance.md):** Không áp dụng cho `JournalEntry` (chỉ áp dụng cho `AccountingEntry` aggregate). Nhưng `JournalEntry` vẫn immutable về mặt domain — không mutate sau khi tạo.
- [ ] **EF Core Tracking:** Khi `AddAsync(entity)` được gọi lần 2 cho entity đã tracked, EF Core throw `InvalidOperationException: The instance of entity type 'JournalEntry' cannot be tracked because another instance with the same key value is already being tracked`.
- [ ] **TT 152/2025/TT-BTC:** Doanh thu phải ghi nhận ĐÚNG khi thanh toán xác nhận. Silent failure trong accounting generation = sổ cái thiếu entry = vi phạm compliance.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC_1:** `POST /api/webhooks/payment` với order hợp lệ + `accountingEnabled=true` trả **HTTP 200** (không 500).
- [ ] **SC_2:** Cùng `JournalEntry` instance thêm vào 2 book types (S2b_HKD + S2c_HKD) không throw exception.
- [ ] **SC_3:** DB có ĐÚNG 1 row `JournalEntries` cho revenue entry đó (không duplicate, không thiếu).
- [ ] **SC_4:** DB có ĐÚNG 1 row `JournalEntries` cho COGS entry (nếu `cogsAmount > 0`).
- [ ] **SC_5:** Webhook idempotency: gọi 2 lần liên tiếp với cùng `OrderId` + `TransactionId` → lần 2 trả 200, KHÔNG tạo thêm entry mới.
- [ ] **SC_6:** E2E test `khachlink-full-order-flow-prod.spec.ts` step 2 được revert thành `expect(200)` và PASS trên cả local + VPS.
- [ ] **SC_7:** Unit test mới: `GenerateAccountingEntriesAsync_WithTwoBookTypes_DoesNotThrowDuplicateKey` PASS.
- [ ] **SC_8:** Unit test mới: `ConfirmPaymentAsync_Idempotent_DuplicateRequestReturns200NoExtraEntries` PASS.
- [ ] **SC_9:** `dotnet build VanAn.sln` PASS, `guard-check.ps1` PASS.
- [ ] **SC_10:** `dotnet test VanAn.Core.Tests` PASS (990+2 tests).
- [ ] **SC_11:** Manual test trên VPS khachvip.online: webhook trả 200, kiểm tra PostgreSQL có đủ revenue + COGS entries.
- [ ] **SC_12:** Update `docs/testing/manual-test-vps-guide.md` line 410 + 455 — xóa ghi chú "500 chấp nhận được".

**Implementation Date:** TBD
**Branch:** `fix/payment-webhook-journalentry-duplicate-key` (tạo mới từ `main`)

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — fix theo pattern đã xác định (EF Core tracking duplicate)
- `domain-integrity-validation` — verify không mutate JournalEntry domain
- `test-system-upgrade` — add unit tests + revert E2E test expectation

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 7
- **Verified Facts:**
  - Fact 1: `OrderService.GenerateAccountingEntriesAsync` line 160-163 gọi `AddToBookAsync(revenueJournalEntry, S2b_HKD)` rồi `AddToBookAsync(revenueJournalEntry, S2c_HKD)` với CÙNG instance.
  - Fact 2: `HKDBookRepository.AddToBookAsync` line 144-145 gọi `_context.JournalEntries.AddAsync(entry)` + `SaveChangesAsync()` mỗi lần — không check đã tracked.
  - Fact 3: `JournalEntry` constructor line 56 set `JournalEntryId = new(Guid.NewGuid())` — PK unique mỗi instance, duplicate chỉ khi same instance re-added.
  - Fact 4: `OrderService.cs` line 628-640 có try-catch bọc `GenerateAccountingEntriesAsync` — exception bị nuốt, log Error, order vẫn mark Paid → silent data inconsistency.
  - Fact 5: E2E test `khachlink-full-order-flow-prod.spec.ts` line 107-108 đang `expect([200, 400, 500])` — workaround chấp nhận bug.
  - Fact 6: `manual-test-vps-guide.md` line 410 + 455 ghi rõ "500 — Pre-existing JournalEntry duplicate key bug (chấp nhận được)".
  - Fact 7: COGS path cũng có pattern tương tự line 180-184 — `cogsJournalEntry` chỉ add 1 lần vào `S2c_HKD` (không duplicate), nhưng nếu sau này thêm book type thì sẽ cùng bug.
- **Assumptions:**
  - A1: `IHKDBookRepository.AddToBookAsync` được gọi sync trong cùng DbContext scope — không có detached/re-attach scenario.
  - A2: `bookType` parameter hiện KHÔNG được dùng trong `AddToBookAsync` (comment line 142-143 nói "For now, just add the journal entry to the database") → fix không cần implement proper book mapping.
- **Open Questions:**
  - Q1: Có nên fix bằng cách check `_context.JournalEntries.Local.Any(e => e.JournalEntryId == entry.JournalEntryId)` trước khi `AddAsync`? Hay deduplicate ở caller (OrderService) — chỉ gọi `AddToBookAsync` 1 lần và ghi nhận book membership qua metadata?
  - Q2: Webhook idempotency cho `/payment` endpoint — có cần thêm check `Order.Status == Paid` trả 200 ngay từ đầu (skip toàn bộ flow) không? Hiện controller không có.
  - Q3: Nên giữ try-catch ở `ConfirmPaymentAsync` line 633-640 sau khi fix root cause không? (Defense-in-depth yes, nhưng có thể mask future bugs.)
- **Recommended Action:** Đợi user approval trước khi implement. Present 2 fix options (Section 10).

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `3_CoreHub/Services/OrderService.cs` | Logic ghi accounting entries — ảnh hưởng toàn bộ order payment flow | Unit test cho 2 book types + COGS path |
| `3_CoreHub/Repositories/HKDBookRepository.cs` | Shared repository cho mọi HKD book operations | Test backward compat: callers khác vẫn add 1 entry/bok type OK |
| `3_CoreHub/Repositories/IHKDBookRepository.cs` | Interface — có thể thêm method mới hoặc update signature | Prefer NOT change interface — fix trong implementation nếu có thể |
| `6_Testing/e2e-tests/khachlink-full-order-flow-prod.spec.ts` | E2E test expectation — revert về `expect(200)` | Chạy E2E local + VPS sau fix |
| `docs/testing/manual-test-vps-guide.md` | Docs — xóa "500 chấp nhận được" | Chỉ update sau khi SC_11 PASS |

## 9. TDD & E2E TESTING STRATEGY
- **Unit Test — Root Cause Reproduction:**
  - Tạo test `GenerateAccountingEntriesAsync_AddsSameEntryToTwoBookTypes_DoesNotThrow` dùng in-memory SQLite + Order mẫu → gọi `GenerateAccountingEntriesAsync` → assert không throw, DB có đúng 1 revenue JournalEntry + 1 COGS JournalEntry.
- **Unit Test — Idempotency:**
  - Tạo test `ConfirmPaymentAsync_DuplicateRequest_Returns200NoExtraEntries` — gọi 2 lần liên tiếp → assert response 200 cả 2 lần, DB có đúng 1 set entries.
- **E2E Test — Webhook Contract:**
  - Revert `khachlink-full-order-flow-prod.spec.ts` line 108: `expect([200, 400, 500])` → `expect(resp.ok())` (chỉ 200).
  - Chạy local + VPS — cả hai phải PASS.
- **Test boundary:**
  - Unit tests: `6_Tests/VanAn.Core.Tests/` — `OrderServiceAccountingTests` (hoặc file mới)
  - Integration tests: không cần mới — `KhachLinkStartupTests` 4/4 vẫn PASS
  - E2E tests: `6_Testing/e2e-tests/khachlink-full-order-flow-prod.spec.ts` — revert expectation + verify

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: 2 Fix Options — Cần User Approval

**Option A (Minimal — Recommended):** Deduplicate ở Caller (`OrderService`)
- Trong `GenerateAccountingEntriesAsync`, thay vì gọi `AddToBookAsync(entry, S2b)` rồi `AddToBookAsync(entry, S2c)`, chỉ gọi MỘT LẦN `AddToBookAsync(entry, S2b_HKD)` — vì `AddToBookAsync` hiện không phân biệt book type (comment line 142-143), entry chỉ cần persist 1 lần.
- Comment rõ ràng: `// Book membership (S2b + S2c) will be tracked via mapping table in future implementation. Currently AddToBookAsync persists once regardless of bookType.`
- Pros: Fix 1 dòng, không đụng repository, backward compat 100%.
- Cons: Mất semantic "entry thuộc book S2c" — nhưng hiện tại cũng chưa có semantic thật (chỉ comment).

**Option B (Repository Guard):** Track-aware `AddToBookAsync`
- Trong `HKDBookRepository.AddToBookAsync`, check `_context.JournalEntries.Local.Any(e => e.JournalEntryId == entry.JournalEntryId)` trước khi `AddAsync` — nếu đã tracked, skip AddAsync + skip SaveChangesAsync (hoặc chỉ SaveChangesAsync nếu có thay đổi).
- Pros: Repository robust với mọi caller (không chỉ OrderService).
- Cons: Thêm logic vào repository, có thể mask bug khác (entry được add nhầm book type cũng silent skip).

**Recommendation:** Option A + giữ Option B làm defense-in-depth (l Layer 2 guard). Cần user approval trước khi implement.

### Micro-phase breakdown

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Confirm Option A vs B vs A+B với user | Nothing — await approval |
| **S2** | Write failing unit test reproducing duplicate key | `OrderServiceAccountingTests.cs` — 2 tests (root cause + idempotency), assert FAIL trước fix |
| **S3** | Apply fix per approved option | Edit `OrderService.cs` (Option A) and/or `HKDBookRepository.cs` (Option B) |
| **S4** | Verify unit tests PASS, revert E2E expectation, run local E2E | `dotnet test VanAn.Core.Tests` + `npx playwright test khachlink-full-order-flow-prod.spec.ts` |
| **S5** | Deploy VPS + verify | `cd.yml` trigger hoặc SCP, manual webhook call, kiểm tra PostgreSQL entries |

### Rules
- Tuân thủ 3-Round Fix Limit (governance.md) — nếu fix không work trong 3 rounds, STOP + report.
- KHÔNG sửa Domain (`JournalEntry.cs`).
- KHÔNG bỏ try-catch ở `ConfirmPaymentAsync` line 633-640 (defense-in-depth giữ lại).
- Update `docs/AI/project_state.md` Section 3 + 11 sau khi SC_11 PASS.

## 11. ESTIMATED EFFORT
- ~2-3 sessions JIT Planning (S1 approval + S2 test + S3 fix)
- 1 session verify (S4 local E2E)
- 1 session deploy + VPS verify (S5)
- **BLOCKER:** Cần user approval Option A vs B vs A+B trước khi bắt đầu S2.
