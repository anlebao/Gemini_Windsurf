# Task Card W1: Merchant Audit — "Kiểm tra cửa hàng"

> **Status:** ✅ COMPLETE + PRODUCTION RV PASS (2026-09-07)
> **Week:** W1 / 5
> **Commits:** `e8cd4e62` (impl) + `a21fcffc` (nginx routing fix + 429 fix)
> **Branch:** `main` @ `a21fcffc`
> **Master plan:** `docs/AI/tasks/gtm_drill_mvp/master_plan.md`
> **Top-level card:** `docs/AI/tasks/gtm_drill_mvp/task_card.md`

## Objective

Merchant tự kiểm tra "cửa hàng tôi có trong mạng lưới Vạn An không" → report từ dữ liệu crawl có sẵn → CTA "Đưa cửa hàng lên TimLaThay — Đăng ký miễn phí" → `/claim?name=...`.

**Nguyên tắc "bán kết quả, không bán phần mềm" (§2 GTM):** tiêu đề dùng câu hỏi kết quả kiểu "Kiểm tra cửa hàng bạn có thể nhận thêm bao nhiêu đơn quanh đây" — KHÔNG mô tả "giải pháp quản lý bán hàng toàn diện".

## Scope Checklist

### Task 1.1: Gateway audit endpoint ✅
**File:** `2_Gateway/Controllers/GrowthController.cs` — NEW (`e8cd4e62`)
- [x] `GET /api/v1/growth/audit?name={q}&mst={taxCode}` — `[AllowAnonymous]` + `[EnableRateLimiting("growth-audit")]`
- [x] Logic: match tenant theo tên (ILIKE, theo pattern `TenantStoreController.Search` L223-228) HOẶC theo MST (`Settings.TaxCode` — public business data)
- [x] Input SĐT/FB link DEFER v2 (SĐT crawl = internal-only per M3, không dùng làm public lookup)
- [x] Build `MerchantAuditDto`:
  - `Found`: name, slug, `HasStorefront` (slug != null), `HasKhachLinkDomain`, `SocialLinks`, `IsPending`
  - `IndustryPeerCount`: `Count(Tenants where Settings.BusinessField ILIKE '%{industryToken}%')` — số đo được từ crawl
  - `Checklist`: hiện diện danh bạ / storefront / kênh đặt hàng / social
  - `ClaimUrl`: link claim KhachLink
- [x] **Cấm** trả `CrawledPhone` (M3 — internal only). Pending tenant: chỉ name + IsPending (không address/phone).
- [x] Query tenant theo Pattern #8: `t.Id == new TenantId(guid)` / so sánh trực tiếp property — cấm `EF.Property<Guid>`.

### Task 1.2: Rate limit policy ✅
**File:** `2_Gateway/Program.cs` (`e8cd4e62` + `a21fcffc`)
- [x] Thêm policy `growth-audit`: 10 req/IP/hour FixedWindow (pattern như `claim-submit` — đã có `AddRateLimiter` L103-137 [V])
- [x] **Fix `a21fcffc`:** thêm `OnRejected` callback → 429 + JSON message thay vì default 503

### Task 1.3: Directory landing page ✅
**Files:** `5_WebApps/Directory/Components/Pages/Audit.razor` — NEW · `5_WebApps/Directory/Services/GrowthAuditService.cs` — NEW (`e8cd4e62`)
- [x] Route `/kiem-tra-cua-hang` (SSR, SEO-friendly, dùng `CatalogService` pattern gọi Gateway)
- [x] Form nhập tên cửa hàng hoặc MST → render report bằng **UI Platform components** + meta/OG tags
- [x] CTA: "Đưa cửa hàng lên TimLaThay — Đăng ký miễn phí" → `{KhachLink domain}/claim?name={...}`
- [x] Không load danh sách khi vào trang (pattern #157 — tránh initial load)

### Task 1.4: E2E spec ✅ (written, run pending ecosystem)
**File:** `6_Testing/e2e-tests/gtm-audit.spec.ts` — NEW (Gate 4)
- [x] Truy cập /kiem-tra-cua-hang → nhập tên tenant test → thấy report + CTA; spam >10 lần → 429
- [x] **Status:** spec written, chưa chạy local (cần ecosystem lên + env `DIRECTORY_URL`/`AUDIT_TENANT_NAME`/`AUDIT_PENDING_TENANT_NAME`)

### Task 1.5: nginx routing fix ✅ (UNPLANNED — discovered during RV)
**File:** `nginx/templates/vanan.multivps.conf.template` (`a21fcffc`)
- [x] **Root cause:** nginx `timlathay.com` server block route `location /` → `${KHACHLINK_REMOTE_HOST}:80` (KhachLink WASM) thay vì Directory SSR container (port 8080). Docker-compose có `directory` service trên port 8080 nhưng nginx config không có upstream/map nào trỏ tới nó.
- [x] **Fix:** trong section `@@EXT_DOMAIN_START:timlathay.com@@`, đổi 10 non-API `proxy_pass` từ `${KHACHLINK_REMOTE_HOST}:80` → `:8080` (apex + wildcard, cả HTTP + HTTPS). API routes vẫn `gateway:80`.
- [x] **RV PASS:** `timlathay.com/kiem-tra-cua-hang` → HTTP 200, Blazor Server SSR, `VanAn.Directory.styles.css` (không còn WASM shell)

## Prerequisites (verified before W1 start)

- ✅ Crawler + CrawlSources deployed (PR #162-164, RV pass)
- ✅ Directory SSR live (timlathay.com, Blazor Server, port 8080)
- ✅ Gateway rate limiter infrastructure (`AddRateLimiter` L103-137)
- ✅ `TenantStoreController.Search` pattern (ILIKE name match, L223-228)
- ✅ Pattern #8 documented (TenantId value object query)

## Verification — Production RV Results (2026-09-07) — 9/9 PASS

| # | Test | Expected | Actual |
|---|---|---|---|
| 1 | Directory `/kiem-tra-cua-hang` | HTTP 200, Blazor Server SSR | ✅ 200, `VanAn.Directory.styles.css`, title "Kiểm tra cửa hàng — Danh bạ Vạn An" |
| 2 | Directory Home `/` | Directory SSR (not WASM) | ✅ 200, `VanAn.Directory.styles.css` |
| 3 | Gateway audit empty params | HTTP 400 + Vietnamese message | ✅ 400, `"Vui lòng nhập tên cửa hàng hoặc mã số thuế."` |
| 4 | Gateway audit no-match | HTTP 200, `{"found":false}` | ✅ 200, `{"found":false,"tenant":null,...}` |
| 5 | Gateway audit active tenant | HTTP 200, `found:true`, no phone/email | ✅ 200, "Central Mall" `found:true`, no private contact |
| 6 | Gateway audit pending tenant | HTTP 200, `isPending:true`, M3 privacy, `claimUrl` | ✅ 200, "DONER LAB" `isPending:true`, no phone/email/address, `claimUrl` provided |
| 7 | Rate limit 10/IP/hour | 429 after 10 requests | ✅ HTTP 429 + JSON `"Quá giới hạn yêu cầu. Vui lòng thử lại sau 1 giờ."` |
| 8 | nginx routing fix | timlathay.com → Directory SSR (port 8080) | ✅ 10 proxy_pass changed, nginx -t OK |
| 9 | Gateway 429 fix | 429 instead of 503 | ✅ CD deployed, 429 + JSON message |

## Remaining (W1)

- ⏳ E2E run — cần ecosystem lên + env `DIRECTORY_URL`/`AUDIT_TENANT_NAME`/`AUDIT_PENDING_TENANT_NAME` (chạy theo Playwright rules)
- ⏳ CD deploy Gateway 429 fix (pushed `a21fcffc`, chờ CD)

## Files

| # | File | Action | Status |
|---|---|---|---|
| 1 | `2_Gateway/Controllers/GrowthController.cs` | NEW | ✅ |
| 2 | `2_Gateway/Program.cs` | UPDATE (rate limit + 429 fix) | ✅ |
| 3 | `5_WebApps/Directory/Components/Pages/Audit.razor` | NEW | ✅ |
| 4 | `5_WebApps/Directory/Services/GrowthAuditService.cs` | NEW | ✅ |
| 5 | `6_Testing/e2e-tests/gtm-audit.spec.ts` | NEW | ✅ (spec written) |
| 6 | `nginx/templates/vanan.multivps.conf.template` | UPDATE (Directory SSR routing) | ✅ |
| 7 | `2_Gateway/Architecture/*` whitelist GrowthController | UPDATE | ✅ |

## Governance checklist

- [x] Domain purity: 0 domain mods (W1 không đụng Domain.cs)
- [x] KhachLink HTTP-only: Directory SSR gọi Gateway qua HttpClient (pattern CatalogService)
- [x] UI Platform: Audit.razor dùng VanAn.UI.Platform components
- [x] Không tạo .csproj mới
- [x] Pattern #8: query tenant so sánh property trực tiếp
- [x] M3/legal: audit không lộ CrawledPhone; Pending chỉ name
- [x] Rate limit: 10/IP/hour + 429 JSON response (không 503)

## Risks encountered + resolved

| # | Risk | Resolution |
|---|---|---|
| R1 | nginx routing sai (timlathay.com → WASM thay vì SSR) | Fix `a21fcffc` — 10 proxy_pass changed to `:8080` |
| R2 | Gateway 429 trả 503 default | Fix `a21fcffc` — `OnRejected` callback + JSON message |
| R3 | E2E cần ecosystem lên | Spec written, defer run đến khi ecosystem up |

## Related

- Master plan: `docs/AI/tasks/gtm_drill_mvp/master_plan.md`
- Top-level card: `docs/AI/tasks/gtm_drill_mvp/task_card.md`
- W2 next: `docs/AI/tasks/gtm_drill_mvp/task_card_w2_interactive_demo.md`
- GTM W1 usage guide: `docs/AI/guides/gtm-w1-merchant-audit-usage.md`
- Directory SSR (precedent): `docs/AI/tasks/directory_ssr/`
