# MASTER PLAN — ISSUE #197: METERING NÂNG CẤP (rate-card nhóm tenant · topup tên tenant · deny hiệu lực · +4 service code)

> **Status:** CHỜ USER DUYỆT (2026-10-29)
> **Căn cứ:** Issue #197 (anlebao/Gemini_Windsurf) + verify code + DB production (2026-10-29)
> **Quyết định user chốt:** D1=A (header claims `X-Tenant-Type`/`X-Tenant-Status`) · D2=tách theo size (5 loại hình riêng) · "chưa verify" = `Pending` · +4 service code (Loyalty, Accounting, OCR onboarding, Voice note)

---

## 0. Verify đã làm (bằng chứng)

| # | Yêu cầu | Verify | Kết luận |
|---|---|---|---|
| 0 | Rate-card theo nhóm tenant | `RateCardVersion` không có scope nào; `RateCardService.ResolveAsync` resolve theo ServiceCode + EffectiveFrom (global); `CreateAsync` chặn 2 chain active cùng code | **Chưa có — feature mới** |
| 1 | Topup hiện tên tenant | `MeteringTopups.razor` L65 show `@t.TenantId.ToString()[..8]…`; `TopupRequest` + `MeteringTopupDto` chỉ có TenantId | **Đúng — thiếu tên** |
| 2 | Deny phải có hiệu lực | **Root cause (DB production vanan-metering):** card Deny tồn tại cho `feature.booking`/`chat.*`/`store.visit` nhưng deny gates UI CHỈ ở `feature.inventory`/`feature.financial` — 2 code này KHÔNG có card → unpriced → deny=false mọi nơi. Ví `00000001` cạn 0 (ledger `-53980 → 0.0` 26/10) nhưng vẫn dùng `feature.financial` (29/10 03:58) + `feature.booking` (29/10 04:01) — không bị chặn | **Đúng — gate ↔ rate-card bất khớp** |

**Dữ liệu phụ trợ (production):** 10 ví; tenant thật ví 0: `7C021960` (Cafe Tân Quy), `472BEA7C`, `27917BF8`, `6AAF19E4` (tạo 29/10). `feature.financial` đang bị ingest không card → "đo miễn phí".

---

## 1. Item 0 — Rate-card scoped: Global → TenantType → TenantStatus → Tenant override

### 1.1 Domain metering (8_Metering — KHÔNG đụng `1_Shared/Domain.cs`)
- `RateCardScope` enum: `Global=0 · TenantType=1 · TenantStatus=2 · Tenant=3`
- `RateCardVersion` += `Scope` (default `Global`) + `ScopeValue` (string?):
  - `TenantType` → `"HKD" | "Enterprise_SuperSmall" | "Enterprise_SME" | "Enterprise_Large" | "HTX"` (map từ `TenantType` enum — chuẩn hoá helper)
  - `TenantStatus` → `"verified"` (= `TenantStatus.Active`) | `"unverified"` (= `Pending`) — Suspended/Inactive không khớp bucket → rơi về Global/Type
  - `Tenant` → tenantId (Guid)
- `RateCardService`:
  - Resolve mở rộng → `ResolveForTenantAsync(serviceCode, tenantId?, tenantType?, tenantStatus?, atUtc)`:
    **precedence: Tenant(3) > TenantType(1) > TenantStatus(2) > Global(0)** — miss → null (fail-open giữ nguyên)
  - `CreateAsync` uniqueness: active chain per **(ServiceCode, Scope, ScopeValue)** — cho phép nhiều chain khác scope
  - `PatchAsync`/`DeactivateAsync` giữ nguyên (chỉ head của chain)

### 1.2 API (Program.cs — 8_Metering)
- Check `GET /v1/wallets/{t}/check`: đọc optional headers `X-Tenant-Type` / `X-Tenant-Status` → resolve scoped
- Ingest `POST /v1/usage/events` (+batch): cùng headers → deduct resolve tại `occurredAtUtc` theo attributes
- Rate-card CRUD: `RateCardCreateRequest`/DTO += `scope` + `scopeValue` (optional; thiếu → Global); list/PATCH trả về
- `GET /v1/rate-cards/resolve` += headers (ops/test)
- **Backward compat:** caller không gửi headers → Global-only (hành vi cũ — không vỡ integrate)

### 1.3 Callers (D1=A — không sync registry)
- **ShopERP `MeteringApiClient`:** `ResolveTenantAttributesAsync()` — type từ `VasFeatureFlagService.GetTenantTypeAsync` (PG — đã có sẵn, dùng ở ExpenseEntry/ShopErpMenuService) · status từ mirror `Tenants` (Status=Active→verified / Pending→unverified) · cache 60s · gửi header trên mọi check + ingest
- **Gateway `MeteringApiClient`:** Tenants PG sẵn → attributes trực tiếp (chat.*, feature.ocr); `store.visit` anonymous → không header (Global)
- **Trust model:** headers = caller claims (như tenantId) — internal services HMAC-keyed, không public

### 1.4 Admin UI `/admin/metering/rate-cards`
- Form += "Phạm vi" selector (Global / Loại hình / Trạng thái / Tenant cụ thể) + ScopeValue động (dropdown loại hình 5 mục / dropdown verified-unverified / tenant picker từ `ListTenantsAsync`)
- Bảng += cột Phạm vi (badge)
- Resolve preview: nhập test attributes → hiển thị card resolve theo precedence (ops self-service)

---

## 2. Item 1 — Topup hiện tên tenant (Option A — UI join, mặc định theo đề xuất; user chưa chọn B)

- `MeteringTopups.razor`: `ITenantManagementService.ListTenantsAsync()` (SystemAdmin — policy đã có) → `Dictionary<Guid,string>` → cột Tenant = **Name** + tooltip full id · fallback GUID prefix khi mirror thiếu row
- **0 đụng metering service/DB** — không migration, không backfill

---

## 3. Item 2 — Deny hiệu lực (3 lớp)

### 3.1 Data production (ops — sau khi code build pass)
- Tạo rate-card `feature.financial` + `feature.inventory` với **Enforcement=Deny** (đúng ý định user) qua admin UI/API
- Verify check endpoint trả `deny=true` cho tenant ví 0 (vd `00000001`, `7C021960`)

### 3.2 Server-side deny guard (mới — enforcement thật, không phụ thuộc UI)
- `MeteringDenyGuard` (ShopERP Services): `CheckOrThrowAsync(tenantId, serviceCode, estimate?)` — **fail-open** (transport error → pass + log, giữ G9) · deny → throw `MeteringDeniedException` (HTTP 402)
- Hook server-side (cùng vị trí hook ingest — phủ mọi đường vào):
  - `BookingService.CreateBookingAsync` → `feature.booking` (phủ KhachLink public + staff tạo)
  - `OrderService.UpdateOrderVoiceNoteAsync` → `feature.voice-note`
  - Gateway `TenantOcrOnboardingService` → `feature.ocr`
- API layer catch → JSON 402 + UI alert/toast "Hết hạn mức phí nền tảng" + link nạp

### 3.3 UI deny gates mở rộng (UX)
- Booking ShopERP `/booking/*` += `MeteringDenyGate ServiceCode="feature.booking"`
- Accounting += `MeteringDenyGate ServiceCode="feature.accounting"` ở **AccountingIndex + page nhập liệu/report chính** (chốt danh sách khi implement — KHÔNG 20 page)
- Loyalty += `MeteringDenyGate ServiceCode="feature.loyalty"` trên `LoyaltyDashboard`
- `store.visit`: **KHÔNG gate** (anonymous — đúng design) · Chat deny (KhachLink): giữ deviation GĐ-2 (metering.md §8)

---

## 4. Master switch toàn hệ thống (toggle on/off metering — SystemAdmin)

> Yêu cầu user bổ sung (2026-10-29): toggle bật/tắt chức năng metering TOÀN HỆ THỐNG, SystemAdmin thiết lập.

### 4.1 Thiết kế
- **Toggle sống TRONG metering service** (singleton `EnforcementConfig` — pattern `TopupConfig` đã có, 1 row): mọi quyết định deny/deduct đọc local trong cùng request — **không network dependency, không sync lag**. Không để ở Gateway PG (enforcement point phải đọc trực tiếp).
- **OFF semantics (đề xuất):** check → `deny=false` luôn (không chặn) · ingest → vẫn ghi `UsageEvent` (fact/audit) nhưng **KHÔNG deduct** (`CreditsApplied=null`, không đụng wallet/ledger) · không fire alert · wallet/banner vẫn hiển thị thông tin. **ON (default) = hành vi hiện tại.**
- Toggle quan hệ với mọi lớp deny: server guard (Card B §3.2) + UI gates đều đọc qua check response → OFF tự động "thông" toàn bộ, không cần sửa từng gate.

### 4.2 API + UI
- `GET /v1/config/enforcement` → `{enabled, updatedBy, updatedAtUtc}` (mọi caller authed)
- `PUT /v1/config/enforcement` (admin-guard `CallerKey.IsAdmin` → 403 JSON — KHÔNG `Results.Forbid`) → body `{enabled}` + **AuditLog cùng SaveChanges** (metering.md §1)
- ShopERP: `MeteringRateCardApiClient` += Get/Set · UI toggle ở **header trang `/admin/metering/rate-cards`** (switch + trạng thái + "Cập nhật bởi") — kèm hướng dẫn "Tắt = không chặn, không trừ credit (usage vẫn ghi)"
- NavMenu/Sitemap: nằm trong trang rate-cards sẵn có — không cần route mới

### 4.3 Tests
- M7 toggle default `enabled=true` (row chưa tồn tại) · M8 OFF → check deny=false dù card Deny + ví 0 · M9 OFF → ingest không deduct, usage vẫn ghi · M10 ON → deny/deduct quay lại · M11 PUT non-admin → 403 · M12 PUT ghi AuditLog + singleton (1 row)

---

## 5. Service code mới + ingest hooks (P4 pattern)

| Code | Feature | Hook | Loại | Vị trí |
|---|---|---|---|---|
| `feature.loyalty` | Loyalty | `LoyaltyDashboard.razor` page visit | page | `FeatureUsageRecorder` (pattern feature.financial) |
| `feature.accounting` | Kế toán | AccountingIndex + page nhập liệu/report visit | page | `FeatureUsageRecorder` |
| `feature.ocr` | Onboarding OCR | `TenantOcrOnboardingService` per-action (tenant tạo qua OCR) | action | Gateway — sau persist thành công |
| `feature.voice-note` | Ghi chú giọng nói | `OrderService.UpdateOrderVoiceNoteAsync` per-action | action | 3_CoreHub — sau persist |

- `IMeteringClient.cs` doc comment cập nhật danh sách code
- Lưu ý: `feature.financial` đang ingest không card → sau khi tạo card sẽ **bắt đầu trừ credit** (user đã biết — đúng ý định)

---

## 6. Test matrix

| Nhóm | Tests |
|---|---|
| Metering (VanAn.Metering.Tests) | M1 resolve precedence 4 tầng (Tenant>Type>Status>Global) · M2 unique per (code,scope,value) — chain khác scope không bị chặn · M3 backward compat scope null → Global · M4 check/ingest đọc headers · M5 scope validation (value hợp lệ theo scope) · **M7-M12 master switch (§4.3)** |
| Core.Tests | C1 BookingService deny guard: deny→throw · đủ credit→pass · metering lỗi→fail-open pass · C2 voice-note guard (3 case) |
| Gateway tests | G1 OCR onboarding deny guard |
| ShopERP bUnit | U1 deny gate accounting/loyalty render · U2 topup tên tenant hiển thị |
| E2E (Gate 4) | `metering-issue197.spec.ts`: rate-card scope UI (tạo card theo nhóm + resolve preview) · deny booking thật · topup cột tên tenant |

## 7. Deploy + RV

- Validation: `dotnet build VanAn.sln` 0 err · `dotnet build VanAn.Metering.sln` 0 err · `guard-check.ps1` ALL PASSED · Core.Tests + Metering.Tests PASS (ShopERP.Tests 8 fail pre-existing — Inventory `FeatureUsageRecorder` DI, không liên quan)
- Push → CD Multi-VPS (gateway + shoperp + metering) → RV 5 lớp:
  - L1: marker production (RateCardScope) + cột scope trong metering DB
  - L2: health + rate-card scope API qua `app2.khachvip.online/admin/metering/api/`
  - L3: Playwright — deny `feature.financial` thật (tenant ví 0) · topup tên tenant · rate-card scope UI
  - L4/L5: manual user

## 8. Ngoài phạm vi (defer — không làm trong batch này)

- Chat deny UI (KhachLink) — deviation GĐ-2 đã ghi
- Snapshot attributes cho late/replay event ingest — GĐ-2
- NATS alert push (P9/P10) — giữ poll
- Item 1 Option B (snapshot `TenantName` vào `TopupRequest`) — nếu user muốn, làm phase sau

---

## 9. Tổ chức thực thi

| Card | Nội dung | Gate |
|---|---|---|
| **Card A** `task_issue197_a_ratecard_scope.md` | Item 0 toàn bộ (domain + API + callers + UI + tests) | build 2 solution + Metering.Tests + Core.Tests |
| **Card B** `task_issue197_b_deny_effective_codes.md` | Item 1 (topup tên) + Item 2 (deny 3 lớp + data prod) + **§4 master switch** + 4 service code + E2E + **Deploy + RV** | build + guard + tests + CD + RV L1-L4 |