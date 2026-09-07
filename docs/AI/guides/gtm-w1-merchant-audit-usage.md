# HƯỚNG DẪN SỬ DỤNG — W1 Merchant Audit (Máy Khoan Thủng GTM)

> **Phiên bản:** W1 — Merchant Audit (tuần 1/5 của GTM Drill Machine MVP)
> **Ngày hoàn thành:** 2026-09-07 (code complete + production RV PASS)
> **Commit:** `e8cd4e62` (W1 impl) + `a21fcffc` (nginx routing fix + 429 fix)
> **Branch:** `main` @ `48d488f6`
> **Task card:** `docs/AI/tasks/gtm_drill_mvp_task_card.md`
> **Strategy:** `docs/AI/plans/ecosystem-master-business-model.md` Section 4

---

## 1. W1 LÀ GÌ — GIẢI QUYẾT VẤN ĐỀ GÌ

### Vấn đề

Merchant HKD (hộ kinh doanh) không tự phát hiện mình cần phần mềm quản lý. Door-to-door sales tốn kém và tỷ lệ chốt thấp vì merchant chưa thấy "kết quả" mà chỉ thấy "công cụ". Đây là cold-start của phễu khách hàng.

### Giải pháp W1

Trang **"Kiểm tra cửa hàng"** (`/kiem-tra-cua-hang` trên timlathay.com) cho phép chủ cửa hàng nhập tên hoặc mã số thuế (MST) → nhận báo cáo miễn phí về mức độ hiện diện của cửa hàng trên mạng lưới TimLaThay/Vạn An.

**Nguyên lý:** bán kết quả, không bán phần mềm. Câu hỏi mở đầu không phải "Bạn cần phần mềm quản lý không?" mà là *"Khách gần cửa hàng bạn đang tìm gì?"* — khơi gợi nhu cầu tự phát hiện.

### Phễu W1 tạo ra

```
Merchant tự tìm thấy trang (SEO / chia sẻ / CTV)
  → Nhập tên/MST
  → Nhận báo cáo hiện diện (có/không trong danh bạ, có storefront, có social, peer cùng ngành)
  → Nếu chưa có: CTA "Đưa cửa hàng lên TimLaThay — Đăng ký miễn phí"
  → Nếu đã có (Pending): CTA claim (chứng minh quyền sở hữu)
  → Nếu đã có (Active): thấy mình đang ở đâu trong mạng lưới → bước sang W3 Revenue Proof
```

---

## 2. CÁCH TRUY CẬP

### Trang audit (dành cho merchant)

**URL:** `https://timlathay.com/kiem-tra-cua-hang`

- SSR (Blazor Server) — load < 1 giây, SEO-friendly
- Anonymous — không cần đăng nhập
- Mobile-friendly (UI Platform components)

### API audit (dành cho developer / tích hợp)

**Endpoint:** `GET https://api2.khachvip.online/api/v1/growth/audit`

**Query params:**
- `name` (string, tùy chọn) — tên cửa hàng (ILIKE match)
- `mst` (string, tùy chọn) — mã số thuế (exact match)

**Bắt buộc:** ít nhất 1 trong 2 param phải có giá trị. Nếu cả 2 rỗng → HTTP 400.

**Rate limit:** 10 requests/IP/hour (FixedWindow). Vượt giới hạn → HTTP 429 + JSON `{"message":"Quá giới hạn yêu cầu. Vui lòng thử lại sau 1 giờ."}`.

**Auth:** Anonymous (không cần token).

**Ví dụ:**

```bash
# Tìm theo tên
curl "https://api2.khachvip.online/api/v1/growth/audit?name=Central%20Mall"

# Tìm theo MST
curl "https://api2.khachvip.online/api/v1/growth/audit?mst=0301234567"

# Tìm kết hợp (name OR mst)
curl "https://api2.khachvip.online/api/v1/growth/audit?name=Van%20An&mst=0301234567"
```

---

## 3. RESPONSE FORMAT

### Match — Active tenant (đã claim, đã Active)

```json
{
  "found": true,
  "tenant": {
    "id": "b391c72b-...",
    "name": "Central Mall",
    "isPending": false,
    "slug": null,
    "industry": null,
    "hasStorefront": false,
    "hasKhachLinkDomain": false,
    "khachLinkDomain": null,
    "hasSocialLinks": false,
    "socialLinksFb": null,
    "socialLinksTiktok": null
  },
  "industryPeerCount": null,
  "claimUrl": null,
  "registerUrl": null
}
```

### Match — Pending tenant (crawl chưa claim)

```json
{
  "found": true,
  "tenant": {
    "id": "b69b0608-...",
    "name": "CÔNG TY TNHH DONER LAB",
    "isPending": true,
    "slug": "pending-4202064797-73c9",
    "industry": null,
    "hasStorefront": true,
    "hasKhachLinkDomain": false,
    "khachLinkDomain": null,
    "hasSocialLinks": false,
    "socialLinksFb": null,
    "socialLinksTiktok": null
  },
  "industryPeerCount": null,
  "claimUrl": "/store/pending-4202064797-73c9/claim",
  "registerUrl": null
}
```

### No match

```json
{
  "found": false,
  "tenant": null,
  "industryPeerCount": null,
  "claimUrl": null,
  "registerUrl": null
}
```

### Empty params — HTTP 400

```json
{
  "message": "Vui lòng nhập tên cửa hàng hoặc mã số thuế."
}
```

### Rate limited — HTTP 429

```json
{
  "message": "Quá giới hạn yêu cầu. Vui lòng thử lại sau 1 giờ."
}
```

---

## 4. CÁC TRƯỜNG HỢP SỬ DỤNG

### 4.1 Merchant tự kiểm tra (self-discovery)

1. Merchant vào `https://timlathay.com/kiem-tra-cua-hang`
2. Nhập tên cửa hàng (hoặc MST)
3. Xem báo cáo:
   - **Chưa có trong danh bạ** → CTA "Đưa cửa hàng lên TimLaThay — Đăng ký miễn phí" → chuyển sang `/claim?name=...`
   - **Có nhưng Pending** (crawl tạo, chưa claim) → CTA claim → `/store/{slug}/claim` (upload giấy phép kinh doanh)
   - **Đã Active** → thấy mình đang ở đâu (storefront/social/domain) → bước sang W3 dashboard "Tháng này"

### 4.2 CTV/Salesman gửi link cho merchant

1. CTV copy link `https://timlathay.com/kiem-tra-cua-hang`
2. Gửi cho merchant qua Zalo/Messenger
3. Merchant tự nhập → tự thấy report → tự click CTA
4. (W4 sẽ thêm `?ref={customerId}` để CTV được ghi công hoa hồng)

### 4.3 Remote closer (sales) dùng trước khi gọi

1. Closer vào trang audit, nhập tên cửa hàng chuẩn bị gọi
2. Xem merchant đã có storefront/social chưa
3. Mở cuộc gọi bằng: *"Em thấy cửa hàng anh đang ở đây trên TimLaThay, có X sản phẩm. Em vào cùng màn hình 10 phút cấu hình luôn nhé?"* (W5 sẽ thêm nút [Cho phép Vạn An hỗ trợ từ xa])
4. Dùng impersonation #103 để vào cùng tài khoản merchant

### 4.4 Tích hợp vào tài liệu marketing

- QR code in trên card/flyer → dẫn về `/kiem-tra-cua-hang`
- Link trong signature Zalo OA
- Link trong bài đăng Facebook "Kiểm tra miễn phí cửa hàng bạn có trên mạng lưới Vạn An"

---

## 5. QUY TẮC SỐ LIỆU (GROUND RULE 5)

### Được hiển thị (số đo được)

- `found`: có/không trong mạng lưới
- `isPending`: trạng thái (crawl chưa claim / đã Active)
- `hasStorefront`: có storefront trên KhachLink chưa
- `hasKhachLinkDomain`: có domain riêng chưa
- `hasSocialLinks`: có link FB/Tiktok chưa
- `industryPeerCount`: số cửa hàng cùng ngành trong dữ liệu crawl (đếm từ `Settings.BusinessField`)

### KHÔNG được hiển thị (M3 privacy / legal)

- **CrawledPhone**: số điện thoại crawl = internal-only (ND13/2023 + M3 rule)
- **Email**: email crawl = internal-only
- **Address/location** của Pending tenant: chỉ hiển thị name + slug
- **Fake precision**: cấm "420 lượt tìm quanh đây", "+10-20 đơn/ngày" nếu không có dữ liệu đo được

### Pending tenant — chỉ trả

- `name` (tên doanh nghiệp từ crawl — public business data)
- `slug` (slug internal)
- `isPending: true`
- `hasStorefront` / `hasSocialLinks` (có/không, không lộ chi tiết)
- `claimUrl` (link claim)

---

## 6. KIẾN TRÚC & DATA FLOW

```
Merchant browser
  → https://timlathay.com/kiem-tra-cua-hang (Directory SSR, port 8080)
  → Directory GrowthAuditService (server-side HTTP call)
  → https://api2.khachvip.online/api/v1/growth/audit (Gateway, port 80)
  → Gateway GrowthController
  → PostgreSQL (Tenants + Settings + CrawlSources)
  → Response JSON
  → Directory SSR renders report
  → Merchant sees report + CTA
```

**XForwarded-For handling:** Directory gọi Gateway server-side, nhưng rate limit vẫn partition theo IP của **end user** (không phải IP container Directory). Directory `GrowthAuditService` forward IP user qua header `X-Forwarded-For`, Gateway `UseForwardedHeaders` rewrite `RemoteIpAddress` → rate limit partition đúng.

**nginx routing:** `timlathay.com` page/asset/blazor traffic → Directory SSR container (port 8080). API traffic (`/api/`) → Gateway (port 80). KhachLink WASM (port 80) chỉ phục vụ `*.khachvip.online` commerce domains.

---

## 7. FILES & VỊ TRÍ

| File | Vai trò |
|---|---|
| `2_Gateway/Controllers/GrowthController.cs` | API endpoint `GET /api/v1/growth/audit` |
| `2_Gateway/Program.cs` | Rate limit policy `growth-audit` (10/IP/h, 429 OnRejected) |
| `5_WebApps/Directory/Components/Pages/Audit.razor` | Trang `/kiem-tra-cua-hang` (Blazor Server SSR) |
| `5_WebApps/Directory/Services/GrowthAuditService.cs` | Service gọi Gateway + forward XFF |
| `5_WebApps/Directory/Program.cs` | DI registration |
| `nginx/templates/vanan.multivps.conf.template` | Routing timlathay.com → Directory SSR (port 8080) |
| `6_Testing/e2e-tests/gtm-audit.spec.ts` | E2E spec (4 tests, env-driven) |
| `6_Tests/VanAn.Architecture.Tests/AuthorizationEnforcementTests.cs` | Arch whitelist GrowthController |

---

## 8. E2E TEST (chạy khi ecosystem lên)

**File:** `6_Testing/e2e-tests/gtm-audit.spec.ts`

**Env vars cần set:**
- `DIRECTORY_URL` — URL Directory (default `https://timlathay.com`)
- `AUDIT_TENANT_NAME` — tên active tenant test (default "Central Mall")
- `AUDIT_PENDING_TENANT_NAME` — tên pending tenant test (default "DONER LAB")

**Chạy:**
```bash
cd 6_Testing/e2e-tests
DIRECTORY_URL=https://timlathay.com \
AUDIT_TENANT_NAME="Central Mall" \
AUDIT_PENDING_TENANT_NAME="DONER LAB" \
npx playwright test gtm-audit.spec.ts
```

**4 tests:**
1. Audit page renders với form + copy "bán kết quả"
2. Submit active tenant → thấy report + CTA
3. Submit pending tenant → thấy claim CTA, không lộ phone/email
4. Spam >10 requests → 429

---

## 9. GIỚI HẠN W1 (DEFER — không phải bug)

| Giới hạn | Lý do | Khi nào build |
|---|---|---|
| Không có "lượt tìm kiếm quanh đây" | 0 event tracking/telemetry [V] | Gate G1 — sau 30 ngày dữ liệu thật |
| Không có AI scoring/SDR | 0 behavioral data | Gate G1 — event pipeline trước |
| Không lookup theo SĐT/FB link | SĐT crawl = internal-only (M3) | v2 — sau khi có consent flow |
| Không có `industryPeerCount` nếu tenant không có `Settings.BusinessField` | Dữ liệu crawl chưa đầy đủ | Crawler phase 2 (industry enrichment) |
| Không có dashboard "Tháng này" | Cần W3 StoreMetricDaily | W3 Revenue Proof |
| Không có referral attribution | Cần W4 TenantClaimRequest +2 fields | W4 Merchant Referral |
| Không có remote support consent | Cần W5 Support.razor | W5 Remote closing |
| `GrowthMachine:Enabled` flag chưa có | W5 task | W5 — hiện audit luôn ON (anonymous, rate-limited, không harm) |

---

## 10. RV CHECKLIST (chạy lại khi cần)

| # | Test | Command | Expected |
|---|---|---|---|
| 1 | Directory audit page | `curl -sS -o /dev/null -w "%{http_code}" https://timlathay.com/kiem-tra-cua-hang` | 200 |
| 2 | Directory SSR (not WASM) | `curl -sS https://timlathay.com/kiem-tra-cua-hang \| grep "VanAn.Directory.styles.css"` | match |
| 3 | Gateway audit empty | `curl -sS -w "%{http_code}" "https://api2.khachvip.online/api/v1/growth/audit?name=&mst="` | 400 |
| 4 | Gateway audit no-match | `curl -sS "https://api2.khachvip.online/api/v1/growth/audit?name=ZZZNoMatch&mst=9999999999"` | `{"found":false}` |
| 5 | Gateway audit active | `curl -sS "https://api2.khachvip.online/api/v1/growth/audit?name=Central%20Mall"` | `{"found":true,...}` |
| 6 | Gateway audit pending | `curl -sS "https://api2.khachvip.online/api/v1/growth/audit?name=DONER%20LAB"` | `{"found":true,"isPending":true,...,"claimUrl":"..."}` |
| 7 | Rate limit | Spam 12 requests | 200 × 10, then 429 × 2 |
| 8 | nginx routing | `curl -sS https://timlathay.com/ \| grep "VanAn.Directory.styles.css"` | match (Directory SSR) |

---

## 11. BƯỚC TIẾP THEO (W2)

W1 hoàn thành phễu **tự phát hiện → tự xem report**. W2 sẽ thêm **tự trải nghiệm**:

- `/demo` trên KhachLink — merchant nhập tên quán + ngành → dựng storefront mock (logo, 3-5 sản phẩm, giờ mở cửa, màu theme) — **không persistence**, session-only
- Nút "Đưa cửa hàng lên TimLaThay" → `/claim?name=...` (prefill)
- E2E `gtm-demo.spec.ts`

See `docs/AI/tasks/gtm_drill_mvp_task_card.md` Section W2.
