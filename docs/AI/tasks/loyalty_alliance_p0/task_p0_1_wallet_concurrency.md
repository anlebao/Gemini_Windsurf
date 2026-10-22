# TASK CARD: P0.1 — ALLIANCE WALLET CONCURRENCY (TOCTOU double-spend)

> Created: 2026-10-22 (audit `docs/specs/loyalty-alliance-audit.md` — Risk R1, SRS TC-04)
> Status: IMPLEMENTED 2026-10-22 — tests pass (14/14 P0 + 171/171 loyalty suite)
> Branch: `main`

## 1. GOAL & CONTEXT

- **Defect:** `AllianceWalletService.AddPointsAsync/DeductPointsAsync/RefundAsync` là read-modify-write thuần EF — không lock, không rowversion, không conditional update. Hai redeem đồng thời (2 request khác idempotency-key) đều đọc `TotalPointBalance=100` → cả hai pass check → trừ 80×2 → **double-spend, số dư âm về mặt nghiệp vụ** (`DeductPoints` clamp `Math.Max(0,...)` nên số dư không âm nhưng 160 điểm tiêu trên 100 có = gian lận).
- **Precedent có sẵn trong repo:** `WalletService` (tiền) dùng `pg_advisory_xact_lock` + `SELECT ... FOR UPDATE` (HR-SCALE-3, đã deploy prod); `BookingService` dùng `pg_advisory_xact_lock(hashtextextended(tenant:staff))` + unique index defense-in-depth. **Áp đúng pattern đó cho AllianceWallet.**
- **Bằng chứng:** `AllianceWalletService.cs:64-271` — balance check (`:185-191`) tách khỏi `wallet.DeductPoints` + `SaveChanges` (`:245-263`); giữa hai bước không có serialization nào.

## 2. WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Skills (max 3):** `domain-integrity-validation` + `pattern-based-fixing` + `test-system-upgrade`

## 3. SCOPE

### 3.1 Core fix (`3_CoreHub/Services/AllianceWalletService.cs`)
- [ ] Wrap mutation flow trong transaction + **PG advisory lock keyed by walletId** (hoặc CustomerDeviceId): `pg_advisory_xact_lock(hashtextextended('loyalty_wallet:{deviceId}'))` — pattern `BookingService.cs:783-798`.
- [ ] Sau lock: re-read wallet `SELECT ... FOR UPDATE` rồi mới check balance + mutate (chống stale read).
- [ ] SQLite fallback path (nếu provider không phải PG): giữ nguyên behavior hiện tại (single-writer per ShopERP DB — chấp nhận được vì wallet sống ở PG; SQLite branch chỉ có trong test).
- [ ] Áp cho cả 3 method: `AddPointsAsync`, `DeductPointsAsync`, `RefundAsync` (đều mutate cùng wallet row).
- [ ] `ConsolidateWalletsAsync`/`SplitWalletsAsync` (migration ops — SystemAdmin gọi tay): cũng lock để không đua với redeem đang chạy.
- [ ] Idempotency check GIỮ NGUYÊN và chạy TRƯỚC lock (retry same-key không cần chờ lock).

### 3.2 Defense-in-depth
- [ ] Conditional update guard: `DeductPoints` → `ExecuteUpdate WHERE Id=@id AND TotalPointBalance >= @points` — nếu 0 rows affected → retry-once sau khi re-read (belt & suspenders nếu advisory lock miss).
- [ ] Check index: `AllianceTransactions.IdempotencyKey` cần unique index (hiện dedup = `FirstOrDefaultAsync` — race 2 thread cùng key có thể insert đôi; unique index + catch 23505 → return cached).

### 3.3 Tests (`6_Tests/VanAn.Core.Tests/Services/`)
- [ ] Race test kiểu Booking: 2 concurrent `DeductPointsAsync` khác key, balance=100, mỗi cái 80 → đúng 1 success, 1 "Insufficient balance", Σ tx points = -80 (không phải -160).
- [ ] Concurrent Add+Deduct → đúng thứ tự serialization, không lost-update.
- [ ] Idempotency race: 2 request cùng key → 1 tx row.
- [ ] Deadlock-free: lock ordering nhất quán (1 wallet per call → không vấn đề ordering).

## 4. DECISIONS

| # | Câu hỏi | Đề xuất |
|---|---|---|
| Q1 | Advisory lock hay conditional-update-only? | **Cả hai** — advisory lock là chính (giữ read-FIFO-attribution nhất quán), conditional update là fallback |
| Q2 | Lock scope: walletId hay deviceId? | walletId (1 ví/device — tương đương; walletId ổn hơn khi merge) |
| Q3 | `ComputeNetEarnByTenantAsync` nằm trong lock? | Có — attribution phải đọc consistent snapshot |

## 5. CONSTRAINTS

- KHÔNG đụng Domain.cs nếu không cần — fix ở service layer (lock là infra concern). Nếu cần thêm field entity → Domain Phase approval trước.
- Append-only: `AllianceTransaction` giữ nguyên — không sửa tx đã ghi.
- `guard-check.ps1` + build + Core.Tests MUST PASS.
- Playwright DISABLED trong IMPLEMENT.

## 6. DoD

- [ ] Race test: 2×80 trên balance=100 → chỉ 1 thành công
- [ ] `AllianceTransactions.IdempotencyKey` unique index + duplicate-key catch
- [ ] Toàn bộ mutation path qua lock; không regression Silo flow
- [ ] Core.Tests + ShopERP.Tests PASS
