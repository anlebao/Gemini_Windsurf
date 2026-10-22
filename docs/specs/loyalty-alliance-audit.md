# AUDIT REPORT — Loyalty Alliance vs SRS "VẠN AN LOYALTY ALLIANCE"

**Date:** 2026-10-22 · **Auditor:** Devin · **Scope:** SRS `docs/requirements/SRS — VẠN AN LOYALTY ALLIANCE.md` vs base code
**Spec đối chiếu:** `docs/specs/loyalty-alliance-spec.md` v1.0 · **Method:** audit-first, evidence-based (file:line)

---

## 1. Executive Summary

**Alliance subsystem ĐÃ ĐƯỢC IMPLEMENT và vượt xa spec v1.0** — không phải "chưa làm gì". Code có sẵn: ví cross-tenant `AllianceWallet` + sổ cái `AllianceTransaction` (append-only, có `IdempotencyKey`, `SourceTenantId` attribution FIFO), `LoyaltyPointLedgerService` làm single write-path, `LoyaltyModeResolver` (global→tenant→opt-out), `LoyaltyBudgetService` (4 loại cap, atomic counters), `RedemptionService` mode-routing, `LoyaltySyncSubscriber` (NATS mirror PG→SQLite, MAX-merge), `InternalLoyaltyController` (internal API-key), consolidate/split migration 2 chiều.

**Gap lớn nhất vs SRS (P0/P1):**
1. **TC-04 concurrency:** `AllianceWallet` không có lock/rowversion — read-modify-write TOCTOU → concurrent redeem có thể double-spend (khác với `WalletService` đã có `pg_advisory_xact_lock` + FOR UPDATE).
2. **TC-10:** redeem = deduct-then-create-voucher; voucher fail → PG đã trừ, chỉ có log "manual reconcile needed" (`RedemptionService.cs:172`).
3. **NFR-04:** `ComputeNetEarnByTenantAsync` load **toàn bộ** transaction history vào memory mỗi lần deduct — vi phạm "không quét full history mỗi lần dùng" (`AllianceWalletService.cs:547-580`).
4. **FR-08:** không có point expiry (chỉ voucher expiry).
5. **FR-13:** không có dispute/manual-adjust workflow (không có endpoint adjust số dư cho admin).
6. **FR-01/LEG-01:** không có LoyaltyProgram/ProgramVersion — không có "thể lệ" versioned.
7. **FR-14:** member exit — điểm attribute cho tenant đã rời vẫn dùng được ở member khác → rò rỉ chi phí (không có transition policy).
8. **RefundOrchestration flag `ValcnV2_RefundReversal` default OFF** → đơn cancel hiện KHÔNG đảo điểm trên prod (silent leak).

**Khuyến nghị:** TRIỂN KHAI TIẾP trên nền hiện có (reuse-first đúng SRS), fix theo risk register — KHÔNG rewrite.

---

## 2. Architecture Discovery (đã verify qua code)

```
Order delivered/completed → OrderWorkflowService.ProcessLoyaltyPointsAsync (OrderWorkflowService.cs:137-149,275-279)
  → LoyaltyPointLedgerService.AwardAsync (LoyaltyPointLedgerService.cs:57)
       → orderId double-award guard (LoyaltyIssuanceRecord + legacy Silo history fallback) :72-89
       → budget check (LoyaltyBudgetService, always-on) :102-119
       → mode routing: Alliance → AllianceWalletService.AddPointsAsync | Silo → LoyaltyRewardsService
       → LoyaltyIssuanceRecord (PG, per-order, IsReversed flag)
  → LoyaltyBalanceSyncPublisher → NATS vanan.cloud.loyalty.changed.{deviceId}
       → ShopERP LoyaltySyncSubscriber → SQLite LoyaltyRewards mirror (MAX-merge, idempotent history append)

Redeem → RedemptionService.RedeemAsync (RedemptionService.cs:87)
  → mode routing → Alliance: wallet.DeductPointsAsync (FIFO SourceTenantId attribution :199-233)
                 → voucher + RedemptionRecord in SQLite
Cancel voucher → CancelAsync → RefundPointsWithModeRouting → wallet RefundAsync (ADJUST tx)
Order cancel → RefundOrchestrationService (flag ValcnV2_RefundReversal, DEFAULT OFF)
Config → LoyaltyConfigController [Authorize(SystemAdmin)] → PG + NATS mirror config → SQLite
```

**Authoritative source:** PG (`AllianceWallet` + `AllianceTransaction`). SQLite `LoyaltyRewards` = display mirror (Alliance) / source (Silo). Đúng Option C.

---

## 3. Requirement Coverage Matrix

### FR

| ID | Yêu cầu | Status | Evidence | Gap |
|---|---|---|---|---|
| FR-01 | Quản lý chương trình (Program, version, states) | **Chưa có** | Không tồn tại `LoyaltyProgram`/`ProgramVersion` entity (grep toàn repo) | Chỉ có `LoyaltyGlobalConfig` + `LastChangedBy` — không versioned thể lệ, không states, không multi-program |
| FR-02 | Quản lý thành viên liên minh | **Một phần** | `LoyaltyTenantConfig.IsAllianceMember` (Domain.cs:3168) + opt-out resolver (LoyaltyModeResolver.cs:27) | Thiếu: ngày join/leave, member states, policies-accepted, data-scope |
| FR-03 | Tài khoản điểm khách | **Một phần** | `AllianceWallet` 1-per-device unique (Domain.cs:3241); `GetWalletByDeviceIdAsync`, `GetTransactionsAsync` | Thiếu: trạng thái Suspended/Closed (chỉ IsActive+Freeze), khóa theo rủi ro, dedup theo khách-thật (device ≠ người — 1 người nhiều device = nhiều ví) |
| FR-04 | Tích điểm từ giao dịch hợp lệ | **Đã có** | `LoyaltyPointLedgerService.AwardAsync`: orderId guard :72-89, `IdempotencyKey="earn:{orderId}"` (OrderWorkflowService.cs:700), award on delivered+completed | Thiếu: không có cơ chế shop gửi "arbitrary points" — good (đúng yêu cầu) |
| FR-05 | Quy tắc tính điểm | **Một phần** | `LoyaltyPointsConfig` (rate/min/max/AwardOnAll, Domain.cs:2895) + editable global formula (`UpdatePointsFormula`) + `LoyaltyPointsCalculator` unified | Thiếu: exclusion lists, per-campaign rules, versioned policies (EffectiveDate), per-program rates |
| FR-06 | Sử dụng điểm (hold→confirm) | **Một phần — có defect** | `RedemptionService.RedeemAsync` check balance+member+catalog; wallet deduct idempotent | Không có hold/release; deduct trước → voucher fail = PG đã trừ "manual reconcile needed" (:172-175) |
| FR-07 | Cấu hình ưu đãi | **Một phần** | `RedemptionCatalogItem.PointsRequired` per tenant + `VoucherExpiryDays` | Thiếu: % giá trị đơn cap khi đổi, product exclusions, non-stacking rules |
| FR-08 | Hết hạn điểm | **Chưa có** | Grep: không field ExpiresAt/batch trên `AllianceTransaction`/`AllianceWallet` | Chỉ voucher có `VoucherExpiryDays` — điểm sống vĩnh viễn |
| FR-09 | Hoàn/hủy/đảo điểm | **Một phần** | `RevertOrderAsync` (issuance reversal, `revert:{orderId}:{recordId}` idempotent, mark-reversed chống re-reversal) · `CancelAsync` refund · `RefundOrchestrationService` 4-step | **Flag `ValcnV2_RefundReversal` default OFF** → cancel không đảo điểm; partial-refund chưa rõ; khách đã tiêu điểm → deduct fail → "points retained" (:355-360) |
| FR-10 | Sổ cái bất biến | **Một phần** | `AllianceTransaction` append-only + BalanceAfter + IdempotencyKey + SourceTenantId set-once (throws on re-set :3340-3350) | Thiếu: Hold/Release types, RuleVersion, CorrelationId, ProgramId; Silo history = **JSON blob** trong `LoyaltyRewards.History` (không relational, không rebuild được chắc chắn) |
| FR-11 | Ngân sách ưu đãi | **Một phần** | `LoyaltyTenantConfig` caps: monthly/daily/per-customer/per-order-rate + atomic `ExecuteUpdateAsync` counters (LoyaltyBudgetService.cs:138-152) | Cap theo điểm, không phải VND budget; INV-009 deferred (không tính được chi phí ưu đãi VND) |
| FR-12 | Đối soát/báo cáo | **Một phần** | `LoyaltyDashboardStatsService` (4 metrics) + `GetTransactionsByTenantAsync` | Thiếu: đối soát balance-vs-ledger, báo cáo lỗi/pending, expired/adjusted reports |
| FR-13 | Khiếu nại/điều chỉnh | **Chưa có** | Không có `AdjustmentCase` entity/endpoint; `ADJUST` chỉ sinh từ migrate/refund | Không có endpoint admin adjust số dư, không approval workflow |
| FR-14 | Chấm dứt tham gia | **Một phần — có defect** | `IsAllianceMember=false` → forced Silo (`LoyaltyModeResolver.cs:27-30`) + `SplitWalletsAsync` | Điểm đã earn tại tenant rời đi vẫn spendable ở member khác (attribution chỉ là tracking) → **chi phí chuyển sang member còn lại**; không transition policy/announcement |

### NFR

| ID | Yêu cầu | Status | Evidence | Gap |
|---|---|---|---|---|
| NFR-01 | Bảo mật/phân quyền | **Một phần** | `[Authorize(Policy="SystemAdmin")]` config (LoyaltyConfigController.cs:20) · `[InternalApiKey]` internal API · `X-Customer-Token` validate thủ công · TenantId pin + IgnoreQueryFilters explicit | Internal API key = **toàn quyền** add/deduct bất kỳ số điểm nào (không rate-limit, không approval); `LoyaltyController` class `[AllowAnonymous]` + manual token |
| NFR-02 | Toàn vẹn giao dịch | **Một phần — có defect** | IdempotencyKey mọi write · atomic counters `ExecuteUpdateAsync` · single-identity pattern | `AllianceWallet` **không lock/rowversion** — read-modify-write TOCTOU (concurrent deduct double-spend); `BalanceAfter` app-computed; voucher-creation-fail không auto-compensate |
| NFR-03 | Tương thích kiến trúc | **Đã có** | PG wallet + NATS mirror + `LoyaltyReadRouter` fallback + HTTP proxy (multi-VPS ready) | — |
| NFR-04 | Hiệu năng | **Sai** | `ComputeNetEarnByTenantAsync` load full tx history vào memory mỗi deduct (AllianceWalletService.cs:547) — O(n) không giới hạn | Cần materialized per-tenant-net hoặc SQL aggregate; `GetTransactionsAsync` có limit ✓ |
| NFR-05 | Quan sát | **Một phần** | Structured logging đầy đủ (reason, balance, device, tenant) | Không metrics/alerting drift balance-vs-ledger, không fraud detection |

### LEG

| ID | Yêu cầu | Status | Note |
|---|---|---|---|
| LEG-01 | Thể lệ chương trình | **Chưa có** | Không có storage/publish thể lệ |
| LEG-02 | Điểm ≠ tiền | **Đã có** (by absence) | Không có chức năng nạp/rút/chuyển/mua-bán điểm — đúng |
| LEG-03 | TMĐT/khuyến mại | **Chưa xác minh** | Câu hỏi pháp lý — chủ thể chương trình, thông báo Sở Công Thương |
| LEG-04 | Dữ liệu cá nhân | **Một phần** | Tenant chỉ thấy tx tại mình (Q3) — tốt; chưa có consent/retention/xóa riêng cho loyalty |
| LEG-05 | Thuế/kế toán | **Một phần** | Điểm tách khỏi tiền (ledger riêng); chưa có VND-value (INV-009 deferred) → chưa tính được chi phí ưu đãi kế toán |

### TC (test cases SRS)

| TC | Status | Evidence |
|---|---|---|
| TC-01 award hợp lệ | ✅ | `OrderWorkflowAllianceTests`, `LoyaltyPointLedgerServiceTests` |
| TC-02 retry idempotent | ✅ | IdempotencyKey + dedup (LoyaltySyncSubscriberTests 14 tests) |
| TC-03 cancel/refund đảo điểm | ⚠️ | Code có — nhưng flag OFF trên prod |
| TC-04 concurrent spend | ❌ | Không lock → race thật |
| TC-05 non-member reject | ✅ | `IsAllianceMemberAsync` check → "Tenant không tham gia" |
| TC-06 vượt hạn mức | ✅ | MaxPointsPerOrder + MaxWalletPoints + budget caps |
| TC-07 expiry | ➖ N/A | Không có expiry feature |
| TC-08 cross-tenant | ✅ | TenantId pin, member chỉ thấy tx của mình |
| TC-09 unauthorized adjust | ✅ (by absence) | Không có adjust endpoint |
| TC-10 order fail giữa chừng | ❌ | Voucher fail → PG đã trừ, manual reconcile |
| TC-11 offline sync | ✅ | MAX-merge + idempotent append + Outbox fallback |
| TC-12 member exit | ⚠️ | Opt-out có, transition policy không |
| TC-13 balance vs ledger đối soát | ❌ | Không có reconciliation job |
| TC-14 data scope | ✅ | `GetTransactionsByTenantAsync` scoped |
| TC-15 điểm thành tiền | ✅ | Không có chức năng |

---

## 4. Reuse / Modify / Add / Remove

| Nhóm | Thành phần |
|---|---|
| **Reuse** | `AllianceWallet`+`AllianceTransaction` · `LoyaltyPointLedgerService` · `LoyaltyModeResolver` · `LoyaltyBudgetService` · `InternalLoyaltyController` · `LoyaltySyncSubscriber` · consolidate/split · `LoyaltyIssuanceRecord` |
| **Modify** | `AllianceWalletService.DeductPointsAsync` → thêm lock (pg advisory/FOR UPDATE, precedent `WalletService` HR-SCALE-3) + SQL-aggregate thay full-history-load · `RedemptionService` → hold/confirm hoặc compensation-refund khi voucher fail · `AllianceTransaction` → thêm field theo FR-10 (RuleVersion/CorrelationId/ProgramId nếu cần) · bật `ValcnV2_RefundReversal` sau khi test |
| **Add** | Points expiry (batch/lot) · `LoyaltyAdjustmentCase` (dispute + approval) · member-exit transition policy · reconciliation job (balance vs Σtx) · per-tenant-net materialized table · admin manual-adjust endpoint có approval |
| **Remove** | Không — không thành phần nào cần loại bỏ; Silo giữ nguyên cho non-member |

## 5. Risk Register

| # | Rủi ro | Bằng chứng | Xác suất | Ảnh hưởng | Giảm thiểu | Quyết định bởi |
|---|---|---|---|---|---|---|
| R1 | Double-spend concurrent redeem | Không lock trên wallet ops | Trung bình | Cao (P0) | `pg_advisory_xact_lock` per walletId hoặc conditional UPDATE `WHERE balance>=x` | Tech |
| R2 | PG đã trừ + voucher fail → orphan deduction | RedemptionService.cs:172-175 | Trung bình | Cao | Compensation refund auto hoặc create-pending-first saga | Tech |
| R3 | Full-history scan mỗi deduct | AllianceWalletService.cs:547 | Cao (theo thời gian) | Cao (perf) | Materialized `WalletTenantNet` table cập nhật theo tx | Tech |
| R4 | Member exit → điểm dùng ở member khác = chi phí leak | Attribution tracking only | Cao khi có exit | Trung bình | Exit policy: burn/transfer/split theo thể lệ | **User + HTX** |
| R5 | Cancel order không đảo điểm (flag OFF) | `ValcnV2_RefundReversal` default OFF | Cao (đang xảy ra) | Trung bình | Bật flag sau test hoặc batch đảo định kỳ | Tech + User |
| R6 | Silo history JSON blob — không truy vết relational | `LoyaltyRewards.History` string | Đã có | Trung bình | Migrate sang bảng relational khi cần audit Silo | Tech |
| R7 | Internal API key = unlimited mint/burn | InternalLoyaltyController | Thấp | Cao | Thêm scope/approval cho adjust, rate-limit | Tech |
| R8 | Không đối soát balance vs Σtx | Không reconciliation job | Trung bình | Cao (drift âm thầm) | Nightly reconcile job + alert | Tech |
| R9 | Config đổi không có version → điểm cũ truy vết không ra rule đã áp | `LoyaltyGlobalConfig` LastChanged only | Thấp | Trung bình | RuleVersion trên tx hoặc EntityVersion | Tech |
| R10 | Legacy REDEEM null SourceTenantId làm lệch attribution khi split | documented :541-543 | Thấp | Thấp | Đã có fallback hợp lý; ghi nhận khi split | Tech |

## 6. Câu hỏi cần User quyết định (SRS §12.7) — ✅ TẤT CẢ ĐÃ CHỐT 2026-10-22

Nguyên tắc đã duyệt: **quyết định nghiệp vụ ≠ feature mới** — tối thiểu code, tận dụng attribution + idempotency + audit trail đã xây ở P0.

1. ~~**Chủ thể chương trình**~~ → **✅ CHỐT: HTX chủ trương.** SystemAdmin quản trị config **thay mặt HTX** — vấn đề pháp lý-tài liệu (điều khoản/hợp đồng member), **0 code**. LEG-01/03 giải quyết ở lớp văn bản, không cần `LoyaltyProgram` entity.
2. ~~**Hết hạn điểm**~~ → **✅ CHỐT: KHÔNG expiry.** Điều khoản ghi "điểm không hết hạn trong vòng đời chương trình" — **0 code**. Escape hatch nếu sau này cần: batch expiry cuối năm chương trình (1 job + `EXPIRE` tx type + config ngày — ~150 LOC, không FIFO per-point).
3. ~~**Member exit**~~ → **✅ CHỐT: settle-then-burn.** Khi member rời → HTX settle với tenant đó (qua report #4) → burn điểm attributed bằng **reuse `ComputeNetEarnByTenantAsync`** (đang dùng cho split migration) + `ADJUST` tx rows — task card `docs/AI/tasks/loyalty_alliance_p1/task_p1_1_member_exit_settlement.md` (~100 LOC).
4. ~~**Ai tài trợ**~~ → **✅ CHỐT: member chịu redeem tại mình (status quo) + settlement report read-only.** Report tính `Σ earn-credit` vs `Σ redeem-cost` per member từ `SourceTenantId`/`TransactionTenantId` đã ghi sẵn → chênh lệch → HTX chuyển khoản/hóa đơn **ngoài hệ thống** — task card `task_p1_2_cross_tenant_settlement_report.md` (~80 LOC, không ledger mới).
5. ~~**Hoàn đơn sau khi khách đã tiêu điểm**~~ → **✅ ĐÃ CHỐT 2026-10-22: chỉ award khi `completed` (terminal)** — edge case biến mất trong builtin flow; reversal giữ làm safety net cho flow-custom/backfill (trừ tối đa balance, phần thiếu bỏ).
6. ~~**Khiếu nại**~~ → **✅ CHỐT: SystemAdmin adjust trực tiếp + audit = đủ MVP.** Form nhỏ trên `/admin/loyalty-reconcile` gọi `points/add|deduct` internal API sẵn có (Reason + IdempotencyKey) — task card `task_p1_3_admin_adjust_points.md` (~120 LOC). Workflow phê duyệt để khi HTX cần role riêng.

**Tổng code còn lại: ~300 LOC** (3 task cards P1) — không entity mới, không rule engine, không settlement ledger.

---

*Audit hoàn tất theo SRS Phần XIII items 1-10. Không sửa code; mọi đề xuất chờ phê duyệt.*
