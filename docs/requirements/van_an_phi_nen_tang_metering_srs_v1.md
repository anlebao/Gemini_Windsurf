# SRS — PHÍ NỀN TẢNG METERING SERVICE (`vanan-metering`) v1.0

> **Ngày:** 2026-10-23 · **Trạng thái:** DRAFT v1.0 — khớp Policy v1.3 (`docs/requirements/Proposal_Phi_Nen_Tang_Policy_v1.3.md`)
> **Nền tảng:** Module độc lập, container/VPS riêng — KHÔNG nhúng codebase Vạn An; tích hợp qua HTTP API contract
> **Nguyên tắc:** "những điều chưa rõ → giả định kịch bản ĐƠN GIẢN NHẤT" (mọi giả định đánh dấu **[G#]**) · Ma trận kế toán §11.1 policy = **OPEN** — không implement bút toán khi chưa ký duyệt

---

## 1. BỐI CẢNH & MỤC TIÊU

**Yêu cầu người dùng (23/10/2026):** module metering **độc lập, ít dính mắc với code hiện tại**; có thể gắn vào hệ thống khác để tính phí dịch vụ; chạy riêng trên docker container, thậm chí VPS riêng; roadmap 3 giai đoạn (MVP đơn giản nhất → trung bình → hoàn thiện).

**Mục tiêu SRS:** đặc tả `vanan-metering` — service đo lường usage + Rate Card + (GĐ-2) ví credit/ledger — đủ để: (a) Vạn An dùng làm Lớp 1–2 của Phí nền tảng; (b) hệ thống bên ngoài gắn vào tính phí mà không phụ thuộc code Vạn An.

---

## 2. HIỆN TRẠNG (khảo sát codebase 2026-10-22 — đã xác minh)

| Thành phần | Hiện trạng | Kết luận |
|---|---|---|
| Usage metering | **KHÔNG tồn tại** — không service/entity nào đo tài nguyên per task | ❌ BUILD |
| Charge-per-use | `CollaboratorVerificationService`: check số dư → gửi SMS OTP → trừ phí flat `WalletTransaction(SmsOtpFee, -fee)`; phí cấu hình `SystemSetting.SmsOtpFeePerVerification` | 🟡 Precedent — tham khảo, KHÔNG tái sử dụng trực tiếp (module tách) |
| Immutable ledger pattern | `WalletTransaction`, `AllianceWallet`/`AllianceTransaction` — append-only, idempotencyKey, NATS sync mirror SQLite | ✅ Pattern để clone cho credit ledger (GĐ-2) |
| Idempotency | `Idempotency-Key` header (booking POST), `idempotencyKey` param (alliance) | ✅ REUSE pattern |
| Rate limiting | `EnableRateLimiting` Gateway + ShopERP — đếm request để throttle, không persist | 🟡 Đếm được nhưng không phải usage record |
| OCR | `IOcrEngine`/`IOcrConfigService` đang xây (docs/AI/tasks/ocr_hub) — chưa metering | 🟡 Gắn meter ngay từ đầu |
| AI/LLM | Chỉ `IEmbeddingService` (semantic search) — không track token | ❌ Chưa có gì để meter |
| Tenant context | `TenantId` (Guid), `ShopInstanceId` — multi-VPS routing | ✅ Truyền qua API claim |
| Alerts | `IAlertNotifier` + Telegram/Zalo stub | ✅ REUSE cho cảnh báo số dư (GĐ-2) |
| Auth service-to-service | API key pattern + JWT (StaffJwtValidator); `GatewayAdminApiClientBase` cho admin API | ✅ Pattern client mỏng |
| **Chat** | `MessagingHub` (SignalR, Gateway) + `RealtimeController.SendMessageAsync` + `CommunityController` chat — message + payload size đo được server-side | ✅ Hook metering tốt |
| **Storefront tenant** | KhachLink `Store.razor` (WASM — public, không auth) — page view phía client | 🟡 Event client-side → cần public ingest (G15) |
| **Directory timlathay.com** | Blazor SSR — search + redirect tới storefront chạy server-side | ✅ Hook SSR trực tiếp |
| **Feature modules** | Booking (`BookingService`), tài chính (`/financial` — JournalReport/CongNo…), kho (`/inventory/dashboard`, ingredients/forecast) — đều server-side ShopERP | ✅ Hook per-action |

---

## 3. PHẠM VI

### 3.1 Trong phạm vi (theo 3 giai đoạn — policy §12.1)

| GĐ | Thành phần | Ghi chú |
|---|---|---|
| **GĐ-1 MVP** | Ingest usage event · counter per tenant/service/kỳ · Rate Card CRUD (SystemAdmin) · **ví credit quota**: admin cấp, trừ credit tại ingest · **cảnh báo ví còn 50%/10%** (dùng 50%/90%, ngưỡng cấu hình) · **deny khi hết số dư** + thông báo + hướng dẫn top-up · **top-up thủ công VietQR** (config số TK/tên thụ hưởng/ngân hàng; admin xác nhận → grant) · báo cáo usage + shadow · API client mỏng ShopERP + public ingest qua Gateway. **Nguồn event MVP (user chốt):** visit storefront · chat message + payload · search/redirect từ timlathay.com · feature usage booking/financial/inventory | Ví quota + enforce nhẹ — **top-up thủ công vẫn là nhận tiền thật** (cần chứng từ tối thiểu) |
| **GĐ-2 Trung bình** | Hold/Capture/Release + state machine · ledger đa nguồn (purchased/pool/earned) · hạn mức kỳ + cảnh báo quota · hoàn credit · lịch sử giao dịch · mirror read-only về ShopERP | Pilot 1 HTX + nhóm tenant |
| **GĐ-3 Hoàn thiện** | HTX Pool + Earned credits + duyệt đa cấp · đối soát định kỳ · tích hợp kế toán (theo ma trận đã ký) · fraud rules · credit expiry/thu hồi · backup/DR · scale VPS riêng · **thu tiền thật** | Sau khi xác minh pháp lý/kế toán |

### 3.2 Ngoài phạm vi (ghi nợ — KHÔNG làm)

- Thanh toán tự động / tích hợp payment gateway / tự động đối chiếu sao kê ngân hàng trong GĐ-1, GĐ-2 (top-up GĐ-1 = chuyển khoản thủ công VietQR, admin xác nhận tay).
- Quy đổi credit ↔ tiền mặt, chuyển nhượng credit giữa người dùng.
- Metering cho buyer/KhachLink customer (loại khỏi MVP — policy §6).
- Meter per-request cho API calls nhẹ (dùng quota/counter theo kỳ — §9.1.5).
- Tự động đồng bộ ngược usage → sổ kế toán tenant HKD (sổ phí là của pháp nhân nền tảng — §11).

---

## 4. GIẢ ĐỊNH — KỊCH BẢN ĐƠN GIẢN NHẤT

| # | Giả định | Mô tả |
|---|---|---|
| [G1] | **Service độc lập hoàn toàn** | `vanan-metering` = .NET 8 minimal API + EF Core, folder `8_Metering/` trong monorepo (solution riêng `VanAn.Metering.sln`), docker image riêng `ghcr.io/anlebao/vanan-metering`, DB riêng. Không reference `1_Shared`/`3_CoreHub` — chỉ API contract. **CHỐT 23/10/2026 (O4/O5).** |
| [G2] | **SQLite cho GĐ-1/2, PostgreSQL cho GĐ-3** | MVP đơn giản nhất → SQLite file trong volume container; GĐ-3 chuyển PG (migrate) khi cần concurrent writes/scale hoặc gắn nhiều hệ thống. |
| [G3] | **Auth = API key per hệ thống caller** | Mỗi hệ thống gọi (ShopERP-A, Gateway, hệ thống khác) có 1 API key (`X-Metering-Key`) + `X-Caller-Id`. Tenant context = claim `tenantId`/`shopInstanceId` trong body — service KHÔNG join bảng Tenant Vạn An. |
| [G4] | **Credit = đơn vị định mức, không phải tiền** | Service chỉ biết credit + định mức; mọi giao dịch tiền VND nằm ngoài scope (Lớp 3 — hệ thống gọi tự xử lý). |
| [G5] | **Time source = server UTC** | Kỳ (period) tính theo tháng UTC; caller gửi `occurredAtUtc`, server chấp nhận ±5 phút skew, ngoài ngưỡng vẫn ghi nhưng flag `lateEvent=true`. |
| [G6] | **Idempotency bắt buộc mọi mutation** | `Idempotency-Key` unique per (caller, operation); trùng key → trả kết quả lần đầu (200 + `idempotentReplay=true`), không xử lý lại. Unique index DB. |
| [G7] | **Rate Card versioned** | Item có `ServiceCode`, `MeterUnit`, `CreditRate`, `EffectiveFrom`, `EffectiveTo`, `IsActive`. Định mức áp dụng = version hiệu lực tại `occurredAtUtc`. Sửa/xóa = tạo version mới / soft-deactivate — KHÔNG sửa dữ liệu lịch sử. |
| [G8] | **Hold có TTL** | Hold hết hạn tự release (default 10 phút, cấu hình). Một hold chỉ capture 1 lần; capture idempotent. |
| [G9] | **Metering chết ≠ hệ thống chết** | Caller phải có timeout (đề xuất 3s) + circuit breaker; theo policy danh sách fail-open được duyệt trước. Service không quyết định fail-open — caller quyết. |
| [G10] | **Báo cáo đọc trực tiếp** | Không materialized view trong MVP; aggregate on-demand (usage table ít, < vài triệu rows). |
| [G11] | **Mirror về ShopERP (GĐ-2)** | Publish balance/usage qua NATS subject `vanan.metering.balance.{shopInstanceId}` — ShopERP subscribe ghi mirror SQLite chỉ-đọc (pattern `LoyaltySyncSubscriber`). GĐ-1 bỏ qua. |
| [G12] | **Ledger credit trong DB metering** | GĐ-2: `CreditLedgerEntry` append-only trong DB riêng của metering — **CHỐT 23/10/2026 (O1)**; đối soát với sổ kế toán PG qua export/report, không FK chéo. CreditWallet riêng = Option A — **CHỐT (O2)**, HTX Pool/Earned là quyền lợi không-tiền-backing nên không dùng ví VND. |
| [G13] | **Single-identity pattern** | Nếu dùng EF Core: PK `Id` duy nhất, KHÔNG dual-identity (bài học Single-Identity Pattern governance). |
| [G14] | **GĐ-1 có ví quota, chưa có hold/capture** | MVP đo + đếm + rate card + **ví credit trừ tại ingest** (deduct = quantity × định mức). Hold/capture state machine từ GĐ-2 — API shape thiết kế ngay nhưng trả 501 ở GĐ-1. |
| [G18] | **Cảnh báo ví 2 ngưỡng (GĐ-1)** | Ngưỡng mặc định: đã dùng **50%** và **90%** hạn mức ví (còn 50%/10%) — cấu hình được per tenant/system. Khi deduction làm vượt ngưỡng → ghi `WalletAlert` + trả `alertLevel` trong response check/balance → ShopERP hiển thị banner + `IAlertNotifier` (Telegram/Zalo stub). Mỗi ngưỡng chỉ alert 1 lần/kỳ ví (reset khi top-up). |
| [G19] | **Top-up thủ công VietQR (GĐ-1)** | `TopupConfig` (SystemAdmin CRUD): `BankCode/BankName`, `AccountNumber`, `AccountName` (tên thụ hưởng). Flow: user bấm top-up → `POST /v1/topups` tạo `TopupRequest` {referenceCode=`TOPUP-{walletId|short}-{seq}`, amountVnd?} → service trả QR payload (chuỗi EMV/VietQR sinh local, không gọi API ngoài) → user quét chuyển khoản kèm referenceCode → SystemAdmin `POST /v1/topups/{id}/confirm` → ledger `Grant` + cộng ví. **Quy đổi VND→credit do admin quyết lúc confirm** (không gắn 1 credit = X VND cố định — giữ nguyên tắc credit ≠ tiền); hiển thị gợi ý nếu có cấu hình. Reject → ghi lý do. **Tiền thật vào TK công ty → bắt buộc đối chiếu sao kê tay + chứng từ (gate kế toán §11.1 policy).** |
| [G20] | **Deny theo enforcement per serviceCode (GĐ-1)** | `RateCardVersion.Enforcement = MeterOnly | Deny`. Caller check `GET /v1/wallets/{tenantId}/check?serviceCode=` trước khi phục vụ → `{balance, alertLevel, deny, topup:{referenceHint, qrAmount?}}`. **CHỐT 23/10/2026 (O-4):** `feature.*`, `chat.*` = Deny; `store.visit`, `directory.*` = MeterOnly (**không chặn traffic bán hàng của tenant**). Check phải cache được phía caller (TTL ~30s) để không tạo hot path. |
| [G15] | **Event phía client qua public ingest** | Page view storefront (WASM) không giữ API key → client gọi public endpoint trên Gateway (`POST /api/public/metering/event`, rate-limited, không auth user) → Gateway forward vào metering với caller credential. Event phía server (chat, feature, referral SSR) hook trực tiếp. |
| [G16] | **Anti-bot / attribution** | Visit/referral chỉ đếm event "hợp lệ": lọc bot theo User-Agent + fingerprint thô (MVP), nâng cấp fraud rules GĐ-3. Referral = **lượt click redirect** từ timlathay.com tới storefront (đếm được, khó gian lận hơn impression). |
| [G17] | **Event tần suất cao → batch + counter** | Visit/chat/referral là event nhẹ tần suất cao → client/caller buffer + `events:batch`, counter theo kỳ; KHÔNG hold/capture (§9.1.5 policy). |

---

## 5. KIẾN TRÚC

```
┌──────────────┐    ┌──────────────┐    ┌──────────────────┐
│  ShopERP-A   │    │   Gateway    │    │ Hệ thống khác X  │
│ (client mỏng │    │ (client mỏng│    │ (client riêng)   │
│  MeteringApi │    │  MeteringApi │    │                  │
│   Client)    │    │   Client)    │    │                  │
└──────┬───────┘    └──────┬───────┘    └────────┬─────────┘
       │ HTTP + X-Metering-Key + Idempotency-Key │
       ▼                   ▼                     ▼
┌──────────────────────────────────────────────────────────┐
│              vanan-metering (container riêng)            │
│  ┌─────────┐  ┌─────────┐  ┌────────┐  ┌──────────────┐  │
│  │ Ingest  │  │ RateCard│  │ Hold/  │  │  Reports/    │  │
│  │ Usage   │  │ CRUD    │  │ Capture│  │  Shadow calc │  │
│  └────┬────┘  └────┬────┘  └───┬────┘  └──────┬───────┘  │
│       └────────────┴───────────┴──────────────┘           │
│                    DB riêng (SQLite → PG)                 │
└──────────────┬───────────────────────────────────────────┘
               │ NATS (GĐ-2): vanan.metering.balance.{shopInstanceId}
               ▼
         ShopERP mirror (read-only SQLite — hiển thị)
```

**Nguyên tắc boundary:** metering service không gọi ngược vào hệ thống Vạn An (one-way: caller → metering; metering → NATS broadcast). Không FK, không shared DB, không shared code với `1_Shared`/`3_CoreHub`.

---

## 6. API CONTRACT (v1)

Auth: header `X-Metering-Key: <api-key>` + `X-Caller-Id: <shoperp-a|gateway|...>`. Mọi mutation: `Idempotency-Key` header.

### 6.1 GĐ-1 (MVP)

| Method | Path | Mô tả |
|---|---|---|
| POST | `/v1/usage/events` | Ghi 1 usage event `{tenantId, shopInstanceId?, serviceCode, quantity, unit, occurredAtUtc, requestedBy?, taskRef?, metadata?}` → `{eventId, idempotentReplay?}` |
| POST | `/v1/usage/events:batch` | Ghi batch ≤500 events cùng format (mỗi event kèm `idempotencyKey` riêng) |
| GET | `/v1/usage/summary?tenantId=&period=YYYY-MM` | Counter: Σ quantity theo service + credits tính giả lập theo rate card hiệu lực |
| GET | `/v1/usage/events?tenantId=&serviceCode=&from=&to=&page=` | Lịch sử usage phân trang |
| POST | `/v1/rate-cards` | SystemAdmin tạo hạng mục `{serviceCode, meterUnit, creditRate, effectiveFrom, effectiveTo?, label}` |
| PATCH | `/v1/rate-cards/{id}` | Sửa định mức → sinh version mới (`EffectiveFrom` mới), version cũ tự đóng |
| DELETE | `/v1/rate-cards/{id}` | Soft-deactivate (`IsActive=false`) — không xóa lịch sử |
| GET | `/v1/rate-cards?includeInactive=` | Danh sách + version history |
| GET | `/v1/wallets/{tenantId}` | Số dư + `alertLevel` (ok/warn50/warn90/depleted) + hạn mức ví |
| GET | `/v1/wallets/{tenantId}/check?serviceCode=` | **Deny check** trước khi phục vụ `{balance, deny, alertLevel, topup:{accountRef?}}` — caller cache ~30s |
| POST | `/v1/wallets/{tenantId}/grants` | Admin cấp credit `{source, credits, reason, topupRequestId?}` → ledger Grant |
| GET | `/v1/wallets/{tenantId}/ledger` | Lịch sử ledger phân trang |
| GET/PUT | `/v1/topup-config` | SystemAdmin xem/sửa `{bankCode, bankName, accountNumber, accountName}` — singleton per caller system |
| POST | `/v1/topups` | Tạo `TopupRequest` `{tenantId, amountVnd?}` → `{topupId, referenceCode, qrPayload}` (VietQR/EMV sinh local) |
| POST | `/v1/topups/{id}/confirm` | SystemAdmin xác nhận đã nhận tiền `{credits, note?}` → grant credit (idempotent) |
| POST | `/v1/topups/{id}/reject` | Từ chối `{reason}` |
| GET | `/v1/topups?status=&tenantId=` | Danh sách yêu cầu top-up |
| GET | `/v1/health` | Health check cho docker/CD smoke |

**Public ingest path (GĐ-1):** event phía client (page view storefront) → `POST /api/public/metering/event` trên Gateway (không auth user, rate-limited, validate tenantId hợp lệ) → Gateway forward vào `POST /v1/usage/events` với caller credential. ServiceCodes MVP: `store.visit` · `chat.message` (+`chat.bytes`) · `directory.search` · `directory.redirect` · `feature.booking` · `feature.financial` · `feature.inventory`.

Response lỗi chuẩn: `{error: {code, message, retryable}}` — caller dùng `retryable` để quyết retry.

### 6.2 GĐ-2 (Trung bình) — thêm

| Method | Path | Mô tả |
|---|---|---|
| POST | `/v1/holds` | `{tenantId, serviceCode, estimatedCredits, taskRef}` → `{holdId, expiresAtUtc}` — kiểm tra số dư + budget → giữ credit |
| POST | `/v1/holds/{id}/capture` | `{actualCredits}` → trừ thật vào ví + ledger; idempotent |
| POST | `/v1/holds/{id}/release` | Giải phóng (task fail/cancel); idempotent |
| POST | `/v1/wallets/{tenantId}/refunds` | Hoàn credit khi task lỗi `{holdId?, credits, reason}` |
| GET | `/v1/quotas/{tenantId}` | Hạn mức kỳ + mức dùng hiện tại + % |

### 6.3 GĐ-3 (Hoàn thiện) — thêm

- `POST /v1/pools/{htxTenantId}/allocations` — cấp budget cho member/tenant con, multi-level approval workflow.
- `POST /v1/settlements:run` — job đối soát định kỳ (usage ↔ ledger ↔ invoice ref) → report.
- `GET /v1/export/accounting?period=` — export bút toán theo ma trận §11.1 đã ký.
- `POST /v1/credits/expire`, `/v1/credits/revoke` — hết hạn / thu hồi gian lận.
- Webhooks `POST /v1/webhooks` — đăng ký callback (balance.low, hold.expired…) cho caller.

### 6.4 Client mỏng phía ShopERP

`5_WebApps/ShopERP/Services/MeteringApiClient.cs` — pattern `GatewayAdminApiClientBase`: inject `HttpClient` base address `Metering:BaseUrl` + header key từ config. Interface `IMeteringClient` (GĐ-1: `RecordUsageAsync`, `GetSummaryAsync`; GĐ-2: `AuthorizeAsync`, `CaptureAsync`, `GetBalanceAsync`). Timeout 3s + Polly-style circuit breaker; khi circuit open → trả `MeteringUnavailable` cho caller tự quyết fail-open.

---

## 7. DATA MODEL (GĐ-1/2)

| Bảng | Cột chính | Ràng buộc |
|---|---|---|
| `UsageEvent` | Id (PK) · TenantId · ShopInstanceId? · ServiceCode · Quantity (decimal) · Unit · OccurredAtUtc · ReceivedAtUtc · RequestedBy? · TaskRef? · MetadataJson? · LateEvent (bool) | Append-only · index (TenantId, OccurredAtUtc), (ServiceCode, period) · **unique (CallerId, IdempotencyKey)** |
| `RateCardVersion` | Id · ServiceCode · MeterUnit · CreditRate (decimal) · EffectiveFrom/To · IsActive · Label · ChangedBy · ChangedAtUtc · AuditNote? | Append-only versions · unique (ServiceCode, EffectiveFrom) |
| `CreditWallet` (GĐ-1) | Id · TenantId · BalancePurchased · BalancePool · BalanceEarned (GĐ-1 chỉ dùng `BalancePurchased`; Pool/Earned từ GĐ-3) · GrantedTotal · UpdatedAtUtc | unique TenantId · update chỉ qua service |
| `CreditLedgerEntry` (GĐ-1) | Id · TenantId · Type (Grant/**Deduct**/Hold/Capture/Release/Refund/Adjust/Expire/Revoke — GĐ-1 dùng Grant/Deduct/Adjust) · Source (Purchased/Pool/Earned) · Credits (signed) · BalanceAfter · HoldId? · UsageEventId? · TopupRequestId? · Reason · CreatedBy · CreatedAtUtc | **Append-only** — không update/delete; điều chỉnh = entry đảo |
| `TopupConfig` (GĐ-1) | Id · CallerSystem? · BankCode · BankName · AccountNumber · AccountName · UpdatedBy · UpdatedAtUtc | Singleton per caller system · sửa → AuditLog |
| `TopupRequest` (GĐ-1) | Id · TenantId · ReferenceCode (unique) · AmountVnd? · CreditsGranted? · Status (Pending/Confirmed/Rejected/Expired) · ConfirmedBy? · ConfirmedAtUtc · Note? · CreatedAtUtc | unique (CallerId, IdempotencyKey) · confirm/reject idempotent |
| `WalletAlert` (GĐ-1) | Id · TenantId · Level (Warn50/Warn90/Depleted) · CycleKey (reset khi top-up) · FiredAtUtc · NotifiedChannels | unique (TenantId, Level, CycleKey) — mỗi ngưỡng 1 lần/chu kỳ |
| `CreditHold` (GĐ-2) | Id · TenantId · ServiceCode · EstimatedCredits · ActualCredits? · Status (Active/Captured/Released/Expired) · TaskRef · ExpiresAtUtc · CreatedAtUtc | unique (CallerId, IdempotencyKey) · index Status+ExpiresAtUtc cho reaper |
| `CallerKey` | Id · CallerId · KeyHash · IsActive · CreatedAtUtc | Admin-only manage |
| `AuditLog` | Id · Actor · Action · EntityType/Id · OldJson/NewJson · AtUtc | Append-only |

---

## 8. YÊU CẦU CHỨC NĂNG (FR)

| # | FR | GĐ | AC |
|---|---|---|---|
| FR-1 | Ingest usage single/batch, idempotent | 1 | Gửi trùng key → replay, không double-count trong summary |
| FR-1a | Visit storefront (client event qua public ingest) | 1 | Page load `Store.razor` → event `store.visit` có tenantId, loại bot [G16]; event giả (spoof tenantId) bị chặn validate |
| FR-1b | Chat metering (server-side) | 1 | `MessagingHub`/`RealtimeController.SendMessageAsync` → event `chat.message` + `chat.bytes` (payload KB) per tenant |
| FR-1c | Directory search + redirect (timlathay.com SSR) | 1 | Search → `directory.search`; click tới storefront → `directory.redirect` với tenantId đích |
| FR-1d | Feature usage (server-side ShopERP) | 1 | Booking create → `feature.booking`; báo cáo tài chính chạy → `feature.financial`; nghiệp vụ kho → `feature.inventory` |
| FR-2 | Counter theo tenant/service/kỳ + credits giả lập theo rate card hiệu lực tại occurredAt | 1 | Summary khớp Σ events × định mức version đúng thời điểm |
| FR-3 | Rate Card CRUD versioned | 1 | Sửa giá → version mới; event quá khứ vẫn tính theo giá cũ; audit log đầy đủ |
| FR-4 | Báo cáo usage + shadow billing | 1 | GET summary trả usage + credits tính giả lập (song song ví thật) |
| FR-13 | Trừ credit tại ingest theo rate card | 1 | Event ingest → ledger `Deduct` = quantity × định mức; không trừ quá số dư (event vẫn ghi, phần thiếu flag `unpaid`) |
| FR-14 | Cảnh báo ví còn 50%/10% (dùng 50%/90%) | 1 | Deduction vượt ngưỡng → `WalletAlert` + `alertLevel` trong check/balance + notify qua `IAlertNotifier`; mỗi ngưỡng 1 lần/chu kỳ, reset khi top-up |
| FR-15 | Deny khi hết số dư + hướng dẫn top-up | 1 | `check?serviceCode=` với `Enforcement=Deny` và balance < định mức → `deny:true` + payload hướng dẫn top-up (QR) |
| FR-16 | Top-up thủ công VietQR | 1 | Config TK/thụ hưởng/ngân hàng → QR payload local → referenceCode unique → admin confirm → ví +credit; confirm idempotent, replay không cộng 2 lần |
| FR-5 | Hold/Estimate → Capture/Actual → Release | 2 | Hold hết hạn tự release; capture idempotent; không trừ quá số dư |
| FR-6 | Ví + ledger bất biến đa nguồn | 2 | Số dư = Σ ledger theo source; không sửa dòng cũ; adjust = đảo |
| FR-7 | Hạn mức kỳ + cảnh báo quota | 2 | Vượt ngưỡng quota → event NATS `vanan.metering.alert` (ShopERP hiển thị / AlertNotifier) |
| FR-8 | Hoàn credit khi task lỗi | 2 | Refund tạo ledger entry +, link hold/taskRef |
| FR-9 | Mirror balance về ShopERP | 2 | NATS `vanan.metering.balance.{shopInstanceId}` → mirror chỉ-đọc |
| FR-10 | HTX Pool + Earned + phê duyệt | 3 | Allocation có người duyệt, kỳ, mục đích; clawback chưa dùng |
| FR-11 | Đối soát + export kế toán | 3 | Report chênh lệch usage↔ledger; export theo ma trận đã ký |
| FR-12 | Fraud rules + expiry/revoke | 3 | Rule từ fraud engine map sang; thu hồi ghi lý do + audit |

---

## 9. YÊU CẦU PHI CHỨC NĂNG (NFR)

| # | NFR | Ngưỡng |
|---|---|---|
| NFR-1 | Authorize p95 (GĐ-2) / Balance-check p95 (GĐ-1) | ≤50ms (LAN/VPS cùng region); timeout caller 3s; caller cache check ~30s |
| NFR-9 | QR top-up | Payload VietQR/EMV sinh local trong service — không phụ thuộc API bên ngoài; referenceCode unique, không trùng qua retry |
| NFR-2 | Ingest throughput (GĐ-1) | ≥100 events/s SQLite batch; mục tiêu GĐ-3 ≥1k events/s trên PG |
| NFR-3 | Usage loss | ≈0 — caller retry + batch buffer; DLQ cho event fail sau retry |
| NFR-4 | Availability | Metering down không làm down caller (circuit breaker); chức năng trả phí degrade theo fail-list |
| NFR-5 | Audit | Mọi rate-card change, grant, adjust, revoke → AuditLog append-only |
| NFR-6 | Deploy | Image riêng trong CD pipeline; `/v1/health` cho smoke test; config qua env vars |
| NFR-7 | Backup | GĐ-1: file SQLite trong volume + backup ngày; GĐ-3: PG backup + PITR |
| NFR-8 | Bảo mật | API key hash lưu DB; TLS qua nginx; không chứa PII ngoài tenantId/userId tối thiểu |

---

## 10. RỦI RO & MỞ

| # | Rủi ro | Giảm thiểu |
|---|---|---|
| R-1 | Ledger ngoài PG → đối soát 2 nguồn | Job đối soát GĐ-3 + báo cáo chênh lệch; quyết định §14.18 policy |
| R-2 | Clock skew caller→service | [G5] chấp nhận ±5', flag lateEvent |
| R-3 | Rate card đổi giữa chừng tác vụ | Định mức chốt tại authorize/hold (snapshot vào CreditHold), không tra lại khi capture |
| R-4 | NATS mirror lag → số dư hiển thị sai | Mirror chỉ-đọc, quyết định luôn qua API (§9.1.1) |
| R-5 | Caller tự gửi tenantId sai | CallerId ↔ allowed tenants mapping ở `CallerKey` config (GĐ-2+) |
| O-1 | ✅ CHỐT 23/10/2026 — Option A: CreditWallet riêng trong metering (HTX Pool/Earned không có tiền backing → không dùng ví VND) | Thiết kế FR-5+ giữ nguyên |
| O-2 | ✅ CHỐT 23/10/2026 — danh mục MVP = traffic storefront + chat + referral timlathay.com + feature booking/financial/inventory | FR-1a..1d |
| O-3 | ✅ CHỐT 23/10/2026 — booking **per-action** (mỗi booking tạo); financial/inventory **module-active-month** | Rate card P2 áp đúng mô hình đã chốt |
| O-4 | ✅ CHỐT 23/10/2026 — `feature.*`+`chat.*` = **Deny**; `store.visit`/`directory.*` = **MeterOnly** (không chặn traffic bán hàng) | [G20] thành spec chính thức |
| R-6 | Top-up thủ công GĐ-1 = tiền thật vào TK công ty trước gate kế toán GĐ-3 | Đối chiếu sao kê tay khi confirm; TopupRequest + ledger = chứng từ; cảnh báo pháp lý/kế toán (§11.1 policy) trước khi mở top-up production |
| R-7 | User gian lận top-up (claim đã CK nhưng chưa) | Admin chỉ confirm khi thấy tiền trong sao kê; referenceCode bắt buộc khớp nội dung CK |

---

## 11. TIÊU CHÍ NGHIỆM THU (per GĐ)

- **GĐ-1:** container chạy độc lập · ingest + batch idempotent · rate card CRUD versioned qua UI/admin API · summary khớp dữ liệu test · 0 usage mất trên bộ test replay · **đủ 4 nguồn event chảy production-like:** visit storefront (qua public ingest) · chat message · redirect timlathay.com · ≥1 feature event (booking/financial/inventory) · **ví: trừ credit đúng định mức, không trừ quá số dư dưới test đồng thời** · **cảnh báo khi dùng 50%/90% (1 lần/ngưỡng/chu kỳ)** · **deny trả đúng payload hướng dẫn top-up** · **top-up e2e: config TK → QR payload → confirm → credit vào ví (idempotent)**.
- **GĐ-2:** hold→capture→release đúng state machine · ledger đa nguồn · hoàn credit khi lỗi · cảnh báo quota kỳ · mirror balance về ShopERP · pilot tenant thật.
- **GĐ-3:** HTX pool allocation + approval · đối soát report · export kế toán theo ma trận đã ký · fraud rule hoạt động · backup/restore drill · **chỉ sau gate pháp lý/kế toán mới bật thu tiền thật.**
