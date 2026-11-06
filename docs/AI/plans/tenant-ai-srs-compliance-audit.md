# AUDIT: Tenant AI (`vanan-ai`) — SRS v1.1 ↔ Master Plan ↔ Task Cards ↔ Code thực tế

> **Ngày audit:** 2026-11-07 · **Chế độ:** REVIEW_ONLY (không sửa code)
> **Nguồn đối chiếu:**
> - SRS: `docs/requirements/van_an_tenant_ai_srs_v1.md` (v1.1 Consolidated Baseline, approved 2026-11-06)
> - Master plan: `docs/AI/plans/tenant-ai-master-plan.md`
> - Task cards: `docs/AI/tasks/tenant_ai/task_ai_p0..p7_*.md` (9 cards)
> - Code: `9_TenantAI/vanan-ai/`, `2_Gateway/`, `5_WebApps/{KhachLink,ShopERP}`, `UI.Platform/`
> - Trạng thái: `docs/AI/project_state.md` §167+ · git log · GitHub Actions CD runs

---

## 1. KẾT LUẬN TỔNG

**SRS CHƯA được hoàn thành 100%** theo tiêu chí: *đáp ứng yêu cầu SRS + có đủ UI + link trong NavMenu/Sitemap + end-user sử dụng được (không chỉ API/service).*

| Phạm vi | Mức hoàn thành | Ghi chú |
|---|---|---|
| **P1 — code lõi** (G1–G5 theo 9 task cards) | ~90% | Toàn bộ pipeline đã code + deploy production; còn thiếu admin API profile/policy/publish (blocker P7), retention cleanup, drift report, manual sync |
| **P1 — vận hành/end-user** | ⬜ Chưa | CP3 RV chưa chạy · CP4 pilot chưa go-live · flag chưa ON → **widget chưa render cho khách thật** |
| **P2 — Admin UI `/admin/tenant-ai/*`** | 0% | Không trang nào · không NavMenu item · không Sitemap entry · chủ quán không tự phục vụ được |
| **P3 — External Knowledge Gateway** | 0% | Deferral có chủ đích trong SRS |
| **P4 — Ecosystem AI + Directory SSR** | 0% | Deferral có chủ đích trong SRS (O4) |

---

## 2. ĐÃ HOÀN THÀNH (có bằng chứng)

### 2.1 vanan-ai service — lõi đầy đủ

| Thành phần | Bằng chứng |
|---|---|
| Skeleton sln riêng + Dockerfile :5060 + compose `pgvector/pgvector:pg16` | `9_TenantAI/vanan-ai/` — task_ai_p0 ✅ DONE `983d35be` |
| Auth 3 lớp: API key + caller scopes + HMAC-signed tenant ctx (±5ph skew, callerId match, tenant allowlist) | `AuthMiddleware` — task_ai_p0b ✅ 18/18 tests |
| Domain + EF schema: 10 entities, snake_case, `embedding vector(768)`, HNSW `vector_cosine_ops`, `CREATE EXTENSION vector`, approval_status, source_version/synced_at, expires_at | `TenantAiDbContext.cs` + migrations — task_ai_p1 ✅ 23/23 (5 PG Testcontainers) |
| Ingest API: `products:batch` idempotent + fullSync replace · `documents` → `pending_review` · approve/reject | `Program.cs` endpoints `v1/knowledge/*` — task_ai_p2 ✅ `a1b31795` |
| Chat pipeline 9 bước: IntentRouter → PolicyEngine → Retriever (SQL-first/vector-second) → UsagePermissionFilter → LlmOrchestrator → AnswerValidator | `ChatOrchestrator.cs` + `VanAn.TenantAi.Ai/*` — task_ai_p3 ✅ 99/99 · eval gate pass |
| Metering: QuotaGate (deny→refuse · fail+cache→allow · fail+expired→**degrade 0 LLM** PO-1) · MeteringIngestQueue (`ai.chat.message` Deny / `ai.chat.degraded` MeterOnly PO-2) | `QuotaGate.cs`, `MeteringIngestQueue.cs` — task_ai_p5 ✅ 118/118 |
| Kill switch `TenantAi:Enabled` + `TenantAiLlm:ApiKey` rỗng→degrade | `KillSwitchApiTests` 2 tests |
| Widget config thật: `GET /v1/config/widget` → `aiEnabled` + persona greeting/suggestions | `Program.cs:240` |
| Session history `GET /v1/chat/sessions/{sid}/messages` | `Program.cs:193` (deviation bổ sung ở P4) |

### 2.2 Storefront E2E (code)

| Thành phần | Bằng chứng |
|---|---|
| `PublicTenantAiController` — anonymous + `[EnableRateLimiting("tenant-ai-public")]` 60/min/IP + session cap 20/giờ [G8/PO-3] + resolve tenant server-side (T6: client tenantId bị override) | `2_Gateway/Controllers/PublicTenantAiController.cs` — task_ai_p4 ✅ 11 tests |
| `TenantAiContextSigner` (per-host copy HMAC-SHA256) + `TenantAiForwarder` (35s timeout, self-disable, 503 graceful) | `2_Gateway/Services/` |
| `AiChatWidget.razor` — UI Platform only (VanAnCard/VanAInput/VanAnButton) · sessionId localStorage · optimistic UI + messageId dedupe · product cards → `OnAddToCart` | `UI.Platform/Components/Ai/` — 7 bUnit tests |
| Mount thật trên storefront | `5_WebApps/KhachLink/Pages/Store.razor:302-304` — `@if (_features.AIChat_Enabled) <AiChatWidget .../>` |
| `TenantAiPublicHttpService : IAiChatClient` (fail-safe null) + DI | `KhachLink/Services/Http/` + `Program.cs:68-70` |
| Add-to-cart: prefer ProductDto thật (đúng VatRate) else card snapshot | `Store.razor AddAiProductToCartAsync` |

### 2.3 ShopERP integration

| Thành phần | Bằng chứng |
|---|---|
| `TenantAiApiClient` (pattern MeteringApiClient: self-disable, signed ctx) | `ShopERP/Services/TenantAiApiClient.cs` |
| `TenantAiKnowledgeSyncService` — product CUD push, flag `TenantAiEnabled` gate | `ShopERP/Services/TenantAiKnowledgeSyncService.cs` + `ProductsController.cs` inject |
| Nightly reconcile `TenantAiKnowledgeReconcileService` (24h, re-push fullSync) | `ShopERP/Services/TenantAiKnowledgeReconcileService.cs` |
| 2 toggles trong Settings/ShopFeatures: `AIChat_Enabled` (đã enable, bỏ "sắp ra mặt") + `Đồng bộ dữ liệu AI` → `TenantAiEnabled` | `ShopFeatures.razor:278, 282-283` |

### 2.4 Tests + deploy + pilot prep

- `VanAn.TenantAi.Tests` **153/153 PASS** — ThreatSuite 35 tests mới (T1×4 · T2×10 · T3×10 · T4×3 · T5×3 · T7×1 · T10×3, `[Trait("Threat","Tx")]`) + T6/T12 (AuthTests) + T8 (QuotaGate/MeteringGate/KillSwitch) + T9/T11/T13 (ChatApi/Ingest/ChatUnit).
- Eval harness: **41 cases** vi-VN, Grounded/Unsupported gate pass.
- **CD run `7717879f` SUCCESS** — deploy cả 5 VPS: `Deploy to TenantAI VPS · Gateway · Metering · KhachLink · ShopERP` + smoke pass.
- CP1+CP2 RV PASS (commit `d8ccfe8c`); LLM key set + **non-degraded RV pass ở API level** (commit `33daac76`).
- Ops wiring production: `vanan-ai` caller registered trên metering (auth-check 200) + 3 rate cards (`ai.chat.message` Deny 5cr · `ai.chat.degraded`/`ai.embedding.batch` MeterOnly 1cr).
- Pilot prep: tenant **An Tâm** (quán chay — đúng ưu tiên SRS §3.1) `e2d91cdf-d3c4-4399-b96e-e772a7a41630` · `docs/AI/seeds/tenant-ai/an-tam-menu-reference.json` (42 món) · `scripts/seed-an-tam-ai-docs.ps1` (8 docs story/faq → ingest+approve) · `scripts/tenant-ai-ops.ps1` (operator tooling FR-8).

---

## 3. GAP TRONG P1 (trong scope master plan nhưng THIẾU/SAI)

### 3.1 ⛔ BLOCKER — Admin API §6.3 thiếu phần lớn → P7 không thực hiện được

SRS §6.3 yêu cầu 8 nhóm endpoint admin; thực tế `Program.cs` chỉ có:

| Endpoint SRS yêu cầu | Trạng thái thực tế |
|---|---|
| `GET /v1/admin/knowledge/pending` | ✅ Có (`Program.cs:341`) |
| `POST /v1/admin/knowledge/{id}/approve` `/reject` | ✅ Có (`Program.cs:347,360`) |
| `POST /v1/admin/tenants/{t}/preview-chat` | ✅ Có (`Program.cs:176`, resolve Draft config) |
| `GET /v1/admin/tenants/{t}/profile` | ⚠️ **Stub `NotImplemented`** (`Program.cs:377`) |
| `PUT /v1/admin/tenants/{t}/profile` | ❌ **Không tồn tại** |
| `GET/PUT /v1/admin/tenants/{t}/policy` | ❌ **Không tồn tại** — `AiPolicy` entity có nhưng không config được qua API |
| `POST /v1/admin/tenants/{t}/publish` `/rollback` | ❌ **Không tồn tại** — [G11] lifecycle Draft→Publish không vận hành được |
| `GET /v1/admin/tenants/{t}/conversations` | ❌ **Không tồn tại** |
| `GET /v1/admin/tenants/{t}/usage` | ❌ **Không tồn tại** |

**Hệ quả nghiêm trọng:** `TenantAiConfig.CreateDraft`/`MarkPublished`/`MarkArchived` chỉ được gọi trong **test code** (`ChatApiTests.cs:343`, `MeteringGateTests.cs:268`). Production không có code path nào tạo/publish config → persona/tên AI/business profile cho tenant **không thể set qua API** — chat luôn fallback defaults, widget-config dùng default greeting/suggestions.

**Task P7 bị block:** Bước 3 yêu cầu "`PUT profile` → preview-chat ≥5 câu → **publish**" — 2 trong 3 endpoint không tồn tại. Không thể onboard An Tâm đúng quy trình mà không insert DB tay.

**Vi phạm SRS:** FR-1 (config versioned Draft→Preview→Publish), FR-8 (P1 operator tooling = admin API + script phải có "profile put, publish"), §6.3.

### 3.2 CP3 — Full RV chưa thực hiện

- Code đã deploy (CD `7717879f` success) nhưng **formal CP3 RV L1–L4 chưa chạy/ghi nhận**: markers trong deployed DLLs · auth matrix production · `tenant-ai-rv` script · e2e widget spec · metering ledger verify · T1–T13 production subset.
- `6_Testing/e2e-tests/ai-chat-widget.spec.ts` tồn tại nhưng **self-gating** `AI_CHAT_TEST_STORE_URL` — chưa từng chạy trên production.
- `6_Testing/rv-scripts/tenant-ai-rv/run-on-vps.sh` (8 bước) sẵn sàng nhưng chưa invoke.
- project_state §170 ghi rõ: "Next: CP3 — RV L1-L4".

### 3.3 CP4/P7 — Pilot chưa go-live → end-user CHƯA dùng được

- Flag `AIChat_Enabled`/`TenantAiEnabled` chưa ON cho An Tâm (PG + verify mirror).
- Knowledge chưa ingest production: menu 42 món (cần nhập ShopERP thật rồi sync — seed script không seed products theo PO-5) + 8 docs (owner chưa điền `[TODO]` facts: năm thành lập, địa chỉ, giờ, SĐT).
- Eval set tenant ≥20 câu khách thật chưa chạy (yêu cầu Grounded ≥95% trước bật).
- Widget chưa render trên storefront thật; chưa chat e2e ≥10 câu production; chưa đo latency P95 ≤8s.
- Chưa quan sát 48–72h + sign-off.
- PO-7 (billing tier Gemini) chưa quyết — sẽ phát sinh khi free-tier thiếu.

### 3.4 [G10] Retention cleanup job thiếu

- `conversation_messages.expires_at_utc` được set khi tạo message (`ChatOrchestrator.cs:346` — `AddDays(RetentionDays)` // [G10] 90d) + index `ix_conversation_messages_expires_at_utc`.
- **Nhưng không có background service nào xóa message hết hạn.** Hosted service duy nhất trong `VanAn.TenantAi.Api` là `MeteringIngestQueue` (`Program.cs:62`).
- SRS G10/PO-4: "`expires_at` + **daily cleanup**" → data sẽ tích lũy vô hạn. NFR-7 không đạt phần cleanup.

### 3.5 NFR-7 — Tenant-delete purge thiếu

- "tenant xóa → purge knowledge+conversations" — không có endpoint/service nào (grep `purge|DeleteTenant` = 0 kết quả trong `9_TenantAI/vanan-ai/src`).

### 3.6 [G16] Drift report thiếu (deviation chưa ghi nhận)

- SRS: "Nightly reconcile **so count/hash với nguồn → drift report**" + "drift >24h → admin alert".
- Thực tế `TenantAiKnowledgeReconcileService` chỉ **re-push fullSync** toàn bộ catalog (self-heal) — không có count/hash comparison, không drift report, không admin alert.
- Đánh giá: re-push là cách mạnh hơn (tự sửa drift thay vì chỉ báo) nhưng **thiếu observability** — không biết drift có xảy ra không. Đây là deviation cần ghi nhận hoặc bổ sung status compare (đã có `/v1/knowledge/status` trả `source_version` + `synced_at` — chỉ cần reconcile đọc + log diff).

### 3.7 [G9] Manual "Đồng bộ AI" trigger thiếu

- SRS: "Trigger: product CUD + **manual 'Đồng bộ AI'** + nightly reconcile".
- Thực tế: `SyncNowAsync` chỉ được gọi từ `TenantAiKnowledgeReconcileService` — **không có API endpoint hay UI button** nào cho owner/operator bấm đồng bộ thủ công. Toggle "Đồng bộ dữ liệu AI" trong ShopFeatures chỉ bật/tắt flag, không trigger sync.
- Tenant vừa sửa menu phải chờ tối đa 24h (hoặc sửa lại product để kích CUD hook).

### 3.8 Minor — `ai.embedding.batch` chưa ingest usage

- Deviation đã ghi trong task_ai_p5: embedding batch chưa gửi usage events (rate card MeterOnly — observability, "thêm khi P7 cần đo"). Rate card đã tạo sẵn trên metering.

### 3.9 NFR-10 — Eval "trước publish config" chưa wire

- Eval harness + 41 cases chạy trong test suite ✅. Phần "chạy trước publish config" SRS ghi là P2 — nhưng do publish endpoint chưa tồn tại (gap 3.1), hook này cũng chưa có chỗ cắm.

---

## 4. GAP THEO TIÊU CHÍ "UI ĐỦ + NAVMENU + SITEMAP + END-USER TỰ DÙNG"

### 4.1 ⛔ P2 Admin UI `/admin/tenant-ai/*` — thiếu hoàn toàn (gap lớn nhất)

SRS §3.1-P2 + FR-8 yêu cầu full admin UI trong ShopERP cho tenant Owner/StaffOrAbove:

| Chức năng P2 | Trạng thái |
|---|---|
| Identity (tên AI, xưng hô, phong cách) | ❌ Không có — và cả API cũng thiếu (gap 3.1) |
| Profile / Business profile | ❌ |
| Story + FAQ CRUD | ❌ |
| Upload documents | ❌ |
| Permission config (knowledge_usage_permissions) | ❌ |
| Preview chat | ❌ (API có, UI không) |
| Publish / Rollback | ❌ (API cũng thiếu) |
| Conversation review | ❌ (API cũng thiếu) |
| Usage dashboard | ❌ (API cũng thiếu) |
| Sync status | ❌ (API `/v1/knowledge/status` có, UI không) |

**NavMenu:** không có mục "Tenant AI"/"Trợ lý AI" nào trong `5_WebApps/ShopERP/Components/Layout/NavMenu.razor`.
**Sitemap:** không có entry tenant-ai nào.
**Hiện có duy nhất:** 2 toggle trong `Settings/ShopFeatures` (`AIChat_Enabled` + `Đồng bộ dữ liệu AI`) — đúng scope P1.

**Hệ quả:** chủ quán (end-user phía tenant-admin) **không tự phục vụ được bất kỳ thao tác nào** — không soạn story, thêm FAQ, upload doc, duyệt knowledge, xem conversations, xem usage, đổi persona. Mọi thao tác phải qua PowerShell `tenant-ai-ops.ps1` + curl (operator tooling — đúng SRS P1, nhưng không đáp ứng tiêu chí "end-user sử dụng được" của audit này).

> Lưu ý công bằng: SRS chính nó defer Admin UI sang P2, và master plan hiện tại chỉ cover P1. Đây là **deferral có chủ đích**, không phải sai sót — nhưng tính theo "SRS 100% hoàn thành" thì chưa đạt.

### 4.2 P3 — External Knowledge Gateway: chưa làm

- External Knowledge Gateway · allow-list nguồn · tenant toggle phạm vi · citation. `external_allow_json` có trong schema `ai_policies`, intent `ExternalKnowledge` có trong router (refuse mặc định P1 — đúng). Gateway chưa build.

### 4.3 P4 — Ecosystem AI + O4 Directory SSR: chưa làm

- Booking assist · đa điểm chạm · insight aggregate — chưa.
- **O4:** widget chỉ mount trên `KhachLink/Pages/Store.razor`. Tenant profile **Directory SSR** không có AI widget (SRS O4 defer P4 — nhưng nếu pilot tenant nào dùng Directory sẽ không có AI).

---

## 5. MA TRẬN FR/NFR ĐỐI CHIẾU

| Req | Nội dung | Trạng thái |
|---|---|---|
| FR-1 | Config versioned Draft→Preview→Publish | 🟡 Domain model + preview-chat có; **không có endpoint tạo/publish/rollback** |
| FR-2 | Intent Router 12 intents | ✅ |
| FR-3 | Policy Engine intent→sources | ✅ (seed `intent_routes`; tenant-level config thiếu API) |
| FR-4 | Grounded answer + validator | ✅ + eval gate |
| FR-5 | Product suggestion → cart | ✅ code · chưa RV e2e |
| FR-6 | Ingest + approval gate | ✅ |
| FR-7 | Conversation persist + resume | ✅ |
| FR-8 | P1 operator tooling / P2 UI | 🟡 P1 tooling có nhưng **thiếu profile/publish** · P2 UI 0% |
| FR-9 | Feature flag + kill switch | ✅ 3 lớp (TenantAiEnabled · TenantAi:Enabled · ApiKey rỗng) |
| FR-10 | Refuse nhẹ nhàng | ✅ templates |
| FR-11 | Usage Permission filter | ✅ + tests |
| FR-12 | Freshness/staleness | 🟡 strip volatile + staleData flag ✅ · **drift report ❌** |
| FR-13 | Provider abstraction | ✅ ILlmChatClient/IEmbeddingProvider + fakes |
| NFR-1 | Latency ≤8s | ⬜ chưa đo production (P7 step) |
| NFR-3 | AI down ≠ storefront down | ✅ graceful `aiEnabled:false` |
| NFR-6 | Cost metering | ✅ + token persist · embedding ingest thiếu (minor) |
| NFR-7 | Retention 90d + tenant purge | 🟡 expires_at set · **cleanup job ❌ · purge ❌** |
| NFR-8 | Observability + health | ✅ structured log + `/v1/health` |
| NFR-10 | Quality metrics | ✅ local eval 41 cases · production eval chưa |
| T1–T13 | Threat suite | ✅ local 153/153 · production subset chưa chạy |

---

## 6. VIỆC CẦN LÀM — THEO THỨ TỰ ƯU TIÊN

### Để go-live P1 (CP3 + CP4) — bắt buộc trước khi nói "P1 xong"

1. **Bổ sung admin API tối thiểu cho P7** (unblock ngay): `PUT /v1/admin/tenants/{t}/profile` (upsert Draft config) + `POST /v1/admin/tenants/{t}/publish` + `rollback` — nếu không, bước 3 của P7 không chạy được. Nên gom thêm `GET profile`, `GET conversations`, `GET/PUT policy` cho đủ §6.3.
2. **Retention cleanup job [G10]** — BackgroundService xóa `conversation_messages`/`conversation_sessions` hết `expires_at` (daily).
3. **Manual sync trigger [G9]** — endpoint ShopERP hoặc nút trong ShopFeatures gọi `SyncNowAsync(tenantId)`.
4. **Drift check [G16]** — reconcile đọc `/v1/knowledge/status` so `source_version`/count → log warn khi lệch (tối thiểu).
5. **Chạy CP3 RV** — `invoke-rv.ps1` + `ai-chat-widget.spec.ts` (cần `AI_CHAT_TEST_STORE_URL` + flag on 1 test tenant) + T1–T13 production subset + metering ledger verify.
6. **Chạy P7 pilot An Tâm** — nhập menu thật vào ShopERP → sync → seed docs → owner điền `[TODO]` → profile/publish → eval ≥95% → flag ON → 48–72h observation → sign-off.

### Để đạt "SRS 100% + end-user tự dùng" — cần phase mới

7. **P2 Admin UI `/admin/tenant-ai/*`** — task card(s) mới: pages theo SRS §3.1-P2 + NavMenu item + Sitemap entry + E2E spec (Gate 4). Phụ thuộc gap 3.1 (admin API đủ trước).
8. **P3 External Knowledge Gateway** — phase riêng.
9. **P4 Ecosystem AI + O4 Directory SSR widget** — phase riêng.

---

## 7. GHI CHÚ

- SRS chính nó chia P1/P2/P3/P4 — deferral P2+ là **có chủ đích**, không phải sai sót của plan. Phần "chưa 100% SRS" chủ yếu nằm ở P2 admin UI.
- Trong P1, gap nặng nhất là **3.1 (admin API thiếu)** vì nó block chính task P7 của plan hiện tại — không phải việc ngoài scope.
- Master plan status line đã stale: ghi "P4/P5 uncommitted · chờ LLM key" nhưng thực tế đã push `b0ac52b2` + LLM key set + non-degraded RV pass (`33daac76`). Cần sync lại project_state/master plan.
- Audit này REVIEW_ONLY — không thay đổi file/code nào ngoài báo cáo này.
