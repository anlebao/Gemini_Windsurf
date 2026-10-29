# SRS — ADMIN MOBILE PWA (SHOPERP OPS ON PHONE/TABLET) v1.0

> **Ngày:** 2026-10-30 · **Trạng thái:** DRAFT v1.0 — chờ user chốt các **[G#]**
> **Nền tảng:** ShopERP Blazor Server hiện có + pattern PWA đã chứng minh ở **KhachLink** (`manifest.json` standalone + `service-worker.js` + `pwa.js` với SRI-deadlock fix).
> **Mục tiêu MVP:** người vận hành (POS quầy / bếp / guard / owner mobile) **cài app icon lên màn hình điện thoại/tablet**, mở đúng trang theo role, chạy ổn định như app native — dùng lại toàn bộ trang ShopERP **đã responsive**.

---

## 1. BỐI CẢNH & MỤC TIÊU

**Vấn đề hiện tại:**
1. Luồng vận hành (POS thu ngân, bếp nhận đơn, guard quét QR, đặt lịch nhanh) **bản chất là việc mobile** — nhưng ShopERP chỉ là responsive web, phải mở browser + gõ URL → lỗi thao tác, không giống app.
2. KhachLink đã là PWA hoàn chỉnh (installable + SW + offline assets) — ShopERP **chưa có** manifest/SW → không cài được, mở chậm mỗi lần (wasm + css tải lại).
3. Nhân viên quầy cần "bấm icon là vào màn hình làm việc" theo role (bếp → `/kitchen`, guard → `/guard/scan`, thu ngân → `/pos`).

**Mục tiêu SRS:** biến các trang ShopERP **đã responsive** thành **Admin Mobile PWA** installable — 1 manifest + 1 service worker + entry-point-theo-role, không đổi backend logic.

**Giới hạn kỹ thuật phải chấp nhận (gốc — ảnh hưởng toàn thiết kế):**
ShopERP là **Blazor Server** — UI chạy bằng SignalR circuit tới server. **PWA chỉ tăng tốc khởi động + installable + deep-link; KHÔNG offline functional** — mất mạng chỉ hiện được trang "mất kết nối" (khác KhachLink Blazor WASM có thể offline shell). SRS KHÔNG yêu cầu chạy offline nghiệp vụ [G1].

---

## 2. HIỆN TRẠNG (khảo sát 2026-10-30)

### 2.1 Tài sản PWA có sẵn (tái dùng)

| Tài sản | Vị trí | Tái dùng |
|---|---|---|
| PWA manifest mẫu | `5_WebApps/KhachLink/wwwroot/manifest.json` — standalone, portrait, `lang: vi`, icons 72→512 | Clone + đổi tên/màu/start_url |
| Service worker | `KhachLink/wwwroot/service-worker.js` | Port sang ShopERP wwwroot (scope `/`) |
| PWA bootstrap | `KhachLink/wwwroot/js/pwa.js` — SW register + update toast + **SRI deadlock fix** (auto-reload khi stale wasm) | **Bắt buộc tái dùng** — fix đã proven production 2026-08-05 |
| Icons | `KhachLink/wwwroot/icons/` | Bộ icon mới (có thể dùng tạm ShopERP favicon upscale) |

### 2.2 Trang đã responsive (scope MVP — đã verify có `@media` / viewport)

| Trang | Route | Vai trò mobile | CSS responsive |
|---|---|---|---|
| POS thu ngân | `/pos` | **Core** — bán hàng trên tablet/phone | `POS/Create` có @media |
| Bếp realtime | `/kitchen` | **Core** — Masterchef home mặc định | có (layout đơn giản) |
| Quét QR | `/guard/scan` | **Core** — camera phone | `Guard/Scan` có @media |
| Đơn hàng | `/orders` + `Detail` + `PrintBill` | Xem/xác nhận đơn | `Orders/Index` có @media |
| Đặt lịch nhanh | `/booking/create` | Lễ tân phone | `BookingCreate` có @media |
| Kho nhanh | `/inventory/dashboard`, `/inventory/alerts` | Kiểm tồn tại quầy | có |
| Sitemap | `/sitemap` | Hub điều hướng mobile | `Sitemap.razor.css` có @media |

### 2.3 Trang responsive nhưng **KHÔNG vào MVP mobile app** (desktop nghiệp vụ — vẫn mở được qua browser)

Accounting (AccountingIndex, TransactionHistory, HKDBookDetail, PeriodClosing) · AuditTrail · TenantManagement · OcrOnboarding/OcrSettings · EInvoice dashboard/invoices — có `@media` xem được nhưng thao tác nghiệp vụ nặng → **ra ngoài scope cài icon** [G2].

### 2.4 Trang CHƯA responsive (~70 admin/desktop)

Reconcile, metering, community, cấu hình, CRM grids… — **out of scope** hoàn toàn (xem §3.2).

---

## 3. PHẠM VI

### 3.1 Trong phạm vi (MVP)

1. **PWA shell cho ShopERP**: `manifest.json` + `service-worker.js` + `pwa.js` (port từ KhachLink) + icons → cài icon "ShopERP" lên home screen.
2. **Role-based start_url/deep-link**: sau login, PWA mở đúng trang theo role (bếp→`/kitchen`, guard→`/guard/scan`, staff→`/pos`, owner→`/sitemap`) [G3].
3. **7 trang ops trong §2.2** chạy tốt trong standalone window (fullscreen, no browser chrome).
4. **Offline fallback page**: mất mạng → trang `offline.html` "Mất kết nối — chạm để thử lại" (KHÔNG offline data).
5. **Update flow**: SW update → toast "Có bản mới — Tải lại" (dùng y hệt SRI-deadlock fix của KhachLink).
6. Tablet portrait+landscape cho POS/kitchen (đơn bếp cần landscape).

### 3.2 Ngoài phạm vi (ghi nợ)

- **70 trang admin desktop** (reconcile/metering/community/CRM) — desktop-only, mở browser như cũ [G4].
- **Offline nghiệp vụ** (tạo đơn khi mất mạng → đồng bộ sau) — cần WASM/local queue, refactor lớn → nợ.
- **Push notification** (order.created → bếp) — cần Web Push + VAPID + subscription store → nợ phase 2 (đã có `VAPID_KEY_GENERATION_GUIDE.md` tham khảo).
- **iOS PWA quirk**: iOS Safari PWA không có push + giới hạn cache — ghi nhận, không cam kết parity Android.
- Cài icon cho **KhachLink** — đã có sẵn, không đụng.

---

## 4. THUẬT NGỮ

| Thuật ngữ | Nghĩa |
|---|---|
| PWA | Progressive Web App — installable qua manifest + SW |
| Standalone | `display: standalone` — cửa sổ riêng không thanh địa chỉ |
| Shell | HTML/CSS/JS/Blazor bootstrap cache sẵn trong SW |
| Circuit | Kết nối SignalR Blazor Server — **bắt buộc online** |
| SRI deadlock | Stale SW cache → wasm hash mismatch → boot fail (đã fix ở KhachLink pwa.js) |
| Ops page | Trang vận hành tại điểm bán (POS/bếp/guard/đơn/đặt lịch/kho) |

---

## 5. YÊU CẦU CHỨC NĂNG (FR)

### FR-1 — Installable manifest

- `GET /manifest.json` trả: `name: "Vạn An ShopERP"`, `short_name: "ShopERP"`, `display: standalone`, `orientation: any` (POS landscape được), `theme_color`/`background_color` theo brand ShopERP, `icons` 72→512 (maskable + any), `start_url: /`, `scope: /`, `lang: vi`.
- `App.razor` `<head>` thêm `<link rel="manifest">` + `apple-touch-icon` + `theme-color` meta.
- **AC:** Chrome Android "Cài ứng dụng" hiện · iOS "Add to Home Screen" được · icon + tên đúng.

### FR-2 — Service worker (cache-first shell, network-first circuit)

- `service-worker.js` scope `/`: pre-cache `_framework/*`, `app.css`, bootstrap, icon, `offline.html`; versioned cache `shoperp-pwa-v{N}`.
- Chiến lược: **network-first cho mọi request HTML/SignalR** (luôn mới nhất, fallback `offline.html` khi fail); **cache-first cho static framework/css/js** (tải lại cực nhanh).
- SW KHÔNG cache API POST/PUT — chỉ GET static [G5].
- **AC:** Lần mở thứ 2 < ~2s tới màn login trên 4G · mất mạng hiện `offline.html` không trắng trang.

### FR-3 — Role landing sau khi cài (deep-link)

| Role | PWA mở thẳng tới |
|---|---|
| Masterchef | `/kitchen` |
| Guard | `/guard/scan` |
| Staff / StoreKeeper | `/pos` |
| Owner / ChiefAccountant / SystemAdmin | `/sitemap` |

- Cơ chế: `start_url: /` → server redirect theo role claim sau auth (giữ nguyên flow login hiện có — không đổi auth) [G3].
- **AC:** Cài app → login role X → vào đúng trang role X, không qua bước chọn.

### FR-4 — Ops pages trong standalone

- 7 trang §2.2 phải: render đúng trong standalone (no browser chrome), giữ được circuit khi chuyển trang (Blazor internal nav — không reload), không mất session khi app vào background <5 phút (SignalR reconnect).
- POS: giữ được giỏ hàng đang tạo khi app bị đẩy background (state trong circuit; circuit rớt → reload giữ nguyên trang, không mất hàng trong đơn đã lưu) [G6].
- Guard scan: camera hoạt động trong PWA context (HTTPS đã có → getUserMedia OK).

### FR-5 — Update & versioning

- Deploy mới → SW detect update → toast "Có bản mới" + nút "Tải lại" (UX giống KhachLink); nếu SRI deadlock → auto-reload (không kẹt trắng).
- Version bump `shoperp-pwa-v{N}` mỗi release → cũ cache tự dọn.

### FR-6 — Phân quyền

- PWA dùng y hệt cookie auth hiện có — **không** đường nào cho user chưa login (redirect `/login` trong standalone).
- Role guard trang y hệt Blazor `[Authorize]` hiện có — PWA không mở thêm quyền nào.

---

## 6. YÊU CẦU PHI CHỨC NĂNG (NFR)

| NFR | Yêu cầu |
|---|---|
| NFR-1 Hiệu năng | Cold start PWA ≤ 3s trên 4G tới trang login · mở lần 2 ≤ 1.5s (cache shell) |
| NFR-2 Kết nối | SignalR reconnect ≤ 5s khi mạng chập chờn · banner "Đang kết nối lại" (Blazor default UI) |
| NFR-3 Kích thước | Pre-cache ≤ ~15MB (framework+css — giới hạn iOS) |
| NFR-4 Tương thích | Android Chrome/Edge ≥ v120 · iOS Safari ≥ 16.4 (ghi nhận không push) · tablet portrait+landscape |
| NFR-5 Bảo mật | Không cache dữ liệu auth/order vào SW · HTTPS bắt buộc (đã có) |
| NFR-6 Không regression | Desktop ShopERP giữ nguyên — PWA chỉ **thêm** manifest/SW/meta, không sửa layout desktop |
| NFR-7 Read-only với build | Không migration, không Domain, không project mới — chỉ `wwwroot/` + `App.razor` + (nếu cần) redirect landing |

---

## 7. CASE TEST

| # | Case | Kỳ vọng |
|---|---|---|
| T1 | Chrome Android → app2 → menu "Cài ứng dụng" | Icon "ShopERP" + splash đúng |
| T2 | Cài → mở → login Masterchef | Vào thẳng `/kitchen`, không browser chrome |
| T3 | Staff login → POS tạo đơn 2 món → thanh toán | Luồng đầy đủ trong standalone |
| T4 | Guard login → `/guard/scan` → quét QR | Camera mở trong PWA |
| T5 | Đang POS → tắt WiFi | `offline.html` hiện (không trắng) |
| T6 | Bật WiFi lại | Circuit reconnect, đơn đã lưu còn nguyên |
| T7 | Deploy build mới → mở app | Toast "Có bản mới" → tải lại → không SRI trắng |
| T8 | iOS Safari → Add to Home Screen → mở | Icon + standalone chạy được (ghi nhận: không push) |
| T9 | Owner login → `/sitemap` → đi tới Orders → chi tiết | Nav không reload, responsive đúng |
| T10 | Desktop Chrome vẫn truy cập `/pos` qua browser | Giữ nguyên — không ảnh hưởng |

---

## 8. RÀNG BUỘC KỸ THUẬT & KIẾN TRÚC

- **Blazor Server = online-only** — SW chỉ cache shell, circuit mất = `offline.html`. Không hứa offline data [G1].
- **Không sửa Domain/entity/migration** — chỉ `5_WebApps/ShopERP/wwwroot/` + `App.razor` + (tùy chọn) redirect role.
- **Tái dùng pwa.js của KhachLink nguyên trạng** — port file, chỉ đổi cache name + scope; giữ SRI deadlock fix.
- **Icon**: tái dùng tạm brand assets có sẵn (favicon.png upscale) hoặc bộ icon mới — chốt nguồn icon ở [G7].
- **Gateway không đổi** — PWA thuần phía ShopERP host.
- **UI Platform**: mọi chỉnh sửa nhỏ trên ops pages dùng VanA* components, không HTML tùy ý.

---

## 9. KẾ HOẠCH TRIỂN KHAI (phase)

| Phase | Nội dung | Output |
|---|---|---|
| P0 | manifest + icons + meta vào `App.razor` + `manifest.json` | Cài được icon (chưa cache gì) |
| P1 | port `pwa.js` + `service-worker.js` + `offline.html` | Cache shell + offline fallback + update toast |
| P2 | role landing redirect + 7 trang verify standalone | T2/T3/T4 pass |
| P3 | bUnit/layout test + E2E `shoperp-admin-pwa.spec.ts` (Gate 4) + RV production | Tests pass |
| P4 | push → CD → RV (install thật trên Android/iOS) | Deployed |

---

## 10. TIÊU CHÍ CHẤP NHẬN (ACCEPTANCE)

1. `chrome://inspect`/`about:blank` Android cài app được từ `app2` production.
2. Masterchef login → `/kitchen` standalone, đơn mới vào realtime.
3. Mất mạng → `offline.html`, có mạng → reconnect không mất phiên.
4. Update app → toast + reload, không trắng trang.
5. Tất cả T1–T10 pass · desktop không regression · Lighthouse PWA installable ✓.

---

## 11. CÂU HỎI MỞ — cần chốt trước khi code

| # | Câu hỏi | Đề xuất mặc định |
|---|---|---|
| G1 | Chấp nhận **không offline nghiệp vụ** (chỉ shell)? | Có — Blazor Server bắt buộc online |
| G2 | Accounting/admin trang có `@media` sẵn — cho vào app hay để browser? | Để browser — app chỉ 7 trang ops |
| G3 | Role landing: hardcode map hay đọc từ profile? | Hardcode map đơn giản (role→url) |
| G4 | 70 trang admin desktop — cần roadmap responsive sau không? | Ghi nợ, không scope MVP |
| G5 | SW cache có GET API nào không (catalog/orders list)? | Không — chỉ static shell |
| G6 | POS mất kết nối giữa chừng — giữ state thế nào? | Circuit rớt → reload trang; đơn đã save vẫn nguyên (ghi nhận không giữ draft chưa lưu) |
| G7 | Icon PWA: upscale favicon hiện có hay thiết kế bộ mới? | Upscale tạm, thiết kế sau |

---

*Tài liệu liên quan: `docs/user-guide/System_Features_UseCase_View.md` (bản đồ tính năng) · `5_WebApps/KhachLink/wwwroot/{manifest.json,service-worker.js,js/pwa.js}` (pattern tái dùng) · `docs/AI/VAPID_KEY_GENERATION_GUIDE.md` (phase 2 push) · `docs/guides/laptop-selfhost-deploy-guide.md`.*
