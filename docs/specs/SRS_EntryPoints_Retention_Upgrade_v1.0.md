# SRS — Nâng Cấp Giữ Chân Người Dùng Cho 3 Entry Points
## timlathay.com · diemthuong2.khachvip.online · commienphi.timlathay.com → hoptacxa.timlathay.com

- **Version:** 1.0 (DRAFT — for review)
- **Date:** 2026-11-14
- **Status:** PROPOSED
- **Scope:** Customer-facing index/landing experience của 3 public entry points
- **Out of scope:** ShopERP admin UI, Gateway internals, accounting domain

---

## 1. Bối cảnh & Mục tiêu

3 entry points hiện phục vụ 3 vai trò khác nhau nhưng đều có vấn đề giữ chân (retention):

| Entry point | Routing | Page rendered | Vai trò |
|---|---|---|---|
| `timlathay.com` | nginx apex → `$is_directory=1` → Directory SSR :8080 | `5_WebApps/Directory/Components/Pages/Home.razor` | Danh bạ cửa hàng (directory) |
| `diemthuong2.khachvip.online` | server block riêng → KhachLink WASM | `5_WebApps/KhachLink/Pages/Home.razor` | FullCommerce PWA |
| `commienphi.timlathay.com` | wildcard `*.timlathay.com` → KhachLink WASM (Reseller profile, `OwnerTenantId`) | cùng `Home.razor` | Storefront cơm từ thiện — **giữ nguyên, không đổi** |
| `hoptacxa.timlathay.com` (MỚI — planned) | wildcard `*.timlathay.com` → KhachLink WASM (**profile mới `Hoptacxa`**) | `Home.razor` + portal landing | **Platform portal CHO các hợp tác xã** — KHÔNG gắn `OwnerTenantId`; mỗi HTX có nhiều tenant thành viên (shop/store). Mô hình khuôn mẫu: per-HTX subdomain sau này |

**Mục tiêu:**

1. **O1 — Audit feature visibility:** rà soát toàn bộ tính năng đã implement nhưng đang ẩn do toggle OFF (SystemAdmin/Owner), quyết định giữ ẩn/bật/xoá.
2. **O2 — Mobile-first layout harmonization:** điều chỉnh font/box/spacing hài hòa, không bể layout trên smartphone (360–430px phổ biến; tối thiểu 320px).
3. **O3 — Tạo platform portal hoptacxa.timlathay.com:** subdomain mới với profile `Hoptacxa` riêng — là **khuôn mẫu platform cho các hợp tác xã** (mỗi HTX gồm nhiều tenant thành viên), KHÔNG thuộc 1 tenant cụ thể; `commienphi.timlathay.com` giữ nguyên song song (nó là 1 storefront đơn, có thể trở thành member tenant của 1 HTX).
4. **O4 — Retention UX:** giảm bounce cho cả 3 entry points (đề xuất UX, xem §5.4).

**Non-goals:** không thay đổi Domain layer, không thêm commerce feature mới ngoài re-organize UI.

---

## 2. Finding F1 — Tính năng đã implement nhưng đang ẨN do toggle OFF

### 2.1 Inventory đầy đủ các lớp toggle

**Lớp 1 — Per-instance NavFlags** (SystemAdmin: `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstances.razor`; client cache: `KhachLinkInstanceHttpService`, TTL 1 phút):

| Flag | Nav hiển thị | Page guard (ProfileGuard) | Preset mặc định |
|---|---|---|---|
| ShowHome | Trang chủ | — | ON (mọi profile) |
| ShowCart | Giỏ hàng + CartDrawer | `/cart` | ON (FullCommerce/Reseller) |
| ShowOrders | Đơn hàng | `/my-orders` | ON (FullCommerce/Reseller/Logistics) |
| ShowLoyaltyHistory | Lịch sử tích điểm | `/my-loyalty` | ON (FullCommerce/Reseller) |
| ShowMissions | Nhiệm vụ | `/missions` | ON (FullCommerce/Reseller) |
| ShowRewards | Đổi điểm | `/rewards` | ON (FullCommerce/Reseller) |
| ShowAllianceWallet | Ví liên minh | `/alliance-wallet` | ON flag, NHƯNG gate kép `_isAllianceMode` |
| ShowStores | Cửa hàng | ⚠️ `/stores` KHÔNG có ProfileGuard — ẩn nav nhưng vẫn truy cập trực tiếp được (inconsistent) | ON mọi profile |
| ShowCampaigns | Khuyến mãi | `/campaigns` | ON trừ Logistics/JobMarket |
| ShowScan | Quét QR | `/scan` | ON (FullCommerce/Reseller) |
| ShowQrClaim | QR gửi xe | `/qr/wallet` | ON (FullCommerce/Reseller) |
| ShowCommunity | Cộng tác viên (role-gated `_isShipper‖_isSalesman‖_isShopOwner`) | — | ON (FullCommerce/Reseller/Logistics) |
| **ShowJobs** | ⚠️ **DEAD FLAG** — không có page/component nào đọc flag này, không có `/jobs` route trong KhachLink | — | OFF mọi profile trừ JobMarket |
| ShowProfile | Tài khoản | — | ON mọi profile |
| ShowStaffDashboard | Dashboard (role-gated `_isStaff`) | — | ON (FullCommerce/Reseller) |

**Lớp 2 — Global Home sections** (SystemAdmin: `KhachLinkHomeSettings.razor` → `KhachLinkHomeSettingsController`):
`Home_CampaignSection_Enabled`, `Home_StoreSection_Enabled`, `Home_FeaturedSection_Enabled`, `Home_SocialHub_Enabled` — default ON. Nếu tắt hết → Home chỉ còn search card.

**Lớp 3 — Per-tenant ShopFeatureSettings** (Owner: `/settings/shop-features`, DTO `ShopFeatureSettingsDto`):

| Toggle | Tính năng bị ẩn khi OFF | Default |
|---|---|---|
| `Voice_Note_Enabled` | Mic tìm kiếm giọng nói (Home search, ProductSearchResults, StoreFinder, Cart note, ChatPanel, StoreSearchBar) + trang VoiceNote | **OFF** |
| `Voice_Call_Enabled` | Nút gọi thoại trong chat (Voice Service đã deploy production) | **OFF** (opt-in) |
| `AIChat_Enabled` | Chatbox Tenant AI trên `/store/{slug}` + "Hỏi AI về món này" (vanan-ai đã deploy, P10 admin UI done) | **OFF** (opt-in) |
| `QR_TableNumber_Enabled` | QR đặt món theo bàn | **OFF** |
| `EInvoice_Auto_Export_Enabled` | Tự động xuất hoá đơn điện tử | **OFF** |
| `Price_Validation_Enabled` | Validate giá QR trước checkout | **OFF** |
| `Charity_Donation_Enabled` | Bước "Quyên góp từ thiện" ở checkout | ON |
| `Campaign/VibeShowcase/GoogleMap/SocialHub_Section_Enabled` | 4 section trên trang `/store/{slug}` | ON |
| `Loyalty_Program/VAT_Display/Kitchen_Workflow/Accounting_Sync_Enabled` | các tính năng nền | ON |

### 2.2 Phát hiện (findings)

- **F1-a:** 3 tính năng flagship đã deploy production đang **mặc định tắt** trên mọi tenant: **Tenant AI chatbox** (`AIChat_Enabled`), **voice call trong chat** (`Voice_Call_Enabled`), **voice search/note** (`Voice_Note_Enabled`). Đây là những tính năng tạo khác biệt retention nhất nhưng user không bao giờ thấy.
- **F1-b:** `ShowJobs` là **dead flag** — admin có thể bật nhưng không có `/jobs` page nào trong KhachLink để render (JobMarket profile preset tham chiếu feature chưa ship).
- **F1-c:** `ShowStores` chỉ ẩn nav link — `/stores` thiếu `ProfileGuard` → truy cập trực tiếp vẫn vào được (khác với 7 page đã guard).
- **F1-d:** `ShowAllianceWallet` gate kép (flag + `_isAllianceMode`) — hợp lệ nhưng cần ghi rõ trong admin UI để tránh nhầm "bật rồi mà không thấy".
- **F1-e:** `Home_*` toggles là global — SystemAdmin tắt là mất trên MỌI domain/instance (blast radius lớn, nên chuyển thành per-instance nếu cần khác biệt hóa).
- **F1-f:** Community section (shipper/salesman wallet, gian hàng, dashboard) chỉ hiện khi user có role — đúng thiết kế, nhưng user thường không có đường nào *khám phá* role này từ Home.

### 2.3 Requirements — Feature Visibility

| ID | Requirement | Priority |
|---|---|---|
| FR-V1 | Lập **Feature Visibility Matrix** trong admin (ShopERP): trang tổng hợp "tính năng nào đang OFF ở tenant/instance nào" — hiện phải mở từng màn để biết | P1 |
| FR-V2 | **CHỐT (sửa theo reframing):** hoptacxa là platform multi-tenant → flagship toggles (`AIChat`, `Voice_Call`, `Voice_Note`) bật **theo từng tenant thành viên** của HTX; entry point trên portal chỉ hiện khi member tenant đó bật toggle + có context (xem §2.4). Tenant ngoài hệ sinh thái HTX giữ opt-in | P1 |
| FR-V3 | Xử lý dead flag `ShowJobs`: ẩn khỏi admin UI cho tới khi JobMarket ship, hoặc ship stub `/jobs` "Sắp ra mắt" | P2 |
| FR-V4 | Thêm `ProfileGuard RequiredFlag="ShowStores"` cho `/stores` để đồng nhất enforcement | P2 |
| FR-V5 | Admin UI ghi chú gate kép của `ShowAllianceWallet` (flag + alliance mode) | P3 |

### 2.4 Contextual Surfacing — điều kiện để AI Chat / Voice Call hoạt động

Toggle ON ≠ feature usable. `AIChat_Enabled` và `Voice_Call_Enabled` đều yêu cầu **tenant context** hợp lệ (AI cần `TenantId` để chọn knowledge base; voice call cần counterpart của đúng tenant). Bật global trên surface không-context (Home cross-tenant, Directory) sẽ render nút "chết".

**Context Resolution Chain (độ ưu tiên giảm dần):**

1. **Explicit page context** — `/store/{slug}`, product card (`item.TenantId` đã có trong `RecommendedCatalogItem`), order/tracking page, conversation.
2. **Instance context** — `OwnerTenantId` của instance gắn-1-tenant (Reseller như commienphi; layout đã `SetCurrentTenant` → context luôn sẵn mọi trang). **hoptacxa platform KHÔNG có nguồn này** (không `OwnerTenantId`).
3. **Implicit context** — `_lastTenantId` từ `LastInteractionService` (đã dùng cho Campaign/Store personalization trên Home).
4. **No context** → feature ẩn sạch (không render, không disabled-tooltip) hoặc fallback platform assistant (đầu tư riêng).

**Surface mapping:**

| Surface | Context source | AI Chat | Voice Call |
|---|---|---|---|
| hoptacxa portal (multi-tenant) | #1 per-card `tenantId` của member tenant, #3 `_lastTenantId` | "Hỏi AI"/"Gọi" per member-card — chỉ hiện khi member tenant bật toggle (FR-V8 expose) | Tương tự |
| commienphi (Reseller, mọi trang) | #2 `OwnerTenantId` — luôn có | Bật an toàn | Bật an toàn |
| diemthuong2 Home | #3 `_lastTenantId` hoặc per-card `item.TenantId` | Entry "Hỏi AI về món này" trên featured card; "Chat với cửa hàng" ở store section | Nút gọi trong chat với `_lastTenantId` counterpart |
| diemthuong2 `/store/{slug}`, product, order | #1 explicit | Như hiện nay | Như hiện nay (chat) |
| timlathay.com store card | per-card `tenantId` | "Hỏi AI"/"Gọi" per card — chỉ hiện nếu tenant đó bật toggle | Tương tự |
| New user (không context nào) | — | Ẩn | Ẩn |

**Requirements:**

| ID | Requirement | Priority |
|---|---|---|
| FR-V6 | `FeatureContextService` (client): resolve `EffectiveTenantId` theo chain #1→#3 + `HasTenantContext`; mọi contextual feature đọc từ đây | P1 |
| FR-V7 | Render rule chuẩn: `toggle ON && HasTenantContext` mới render entry point; không render nút disabled/dead | P1 |
| FR-V8 | Public feature exposure cho Directory: thêm `AiChatEnabled`/`VoiceCallEnabled` vào `TenantStoreDto` (hoặc `/api/tenants/{id}/public-features`) — anonymous-safe bools | P2 |
| FR-V9 | Voice call ngoài conversation: tái dùng counterpart resolver (most-recent participant + reachable-first) với tenant context từ FR-V6 | P2 |
| FR-V10 | Per-card AI entry trên Home featured products ("Hỏi AI về món này" → chat với `item.TenantId`) | P2 |

---

## 3. Finding F2 — Layout & Mobile Responsiveness

### 3.1 Vấn đề đã rà soát (code review)

**Header (`KhachLinkLayout.razor`):**
- **F2-a:** Tới 6 icon button 36px (install, cart, rewards, missions, loyalty, profile) + logo trên cùng 1 hàng `justify-content-between` — trên 320–360px chật, không có cơ chế collapse.

**Mobile bottom nav (`NavMenu.razor`):**
- **F2-b:** FullCommerce/Reseller preset render tới **8 tab** (Trang chủ, Đơn hàng, Liên minh, Cửa hàng, Khuyến mãi, Quét QR, QR gửi xe, Tài khoản) → mỗi tab ~45px trên 375px, label `0.65rem` + ellipsis — nhiều tab chữ dài ("QR gửi xe", "Khuyến mãi") bị cắt xấu.
- **F2-c:** Community row thứ 2 (`bottom: 56px`) + main nav ≈ 112px chiếm màn hình; footer spacer chỉ `70px` (`d-md-none`, KhachLinkLayout:196) → **nội dung/footer bị che** khi user có role.
- **F2-d:** Floating `CartDrawer` + reconnect state + onboarding tour + WhatsNewBanner có thể chồng lấn bottom nav (z-index nav=1000).

**Home (`Home.razor` KhachLink):**
- **F2-e:** Product grid `col-md-3` → mobile full-width stack, 8 featured card × 200px ảnh = scroll rất dài; mật độ thấp so với chuẩn mobile e-commerce (nên 2 cột).
- **F2-f:** Mic button dùng emoji 🎤/⏹️ thay `bi-` icon — lệch design system (Directory dùng `bi-mic`), emoji render khác nhau theo OS.
- **F2-g:** `campaign-media` fixed 100px + text — trên 320px co chữ; placehold.co placeholder không hoạt động offline (PWA).
- **F2-h:** Mixed grid conventions: `col-md-3`, `col-md-6 col-lg-4`, `col-12 col-md-6` trên cùng 1 trang — chưa có quy ước mobile grid chuẩn.

**Directory Home:** nhìn chung ổn (card-based, col responsive) — cần giữ pattern làm chuẩn; hero + card `py-4` hơi cao trên điện thoại thấp.

**Chung:**
- **F2-i:** WASM splash "Tìm Là Thấy đang tải..." hiển thị trên mọi instance (sai brand cho commienphi/diemthuong2) + không skeleton → perceived performance kém.
- **F2-j:** meta `<head>` tĩnh theo timlathay (description, apple title) cho mọi domain.

### 3.2 Requirements — Layout

| ID | Requirement | Priority |
|---|---|---|
| FR-L1 | **Mobile bottom nav tối đa 5 tab** — nhóm ít dùng vào "Thêm" (bottom sheet) hoặc ưu tiên theo profile; label ≤ 8 ký tự | P1 |
| FR-L2 | Footer/content bottom padding động = số nav rows × 56px + safe-area (không hardcode 70px) | P1 |
| FR-L3 | Header icon collapse: tối đa 3 icon + overflow menu (⋮) trên <400px | P2 |
| FR-L4 | Product grid mobile = 2 cột (`col-6`), card compact (img ~140px, tên 2 dòng clamp); desktop giữ `col-md-3` | P1 |
| FR-L5 | Chuẩn hoá icon: thay emoji 🎤/⏹️ bằng `bi-mic`/`bi-stop` (nhất quán Directory + KhachLink) | P2 |
| FR-L6 | Splash screen → skeleton Home (search bar + 4 card placeholders) + brand text động theo instance (đọc manifest/config trước khi WASM boot, hoặc tối thiểu text trung lập) | P1 |
| FR-L7 | `<head>` meta/title per-instance — proxy động như pattern manifest.json hiện có | P2 |
| FR-L8 | Placeholder ảnh: thay placehold.co bằng inline SVG/local asset (offline-safe PWA) | P3 |
| FR-L9 | Design tokens: quy ước mobile grid + spacing dùng chung ở `UI.Platform` (mọi page mới tuân theo) | P2 |
| FR-L10 | Mobile breakpoints kiểm thử: 320 / 360 / 390 / 430px portrait — không overflow-x, không che bottom nav | P1 |

---

## 4. Finding F3 — Tạo platform portal hoptacxa.timlathay.com (commienphi GIỮ NGUYÊN)

### 4.1 Mô hình sản phẩm — ✅ ĐÃ CHỐT (reframed 2026-11-14)

`hoptacxa.timlathay.com` **KHÔNG thuộc 1 hợp tác xã/tenant cụ thể** — nó là **platform (khuôn mẫu) cho các hợp tác xã**:

```
hoptacxa.timlathay.com          ← portal cấp platform (danh bạ HTX + member shops + modules)
   ├── Hợp tác xã A             ← 1 HTX = nhóm member tenants (có thể subdomain riêng sau này)
   │     ├── tenant shop A1 ────┤
   │     ├── tenant shop A2     │  member tenants = các Tenant/ShopInstance hiện có
   │     └── tenant dịch vụ A3 ─┘
   ├── Hợp tác xã B ...
   └── commienphi.timlathay.com ← storefront đơn (Reseller, OwnerTenantId) — GIỮ NGUYÊN;
                                 có thể trở thành member tenant của 1 HTX
```

**Module roadmap — sẽ implement rất sớm** (thiết kế profile + landing phải chừa chỗ ngay):
- **Mua chung (group-buy)** — nhiều member cùng gom đơn.
- **Sàn việc (job market)** — NavFlag `ShowJobs` đã tồn tại trong DTO/admin nhưng là dead flag (chưa có page) → module này là lý do `ShowJobs` tồn tại; cần ship `/jobs` thực.
- **Cộng đồng/cộng tác viên** — đã có (ShowCommunity + roles).
- **Cơm từ thiện/charity** — đã có (Charity_Donation) như 1 module con.

### 4.2 Gap kiến trúc cần lưu ý

- **Tenant grouping chưa tồn tại:** hiện không có khái niệm "HTX = nhóm member tenants". Cần model mới (vd `Cooperative` aggregate hoặc `Tenant.CooperativeId`/member list trên instance) để portal lọc "chỉ hiển thị member shops của các HTX". Đây là thay đổi domain/config — cần Gate 5 + task card riêng.
- **Instance KHÔNG có `OwnerTenantId`:** portal cross-tenant → `TenantService` không được set 1 tenant duy nhất; context resolve theo §2.4 chain #1/#3.
- **Filtering catalog:** featured/products/tenants endpoints hiện global — portal HTX cần scope theo member list (filter param hoặc endpoint riêng `/api/cooperatives/{id}/members`).

### 4.3 Tác động kỹ thuật

| Hạng mục | Việc cần làm | File/Vị trí |
|---|---|---|
| Instance config | Tạo row `KhachLinkInstance` MỚI: domain `hoptacxa.timlathay.com`, profile `Hoptacxa` (FR-P1), **`OwnerTenantId = null`** | Admin `KhachLinkInstances.razor`; PG `KhachLinkInstances` table |
| DNS | A record `hoptacxa.timlathay.com` → gateway VPS IP (136.85.94.119) | DNS provider timlathay.com |
| SSL cert | `certbot certonly --expand -d timlathay.com -d hoptacxa.timlathay.com` (wildcard block reuse cert apex — SAN per subdomain) | `scripts/init-ssl-timlathay.sh` |
| commienphi | **Không đổi** — QR salesman đã in, link cũ, PWA đã cài đều hoạt động tiếp | — |
| nginx routing | Wildcard `*.timlathay.com` đã cover → không đổi config | `nginx/templates/vanan.multivps.conf.template` |
| PWA | manifest động theo Host → instance mới cần icon/name riêng | `5_WebApps/KhachLink/nginx.conf` manifest proxy |
| Voice CORS | Đã fix allow any `https://` origin (`0f835f74`) → không bị chặn | Gateway |
| Docs/guides | Bổ sung hoptacxa vào community-commerce guides | `5_WebApps/ShopERP/wwwroot/guides/community-commerce/*.html` |

### 4.4 Requirements — Subdomain + profile + grouping

| ID | Requirement | Priority |
|---|---|---|
| FR-D1 | DNS A record + cert SAN expand, verify `https://hoptacxa.timlathay.com` 200 | P0 |
| FR-D2 | Tạo `KhachLinkInstance` `hoptacxa.timlathay.com`, profile `Hoptacxa`, `OwnerTenantId = null` | P0 |
| FR-D3 | ~~redirect~~ — **KHÔNG làm**: commienphi giữ nguyên | ✅ chốt |
| FR-D4 | Landing portal: hero "Hợp tác xã" + module cards chừa chỗ cho Mua chung/Sàn việc/Cộng tác viên + danh bạ member shops (cần FR-G1) | P1 — gộp FR-U2 |
| FR-D5 | Update guides + PWA install guide | P2 |
| FR-G1 | **Tenant grouping model** — `Cooperative` (hoặc equivalent) + member-tenant relationship + seed HTX đầu tiên (member = tenant commienphi?). Gate 5 + task card riêng | P1 — prerequisite cho portal data |
| FR-G2 | Portal data endpoints: `GET /api/cooperatives` (list), `/api/cooperatives/{id}/members` (member shops) — anonymous, cacheable (pattern `dir_api_cache`) | P1 |
| FR-P1 | Thêm `KhachLinkProfile.Hoptacxa` + preset `GetPresetFlags`. **Draft preset:** `ShowHome/Stores/Campaigns/Community/Jobs/Profile = true`, `ShowCart/Orders/Scan/Loyalty*/Rewards/Missions/AllianceWallet/QrClaim/StaffDashboard = false` (commerce ở member storefronts, không ở portal) — tinh chỉnh khi Mua chung ship | P1 |
| FR-P2 | Admin label "Hợp tác xã" + `GetProfileLabel` + `WhatsNewBanner`/`OnboardingTour` direction mapping | P1 |
| FR-P3 | Icon/logo/theme riêng (`IconUrl`, `LogoUrl`, `Theme`, nav/header/footer colors) | P2 |

---

## 5. Finding F4 — Retention UX (tóm tắt từ review trước)

| ID | Proposal | Target |
|---|---|---|
| FR-U1 | Directory Home: below-fold content — category chips, "tìm gần đây" (localStorage), featured stores; trạng thái rỗng hiện tại không giữ được ai | timlathay.com |
| FR-U2 | Reseller landing tenant-scoped: `OwnerTenantId` → hero HTX + menu/module của riêng tenant thay vì cross-tenant search | hoptacxa |
| FR-U3 | New-user hero strip trên KhachLink home: value prop + "Cài app" + 3 bước dùng; empty states kèm CTA phục hồi | diemthuong2 + hoptacxa |
| FR-U4 | Giữ traffic on-domain: `GoToStoreFinder` không forceLoad sang `khachvip.online` — dùng `/stores` nội bộ Directory | timlathay.com |
| FR-U5 | Surface retention hooks: teaser Nhiệm vụ/Đổi điểm/điểm số trên Home kể cả logged-out (CTA đăng nhập); campaign carousel mở cho new user (hiện gate `_lastTenantId`) | diemthuong2 + hoptacxa |
| FR-U6 | SEO Directory: JSON-LD `WebSite`+`LocalBusiness`, footer category links | timlathay.com |

---

## 6. NFR & Constraints

- **UI Platform:** mọi component mới dùng `VanAn.UI.Platform` (hard stop — không custom HTML/CSS bypass).
- **Gate 4:** mọi thay đổi layout bắt buộc E2E spec tại `6_Testing/e2e-tests/` (mobile viewport 375px).
- **Domain layer:** không đụng `1_Shared/Domain.cs`; NavFlags/toggles nằm ở config, không phải entity change trừ khi thêm flag mới (Gate 5 nếu có).
- **RV:** 5-layer runtime verification sau mỗi deploy; WASM domain mới cần verify manifest/icon/SW + login flow.
- **Performance:** mobile first — tránh thêm blocking request trước first paint; skeleton splash thuần HTML/CSS.
- **Privacy:** giữ nguyên metering hash/privacy pattern hiện có.

## 7. Decisions & Open Questions

### 7.1 Đã chốt (2026-11-14)

| # | Question | Quyết định |
|---|---|---|
| Q1 | Chính tả domain | **`hoptacxa.timlathay.com`** (không phải hoptacxaa) |
| Q2 | Flagship toggles bật cho ai | **Theo member tenant của HTX** (portal cross-tenant — không có `OwnerTenantId`); tenant ngoài HTX giữ opt-in → FR-V2 |
| Q3 | Profile riêng cho HTX | **Có** — `KhachLinkProfile.Hoptacxa` mới → FR-P1..P3 |
| Q4 | commienphi xử lý thế nào | **Tạo subdomain mới, giữ nguyên commienphi** — không redirect, 2 site sống song song |
| Q5 | `Home_*` toggles | **Giữ global** — không chuyển per-instance |
| Q6 | Platform assistant (scope `PlatformTenantId`) | **Đầu tư riêng** — ngoài scope SRS này |

### 7.2 Còn mở

1. **Module roadmap HTX (sẽ implement rất sớm):** Mua chung (group-buy), Sàn việc (job market — `ShowJobs` flag đã có sẵn, chờ ship page). Cần NavFlag mới cho Mua chung + section model trên portal landing (FR-D4). Viết task card khi spec từng module.
2. FR-G1: cooperative grouping model — aggregate mới `Cooperative` hay field trên `Tenant`/`KhachLinkInstance`? Seed HTX đầu tiên member nào?

## 8. Suggested phasing

- **Wave 1 (P0):** FR-D1–D2 (DNS + cert + instance `hoptacxa` không `OwnerTenantId`), FR-P1–P2 (profile `Hoptacxa`), FR-L1, FR-L2, FR-L4, FR-L6, FR-L10.
- **Wave 2 (P1):** FR-G1–G2 (cooperative grouping + portal endpoints), FR-D4 (portal landing chừa chỗ modules), FR-U1–U5, FR-V1, FR-V2, FR-V6–V7.
- **Wave 3 (P2–P3):** FR-V3–V5, FR-V8–V10, FR-P3, FR-L3/L5/L7–L9, FR-U6, FR-D5.
- **Module waves (song song, rất sớm):** Sàn việc (`/jobs` + `ShowJobs` activation), Mua chung (spec riêng + NavFlag/section mới).
