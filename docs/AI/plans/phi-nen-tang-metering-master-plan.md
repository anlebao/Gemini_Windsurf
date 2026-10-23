# MASTER PLAN: PHÍ NỀN TẢNG — METERING SERVICE (`vanan-metering`)

> Created: 2026-10-23 — user chốt v1.3 (Policy `docs/requirements/Proposal_Phi_Nen_Tang_Policy_v1.3.md` · SRS `docs/requirements/van_an_phi_nen_tang_metering_srs_v1.md`)
> Status: **📋 PLANNED — tất cả quyết định O1–O5 ĐÃ CHỐT (23/10/2026), chờ user approve plan để bắt P0**
> Code metering: folder **`8_Metering/`** trong monorepo — solution riêng `VanAn.Metering.sln`, image `ghcr.io/anlebao/vanan-metering`, KHÔNG reference `1_Shared`/`3_CoreHub`

---

## 1. MỤC TIÊU

Đáp ứng 3 quyết định user (23/10/2026):

1. **Metering module độc lập** — ít dính mắc code hiện tại, chạy riêng docker container, có thể VPS riêng, gắn được vào hệ thống khác để tính phí dịch vụ.
2. **Rate Card linh hoạt** — SystemAdmin thêm/xóa/sửa hạng mục + mức giá, versioned, không cần deploy.
3. **Roadmap 3 giai đoạn** — MVP đơn giản nhất → Trung bình → Hoàn thiện (khắc khe kỹ thuật/pháp lý/kế toán/kinh doanh).

**Nguyên tắc:** SRS [G1] one-way boundary (caller → metering, metering → NATS broadcast); KHÔNG shared code/DB với VanAn; **top-up GĐ-1 = chuyển khoản VietQR thủ công (admin xác nhận)** — thanh toán tự động/hóa đơn chỉ từ GĐ-3 (P17).

---

## 2. HIỆN TRẠNG (đã xác minh 2026-10-22)

| Thành phần | Hiện trạng | Vai trò trong plan |
|---|---|---|
| Usage metering | Không tồn tại | ❌ BUILD mới hoàn toàn |
| Charge-per-use | `CollaboratorVerificationService` (check số dư → task → trừ `SmsOtpFee` flat qua `SystemSetting`) | Precedent flow; SRS [G14] đối chiếu Option B |
| Ledger pattern | `WalletTransaction`, `AllianceWallet`/`AllianceTransaction` append-only + NATS sync + idempotencyKey | Clone cho `CreditLedgerEntry` (GĐ-2) |
| Idempotency | `Idempotency-Key` (booking), `idempotencyKey` (alliance, unique index) | REUSE pattern |
| OCR | `IOcrEngine`/`IOcrConfigService` đang xây | Task metering đầu tiên (candidate) |
| NATS | `vanan.cloud.*` subjects + `LoyaltySyncSubscriber` pattern | REUSE cho mirror GĐ-2 |
| CI/CD | `cd-multivps.yml` (3 VPS) + GHCR images | Thêm deploy_set `vanan-metering` |
| Alerts | `IAlertNotifier` + Telegram/Zalo stub | REUSE cảnh báo số dư GĐ-2 |

---

## 3. QUYẾT ĐỊNH ĐÃ CHỐT (user 23/10/2026)

| # | Quyết định |
|---|---|
| D1 | Tên sản phẩm: **"Phí nền tảng"** (đơn vị định mức nội bộ = credit) |
| D2 | Rate Card do **SystemAdmin CRUD** qua UI/API — versioned, effective-dated, audit |
| D3 | Metering = **module độc lập** — container riêng, có thể VPS riêng, API contract generic, gắn được hệ thống khác |
| D4 | Roadmap **3 giai đoạn** (GĐ-1 MVP / GĐ-2 trung bình / GĐ-3 hoàn thiện) — gate nghiêm ngặt giữa các giai đoạn |
| D5 | **GĐ-1 có ví quota + enforce + top-up:** cảnh báo khi ví dùng 50%/90% (còn 50%/10%), deny khi hết số dư + thông báo + hướng dẫn top-up quét QR; `TopupConfig` (số TK/tên thụ hưởng/ngân hàng) do SystemAdmin thiết lập; top-up = VietQR thủ công, admin xác nhận nhận tiền → cộng credit |

---

## 4. QUYẾT ĐỊNH KIẾN TRÚC (O1–O5: ✅ tất cả ĐÃ CHỐT 23/10/2026)

| # | Câu hỏi | Quyết định |
|---|---|---|
| O1 | Ledger credit đặt ở DB metering riêng hay PG Gateway? | ✅ **DB metering riêng** (policy §14.18) — độc lập để gắn hệ thống khác; đối soát định kỳ với sổ kế toán PG; backup metering DB = critical |
| O2 | Option A (credit riêng) vs Option B (VND deposit wallet) | ✅ **Option A — CreditWallet riêng trong metering** — HTX Pool/Earned là quyền lợi không-tiền-backing, ví VND (rút được) không biểu diễn được và rủi ro "giá trị lưu trữ"; Deposit wallet VND giữ vai trò precedent flow |
| O3 | ✅ **ĐÃ CHỐT (23/10/2026)** — định mức theo **giá trị sử dụng**: (a) traffic storefront tenant (`diemthuong2.khachvip.online/store/by-id/{tenantId}`): lượt truy cập · số tin nhắn + dung lượng chat · lượt tìm kiếm & redirect từ timlathay.com; (b) tính năng module: booking · tài chính (`/financial`) · quản lý kho (`/inventory`). Nhóm task-based (OCR/AI/export…) defer GĐ-2+. **Chi tiết hóa (chốt cùng ngày):** booking = per-action · financial/inventory = **module-active-month** · enforcement: `feature.*`+`chat.*` = Deny, `store.visit`/`directory.*` = MeterOnly (O-4 SRS) |
| O4 | Repo metering: repo riêng hay folder trong monorepo? | ✅ **Folder `8_Metering/vanan-metering` trong monorepo** — solution riêng, image riêng, re-use CI patterns; tách repo khi có hệ thống ngoài adopt. **Deviation đã phê:** rule "no new .csproj" vốn chặn tách host app chính — metering là service độc lập theo chỉ đạo user |
| O5 | Tech stack metering service | ✅ **.NET 8 minimal API + EF Core + SQLite (GĐ-1/2) → PostgreSQL (GĐ-3)** — đồng stack team, tái dùng CI/test patterns |

---

## 5. ROADMAP 3 GIAI ĐOẠN ↔ PHASES

### GĐ-1 — MVP (metering + ví quota + cảnh báo/deny + top-up VietQR thủ công)

| Phase | Nội dung | Deliverable | Gate |
|---|---|---|---|
| **P0** | ANALYZE (O1–O5 đã chốt) · ma trận REUSE/MODIFY/EXTEND/BUILD cho 4 hook nguồn · danh mục tác vụ GĐ-1 (O3) · phân loại chức năng cốt lõi/trả phí | Task card + quyết định | User approve |
| **P1** ✅ | Skeleton `8_Metering/vanan-metering`: minimal API + EF SQLite + `UsageEvent` + `CallerKey` + health + Dockerfile + CI build image — **DONE 2026-10-23** (build 0/0, smoke local PASS, CI matrix `metering` wired) | Service chạy, `/v1/health` 200 | build xanh |
| **P2** | Rate Card: `RateCardVersion` + CRUD admin API + audit + resolve-version-theo-thời-điểm + unit tests | CRUD versioned, sửa giá không deploy | tests pass |
| **P3** | Ingest: `POST /v1/usage/events` + batch + idempotency unique + summary + events list + báo cáo shadow (usage × định mức) | Idempotent replay, summary khớp | tests pass |
| **P4** | Client mỏng ShopERP `MeteringApiClient` (pattern GatewayAdminApiClientBase, timeout 3s + circuit breaker) + **4 hook nguồn event:** (a) public ingest `POST /api/public/metering/event` trên Gateway ← page view `Store.razor` (WASM, tenantId theo store-id trong URL) ; (b) `MessagingHub`/`RealtimeController.SendMessageAsync` → `chat.message`+`chat.bytes`; (c) Directory SSR (timlathay.com) → `directory.search`/`directory.redirect`; (d) hook `BookingService` + report tài chính + nghiệp vụ kho → `feature.*` | Event chạy production → summary có data đủ 4 nguồn | RV |
| **P5** | Deploy + RV: thêm vào `cd-multivps.yml` deploy_set · chạy container trên `vanan-shop-a` (hoặc VPS riêng nếu chốt) · smoke `/v1/health` + ingest e2e | CD pass, health 200 | RV L1-L2 |
| **P6** | Admin UI rate card (ShopERP `/admin/metering/rate-cards` — GatewayAdminApiClientBase pattern hoặc trang riêng trong metering service) | SystemAdmin sửa được giá | RV |
| **P7** | **Ví quota + enforce + top-up:** `CreditWallet` + `CreditLedgerEntry` append-only (Grant/Deduct/Adjust) · deduct tại ingest (không trừ quá số dư, flag `unpaid`) · `check?serviceCode=` deny API (caller cache 30s) · `WalletAlert` ngưỡng 50%/90% cấu hình + `IAlertNotifier` · `TopupConfig` (số TK/tên thụ hưởng/ngân hàng) + `TopupRequest` + QR payload VietQR sinh local · admin confirm/reject (idempotent) · ShopERP UI: banner cảnh báo + màn hình top-up QR + admin xác nhận | tests concurrency + e2e top-up | tests + RV |

### GĐ-2 — TRUNG BÌNH (hold/capture + ledger đa nguồn + pilot)

| Phase | Nội dung | Gate |
|---|---|---|
| **P8** | `CreditHold` + state machine hold→capture/release (TTL reaper) + idempotency 2 bước · chuyển deduct-at-ingest → hold/capture cho task nặng | tests concurrency: không trừ quá số dư |
| **P9** | Grants đa nguồn (pool/earned) + refunds + quota/hạn mức kỳ + cảnh báo quota → NATS `vanan.metering.alert` | tests |
| **P10** | NATS `vanan.metering.balance.{shopInstanceId}` + `MeteringSyncSubscriber` mirror SQLite ShopERP (pattern LoyaltySyncSubscriber) | mirror khớp |
| **P11** | ShopERP UI: số dư theo nguồn + lịch sử + cảnh báo quota; Gateway admin: cấp/điều chỉnh credit | RV |
| **P12** | Pilot: 1 HTX/tenant test + nhóm CTV · hoàn credit · khiếu nại | gate pilot policy |

### GĐ-3 — HOÀN THIỆN (khắc khe kỹ thuật/pháp lý/kế toán/kinh doanh)

| Phase | Nội dung | Gate |
|---|---|---|
| **P13** | Migrate SQLite→PG + backup/PITR + CallerKey→tenant mapping + webhooks | scale check |
| **P14** | HTX Pool allocation + approval đa cấp + budget theo kỳ + clawback | chốt §14.11-16 |
| **P15** | Đối soát job (usage↔ledger↔invoice) + export kế toán theo ma trận §11.1 **đã ký** | kế toán ký duyệt |
| **P16** | Fraud rules (map FraudFlagType/RiskScore) + expiry/revoke + DLQ + DR drill | security review |
| **P17** | Thu tiền thật tự động: tích hợp `Order.SourceType="credit_topup"` → OrderFlow + PaymentCompliance + QRChannel + e-invoice (thay thế top-up thủ công P7) | gate pháp lý cuối |

**Dependency:** P0 → P1→P2→P3 (song song được P2/P3) → P4 → P5 → P6 → P7 || GĐ-1 GATE → P8→P9→P10→P11→P12 || GĐ-2 GATE → P13→P14→P15→P16→P17.

---

## 6. THIẾT KẾ CHI TIẾT

### 6.1 Vị trí code

```
8_Metering/vanan-metering/        # module riêng — KHÔNG ref 1_Shared/3_CoreHub
  src/
    VanAn.Metering.Api/           # minimal API + auth API key
    VanAn.Metering.Domain/        # UsageEvent, RateCardVersion, CreditWallet, CreditLedgerEntry, CreditHold
    VanAn.Metering.Infra/         # EF Core (SQLite GĐ-1/2 → PG GĐ-3)
    VanAn.Metering.Contracts/     # DTO API contract (publish cho hệ thống khác)
5_WebApps/ShopERP/Services/
    MeteringApiClient.cs          # client mỏng (pattern GatewayAdminApiClientBase)
```

### 6.2 Luồng GĐ-1: check → deduct → top-up

```
Caller (hook) → MeteringApiClient.CheckAsync(tenantId, serviceCode)   [cache 30s]
  → deny=false: phục vụ + ghi UsageEvent (batch) → metering deduct ví (quantity × định mức)
  → deny=true: trả thông báo hết số dư + nút "Nạp phí nền tảng"
User top-up → POST /v1/topups → QR VietQR (số TK/tên thụ hưởng/ngân hàng từ TopupConfig)
  → user CK kèm referenceCode → SystemAdmin thấy tiền trong sao kê → confirm → ví +credit → alert reset
Deduction vượt ngưỡng 50%/90% → WalletAlert → banner ShopERP + IAlertNotifier
```

### 6.3 Luồng task trả phí (GĐ-2)

```
ShopERP task runner
  → MeteringApiClient.AuthorizeAsync(serviceCode, estCredits, taskRef, idemKey)   [≤50ms]
  → chạy task (OCR/AI/report)
  → thành công: CaptureAsync(holdId, actualCredits)   |   lỗi: ReleaseAsync(holdId)
  → UsageEvent ghi qua events:batch (async)
  → NATS balance → mirror ShopERP hiển thị
```

### 6.4 Rate card resolve

`định mức áp dụng = RateCardVersion WHERE ServiceCode=@code AND EffectiveFrom<=@occurredAtUtc AND (EffectiveTo IS NULL OR EffectiveTo>@occurredAtUtc) AND IsActive` — snapshot vào `CreditHold.EstimatedCredits` tại authorize (R-3).

### 6.5 RV (theo runtime-verification rules)

- **L1:** image `vanan-metering:<sha>` trên GHCR + container healthy trên VPS + `/v1/health` 200.
- **L2:** ingest test event qua API key → summary có count; rate card CRUD qua admin API.
- **L3:** E2E production — task mẫu (export Excel) ghi usage → summary tăng.
- **L4:** flow đầy đủ GĐ-2: authorize→capture→balance mirror→lịch sử.
- **L5:** manual (user).

---

## 7. RỦI RO

| # | Rủi ro | Giảm thiểu |
|---|---|---|
| R-1 | Monorepo thêm project → CI chậm | `8_Metering` tách solution riêng `VanAn.Metering.sln`, CI build image độc lập |
| R-2 | Metering down → task trả phí kẹt | Circuit breaker client + fail-list duyệt trước + deny/queue (SRS [G9]) |
| R-3 | Top-up thủ công = tiền thật trước gate kế toán | Admin confirm chỉ khi thấy tiền trong sao kê; referenceCode khớp nội dung CK; TopupRequest+ledger = chứng từ; cảnh báo §11.1 trước khi mở production |
| R-4 | Scope creep HTX pool | Giới hạn MVP = budget cap (policy §4.2); phân phối phức tạp defer GĐ-3 P13 |
| R-5 | Thu tiền sớm hơn gate | Hard rule: không code payment integration trước ký ma trận §11.1 |

---

## 8. TASK CARDS (tạo khi user approve)

`docs/AI/tasks/phi_nen_tang_metering/` — `task_p0_analyze.md` · `task_p1_skeleton.md` · `task_p2_ratecard.md` · `task_p3_ingest.md` · `task_p4_client.md` · `task_p5_deploy_rv.md` · `task_p6_admin_ui.md` · `task_p7_wallet_topup.md` (GĐ-2/3 tạo sau khi qua gate tương ứng).
