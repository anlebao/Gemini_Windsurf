# Task P4 — MeteringApiClient + 4 Hook Nguồn Event

> **Status:** ✅ ĐÃ DUYỆT — sẵn sàng implement (metering host = `https://app2.khachvip.online/admin/metering/api` — VPS riêng sau nginx shop-a, `Metering:BaseUrl`)
> **Phase:** P4 (GĐ-1) · **Gate:** RV — event chạy production, summary có data đủ 4 nguồn
> **Căn cứ:** task_p0_analyze.md §1 (ma trận đã verify) · SRS [G15][G16][G17] · §6.4 client mỏng

---

## 1. Mục tiêu

4 nguồn event chảy vào `vanan-metering`: (a) `store.visit` qua public ingest, (b) `chat.message`/`chat.bytes` từ 2 service gửi tin, (c) `directory.search`/`directory.redirect` từ timlathay.com, (d) `feature.*` từ booking/financial/inventory. Tất cả fire-and-forget — **metering chết không ảnh hưởng nghiệp vụ** ([G9]).

## 2. Kiến trúc client

| Thành phần | Thiết kế |
|---|---|
| `IMeteringClient` interface | `RecordUsageAsync(event)` · `RecordUsageBatchAsync(events)` · `CheckBalanceAsync(tenantId, serviceCode)` (P7 dùng, khai báo ngay trả null) |
| `MeteringApiClient` (ShopERP `5_WebApps/ShopERP/Services/`) | Clone pattern `GatewayAdminApiClientBase`: `HttpClient` base `Metering:BaseUrl` + headers `X-Caller-Id`/`X-Metering-Key` từ config + `Idempotency-Key` per call. **Timeout 3s + circuit breaker** (fail → log warning, trả null — KHÔNG throw vào nghiệp vụ) |
| `MeteringApiClient` (Gateway `2_Gateway/Services/` hoặc `Realtime/`) | Cùng code shape — caller id `gateway` |
| `MeteringApiClient` (Directory `5_WebApps/Directory/Services/`) | Cùng — caller id `directory`. **Không share DLL** (mỗi host 1 client mỏng copy — tránh tạo shared lib mới chỉ cho HTTP wrapper) |
| Fire-and-forget | Hook gọi `_ = client.RecordUsageAsync(...)` trong `try/catch` + log — không await chặn nghiệp vụ. Chat/visit = batch buffer nhẹ (flush 30s hoặc 50 events — đơn giản: gửi thẳng từng event, batch chỉ cho burst nếu cần sau) |

## 3. Bốn hook chi tiết

### (a) `store.visit` — public ingest

```
Store.razor (WASM, sau khi _store load) → JS fetch POST {gateway}/api/public/metering/event
  {tenantId: _store.Id, serviceCode:"store.visit", quantity:1, unit:"visit", occurredAtUtc, fingerprint:{ua,referrer}}
→ Gateway PublicMeteringController [AllowAnonymous, rate-limited policy riêng]
  validate: tenantId parse được + serviceCode whitelist {store.visit} + quantity=1
  → forward POST /v1/usage/events với caller credential `gateway` (Idempotency-Key = hash(tenantId|date|nonce))
→ metering ghi event (tenantId từ body — public caller không tự validate tenant tồn tại ở GĐ-1, flag metadata.unverified=true nếu muốn sau)
```

- **File mới:** `2_Gateway/Controllers/PublicMeteringController.cs` — pattern `PublicBookingController`/`VietQrController` (`[AllowAnonymous]` đã verify tồn tại 34 controller public)
- **Rate limit:** policy `public-metering` riêng (vd 60 req/phút/IP) — Gateway đã có `EnableRateLimiting`
- **Store.razor:** fire 1 lần/page load sau khi `_store != null`, trong `OnAfterRenderAsync` lần đầu (tránh prerender bắn 2 lần). JS nhỏ `navigator.sendBeacon` hoặc fetch keepalive — không block render
- **Anti-bot MVP [G16]:** Gateway lọc User-Agent rỗng/bot list đơn giản (`bot|crawler|spider|curl|wget|python`) → vẫn 200 nhưng không forward (trả accepted:false — client không cần biết)

### (b) `chat.message` + `chat.bytes` — 2 hook service

| File | Hook |
|---|---|
| `3_CoreHub/Services/RealtimeMessagingService.cs` `SendMessageAsync` | Sau khi message lưu thành công → `chat.message` quantity=1 + `chat.bytes` quantity=UTF8 bytes(Content), tenantId = `Conversation.TenantId` |
| `3_CoreHub/Services/ChatService.cs` `SendMessageAsync` | Tương tự (order chat legacy), tenantId từ Order |

- Inject `IMeteringClient?` **nullable optional-ctor** (pattern AlertNotifier P4b — không vỡ call sites/tests hiện hữu)
- `requestedBy` = senderId; `taskRef` = messageId; `metadataJson` = `{subjectType, subjectId, conversationId}`
- **Tin của chính tenant staff có meter không?** → đề xuất CÓ (dùng dịch vụ vẫn tính); loại trừ sau nếu business muốn (rate card =0 cho sender role shop — defer, không lọc GĐ-1)

### (c) `directory.search` + `directory.redirect` — Directory SSR

| File | Hook |
|---|---|
| `Directory/Components/Pages/Home.razor` `SearchStores()` | Sau khi result về → `directory.search` q=1, tenantId = **platform tenant** (config `Metering:PlatformTenantId` — search là cross-tenant) + metadata `{keyword hash, resultCount}` — KHÔNG ghi keyword thô (privacy) |
| `Home.razor` `NavigateToStore(slug, khachLinkDomain)` | **Đổi signature** thêm `tenantId` (từ `store.Id` — DTO đã có field) → fire `directory.redirect` tenantId=đích trước `NavigateTo`, metadata `{slug, source:"timlathay"}` |

- Directory cần `MeteringApiClient` riêng (host SSR) + config caller `directory`
- Product-search mode (`SearchProductsAsync`) cũng fire `directory.search` + metadata `{mode:"product"}`

### (d) `feature.*` — ShopERP

| serviceCode | Hook | Tần suất |
|---|---|---|
| `feature.booking` | `BookingService.CreateBookingAsync` — sau khi persist thành công | per-action |
| `feature.financial` | `FeatureUsageRecorder` gọi từ `Financial/Dashboard.razor`, `BreakEven.razor`, `UnitEconomics.razor` `OnInitializedAsync` | monthly-active (dedupe `feature.financial:{tenant}:{yyyy-MM}`) |
| `feature.inventory` | `FeatureUsageRecorder` từ `Inventory/InventoryDashboard.razor`, `Forecast.razor`, `Ingredients.razor` | monthly-active |

- **`FeatureUsageRecorder`** (ShopERP service mới): `RecordFeatureAsync(serviceCode)` → build idempotency key `feature:{code}:{tenantId}:{yyyy-MM}` → metering dedupe tự nhiên (unique index) → mỗi tháng tối đa 1 event/tenant/module. Fire-and-forget.
- Booking dedupe theo `booking:{bookingId}` (idempotency key) — retry tạo không double-count.

## 4. Edge cases đã liệt kê hết

- **Metering down/timeout:** mọi hook catch → log warn → tiếp tục nghiệp vụ (NFR-4). Event mất chấp nhận được ở GĐ-1 (DLQ GĐ-3).
- **Double-fire Store.razor:** prerender + interactive render cùng bắn → guard `firstRender` trong `OnAfterRenderAsync` + sendBeacon idempotency `visit:{tenantId}:{userDeviceId|anon}:{yyyy-MM-dd-HH}` (dedupe per giờ — refresh cùng giờ không double).
- **tenantId giả (public endpoint):** GĐ-1 chỉ validate format + whitelist serviceCode — attribution sai thì lọc ở báo cáo; GĐ-3 fraud rules xử lý. Ghi `metadata.source="public-gateway"`.
- **Tin nhắn rỗng/whitespace:** service layer đã chặn trước hook (MaxContentLength 2000 tồn tại) — hook chỉ chạy sau persist thành công.
- **Booking tạo fail/rollback:** hook đặt SAU `SaveChanges` thành công.
- **FeatureUsageRecorder tenant context:** ShopERP có tenant scope sẵn (`ITenantContext` — verify tên thật lúc implement, không suy đoán).
- **NavigateToStore pending tenants:** store `IsPending` không render nút → không fire.
- **Config thiếu `Metering:BaseUrl`:** client disabled silently + log 1 lần — deploy staging không cần metering.

## 5. Test matrix

| # | Test | Tầng |
|---|---|---|
| T01 | `MeteringApiClient` 3s timeout → trả null, không throw | unit (MockHttp — RichardSzalay có sẵn) |
| T02 | Circuit breaker mở sau N lỗi → short-circuit | unit |
| T03 | FeatureUsageRecorder → đúng idempotency key format `feature:{code}:{tenant}:{yyyy-MM}` | unit |
| T04 | RealtimeMessagingService.SendMessageAsync → RecordUsageAsync gọi đúng tenantId+bytes | unit (mock IMeteringClient — Core.Tests) |
| T05 | ChatService.SendMessageAsync → tương tự | unit (Core.Tests) |
| T06 | BookingService.CreateBookingAsync → feature.booking fire sau save | unit (Core.Tests) |
| T07 | PublicMeteringController: UA bot → 200 không forward | unit/architecture hoặc integration |
| T08 | PublicMeteringController: serviceCode ngoài whitelist → 400 | unit |
| T09 | Config thiếu → client disabled, hook no-op | unit |
| T10 | E2E manual (RV): mở store → summary có store.visit; gửi chat → chat.message; search+click timlathay → 2 events; tạo booking → feature.booking; mở /financial → feature.financial | RV L3-L4 |

**Guard note:** hook vào `3_CoreHub` = MODIFY — chỉ thêm optional-ctor param + fire-and-forget call sau persist; không đổi signature public, không chạm Domain.cs, không business logic mới trong service ngoài "bắn event".

## 6. File plan

| File | Action |
|---|---|
| `5_WebApps/ShopERP/Services/MeteringApiClient.cs` + `FeatureUsageRecorder.cs` | BUILD |
| `2_Gateway/Controllers/PublicMeteringController.cs` + `Services/MeteringApiClient.cs` | BUILD |
| `5_WebApps/Directory/Services/MeteringApiClient.cs` | BUILD |
| `KhachLink/Pages/Store.razor` (+ JS nhỏ trong wwwroot) | MODIFY |
| `3_CoreHub/Services/RealtimeMessagingService.cs`, `ChatService.cs`, `Booking/BookingService.cs` | MODIFY (optional ctor + fire) |
| `Directory/Components/Pages/Home.razor` | MODIFY (2 hook + signature) |
| ShopERP `Financial|Inventory/*.razor` (≤6 pages) | MODIFY (1 dòng gọi recorder) |
| `Program.cs` ×3 (DI + config + rate limit policy) | MODIFY |
| `6_Tests/VanAn.Core.Tests` + `ShopERP.Tests` | += tests T03–T06, T09 |

**Out of scope:** check/deny (P7) · NATS mirror (P10) · fraud rules (P16).

## 7. Status — IMPLEMENTED 2026-10-23 (pre-deploy)

**Build:** `dotnet build VanAn.sln` — 0 errors (warnings = baseline). `VanAn.Metering.sln` — 0 errors/0 warnings.
**Tests:** `MeteringP4Tests` 9/9 PASS (T01–T09). Regression ChatService+BookingService+RealtimePlatform: 49/49 PASS. actionlint workflow: clean.

### Deviations ghi nhận (so với card gốc)

1. **`Metering:BootstrapCallers`** (metering Program.cs) — thêm seed list `"id:key;id2:key2"` cho caller `gateway`+`directory` (card gốc chỉ có BootstrapCaller đơn). Backward-compatible, cần thiết vì audit attribution per-caller.
2. **Store.razor fire ở `OnInitializedAsync`** — không phải `OnAfterRenderAsync`: KhachLink là standalone WASM (không prerender), OnInitializedAsync chỉ chạy 1 lần/page-load → tương đương "first render only". JS `vananMetering.recordVisit` dùng `sendBeacon`/fetch keepalive như card.
3. **`dir-search`/`dir-redirect` dùng unique GUID idempotency** — mỗi action đếm 1 lần (không dedupe theo giờ): search/click lần 2 là usage thật, khác `store.visit` cần chống refresh-spam.
4. **FeatureUsageRecorder lấy `ITenantProvider`** (không "tenant context" mơ hồ) — `feature:{code}:{tenant}:{yyyy-MM}` đúng card; `HasTenant=false` (sysadmin browse) → skip.
5. **`Metering__BaseUrl` cần trailing `/`** — client tự normalize defensive; envs đã ghi `.../api/`.
6. **`Metering:PlatformTenantId=f0000000-0000-4000-8000-000000000001`** — tenant grouping cho `directory.search` (không phải tenant thật).
7. **Tests gom 1 file** `6_Tests/VanAn.Core.Tests/Metering/MeteringP4Tests.cs` (Core.Tests ref đủ Gateway+ShopERP+CoreHub) thay vì rải ShopERP.Tests.

### Deploy wiring đã làm

- `docker-compose.{gateway,shoperp,khachlink,directory,metering}.yml` += `Metering__*` envs (callerId/key/BaseUrl/PlatformTenantId + `BootstrapCallers`).
- `cd-multivps.yml`: `.env.{gateway,khachlink,shoperp,metering}` writes `METERING_KEY_*`/`METERING_CALLERS` — key extract từ secret `METERING_CALLERS` (`gateway:<k1>;directory:<k2>`) + `METERING_CALLER` (shoperp-a). Secrets check += `METERING_CALLERS`.
- GitHub secret `METERING_CALLERS` — SET.
