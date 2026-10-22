# TASK CARD: P1.1 — MEMBER EXIT SETTLEMENT (settle-then-burn)

> Created: 2026-10-22 (audit `docs/specs/loyalty-alliance-audit.md` §6 Q3 — user chốt: settle-then-burn)
> Status: PLANNED
> Branch: `main`

## 1. GOAL & CONTEXT

- **Quyết định:** khi member tenant rời liên minh → HTX settle với tenant đó (qua report P1.2) → **burn** phần điểm khách earn attributed cho tenant đó. Điểm KHÔNG được tiếp tục tiêu ở member còn lại (R4: chi phí chuyển cho member ở lại).
- **Reuse có sẵn:** `AllianceWalletService.ComputeNetEarnByTenantAsync(walletId)` trả `Dictionary<TenantId, NetEarn>` — đang dùng cho `SplitWalletsAsync` (migration Alliance→Silo). Attribution `SourceTenantId`/`TransactionTenantId` đã ghi trên mọi `AllianceTransaction`.
- **Attribution semantics (đã implement):** EARN row ghi `TransactionTenantId` = nơi khách mua; redeem multi-source FIFO phần rã theo `SourceTenantId` của các earn. `NetEarn[tenant]` = phần điểm còn lại attribute về tenant đó.

## 2. WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Skills (max 3):** `domain-integrity-validation` + `pattern-based-fixing` + `test-system-upgrade`

## 3. SCOPE

### 3.1 Service (`3_CoreHub/Services/AllianceWalletService.cs` hoặc service mới nhỏ)
- [ ] `ExitMemberSettlementAsync(Guid tenantId, string settlementNote)` — SystemAdmin-triggered, idempotent:
  1. Guard: tenant phải đã opt-out Alliance (`LoyaltyConfig.IsAllianceMember=false` hoặc `LoyaltyMode` không còn Alliance) — không burn khi tenant còn active member.
  2. Duyệt mọi `AllianceWallets` (paging): `ComputeNetEarnByTenantAsync(wallet.Id)` → `netEarn[tenantId]` > 0 → ghi `AllianceTransaction` type `ADJUST`, `points = -netEarn`, `TransactionTenantId = tenantId`, `Reason = "member-exit:{tenantId} {settlementNote}"`, `IdempotencyKey = "exit:{tenantId}:{walletId}"` — retry safe.
  3. Balance mutation qua **atomic `ExecuteUpdateAsync` + `ReloadAsync`** (pattern P0-RV2 `b0fad8ee` — TUYỆT ĐỐI không resync tracked entity).
  4. Wrap `ExecuteAtomicAsync` per wallet (PG retrying strategy — bài học `76565324`).
  5. Kết quả: `(walletsProcessed, totalPointsBurned)` → publish `LoyaltyPointsChanged` cho từng wallet (mirror sync).
- [ ] Idempotency: unique index `IdempotencyKey` đã có — re-run an toàn (dup key → skip qua dedup check trước lock).

### 3.2 API
- [ ] `POST /api/v1/admin/loyalty/alliance/member-exit` — `[Authorize(SystemAdmin)]` trên Gateway (pattern `AdminLoyaltyConfigController` có sẵn). Request: `{tenantId, settlementNote}` — note bắt buộc (audit trail, giống reason bắt buộc của cash-payment-review).

### 3.3 Tests (`6_Tests/VanAn.Core.Tests/Services/`)
- [ ] Wallet có netEarn[T]>0 → 1 ADJUST row −netEarn, balance giảm đúng.
- [ ] Wallet không có attribution T → bỏ qua.
- [ ] Re-run → không ghi đôi (idempotency key).
- [ ] Tenant còn Alliance → reject.
- [ ] Nhiều wallet cùng lúc → tổng burned = Σ netEarn[T].

## 4. DECISIONS (đã chốt — không hỏi lại)

| # | Quyết định |
|---|---|
| Q3 | Burn toàn bộ attributed balance của tenant exit — KHÔNG chuyển phần về member khác (phức tạp FIFO re-attribution, ít giá trị MVP) |
| — | Không auto-trigger khi opt-out — chỉ chạy khi SystemAdmin gọi sau khi HTX settle xong (thứ tự nghiệp vụ: settle tiền → burn điểm) |

## 5. CONSTRAINTS

- Append-only `AllianceTransaction` — burn = row `ADJUST` mới, không sửa row cũ.
- KHÔNG đụng Domain.cs (không entity mới).
- `guard-check.ps1` + build + Core.Tests MUST PASS.

## 6. DoD

- [ ] Settlement idempotent, atomic per wallet, attribution chính xác theo `SourceTenantId`
- [ ] Admin endpoint SystemAdmin-only + audit reason bắt buộc
- [ ] Tests pass, không regression loyalty suite
