# Task AI-P10 — Admin UI `/admin/tenant-ai/*` (SRS P2 — tenant self-service)

> **Status:** ✅ DONE 2026-11-07 — DEPLOYED `23b061a2` · CD Multi-VPS #42348348820 + #42351336029 + #42352725876 ALL SUCCESS · RV sweep 45 checks + e2e production **6/6 PASS**
> **Gate:** SRS v1.1 §3.1-P2 · FR-8 · §6.3 · NFR-10 · **main sln — additive, flag-gated `TenantAiEnabled`**
> **Phụ thuộc:** P6b admin API (DONE — 9 endpoints) · P9 (inbox review pattern) · P7 sign-off (không block code)
> **Gap nguồn:** `docs/AI/plans/tenant-ai-srs-compliance-audit.md` §4.1 — gap lớn nhất để đạt "SRS 100% + end-user tự dùng"

## Objective

Chủ quán (Owner/StaffOrAbove) tự quản trợ lý AI trong ShopERP — không còn phụ thuộc `tenant-ai-ops.ps1`. Đủ 10 chức năng SRS §3.1-P2 qua 1 hub page + sub-pages, dùng admin API P6b sẵn có.

## Hiện trạng

- Backend `/v1/admin/*` đủ: profile GET/PUT · publish/rollback · policy GET/PUT · conversations (+drill) · usage · knowledge pending/approve/reject · documents POST/DELETE · knowledge/status · preview-chat · purge.
- `TenantAiApiClient` (ShopERP) đã có: knowledge status · pending docs · conversations — **cần thêm methods cho profile/policy/publish/preview-chat/documents**.
- Surface hiện có: `ShopFeatures` toggles + sync button · `ShopInbox` tab "AI trả lời khách" (P9 — giữ, không trùng scope).
- Menu: `ShopErpMenuService.cs` (groups tại ~:334, :399, :489) · Sitemap `Sitemap.razor` (~:99 pattern `sitemap-link`).

## Deliverables

### 1. `TenantAiApiClient` — methods mới (pattern `Send<T>` + AdminTimeout)

| Method | Endpoint |
|---|---|
| `GetProfileAsync` / `PutProfileAsync` | `GET/PUT /v1/admin/tenants/{t}/profile` |
| `PublishConfigAsync` / `RollbackConfigAsync` | `POST …/publish` · `POST …/rollback` |
| `GetPolicyAsync` / `PutPolicyAsync` | `GET/PUT …/policy` |
| `PreviewChatAsync` | `POST /v1/admin/tenants/{t}/preview-chat` (timeout 30s — LLM) |
| `SendDocumentAsync` / `DeleteDocumentAsync` | `POST/DELETE /v1/knowledge/documents*` — **scope ingest đã có?** verify caller scopes: `Send` hiện sign ctx caller `TenantAi:CallerId` — nếu caller chỉ `admin` thì documents POST cần scope `ingest` — check `SeedCaller` scopes `"ingest","admin"` (cả 2 — OK) |
| `GetUsageAsync` | `GET …/usage?days=` |

DTOs mirror `VanAn.TenantAi.Contracts/AdminDtos.cs` + `KnowledgeDtos` (per-host copy, không shared DLL).

### 2. Hub page `/admin/tenant-ai` — `Components/Pages/Admin/TenantAi/TenantAiHub.razor`

- `[Authorize]` Owner/StaffOrAbove + `ITenantProvider` · flag `TenantAiEnabled` off → empty state hướng dẫn bật ở ShopFeatures.
- Status cards (từ `knowledge/status` + `usage`): products synced · docs pending badge · stale flag · last sync · usage 30d (msgs/tokens/refused/degraded).
- Quick links → các sub-page dưới.

### 3. Sub-pages (cùng folder, route `/admin/tenant-ai/*`)

| Route | Chức năng | Backend |
|---|---|---|
| `/profile` | Form aiName + persona (xưng hô, phong cách, greeting, suggestions — map `persona_json` keys) + business profile → `PUT profile` (tạo draft) + nút **Preview** (preview-chat sandbox: input câu → answer, 0 persist/metering) + **Publish** + **Rollback** (dropdown version) | profile · publish · rollback · preview-chat |
| `/knowledge` | Tab "Docs" (list + filter pending/approved/rejected + upload doc {type,title,content,classification} + delete) · Tab "Pending" (approve/reject + reason — **approval gate, core flow**) | documents · pending · approve · reject |
| `/policy` | refused topics editor (chips add/remove) · intent-scope toggles (12 intents → `intent_scopes_json`) · externalAllow = read-only "P3" | policy GET/PUT |
| `/conversations` | **Reuse pattern P9** — list + drill read-only (nếu ShopInbox tab đủ thì link ra, tránh trùng) | conversations |
| `/usage` | Grid theo ngày: messages/refused/degraded/prompt/completion tokens + tổng | usage |

### 4. Nav + sitemap + Gate 4

- `ShopErpMenuService`: nhóm **"Cấu hình & thiết lập"** (Owner+SystemAdmin ~:399) + item `"Trợ lý AI (Tenant AI)"` icon `robot` → `/admin/tenant-ai` — role-gated Owner/StaffOrAbove.
- `Sitemap.razor`: `sitemap-link` cùng nhóm Settings.
- E2E spec `6_Testing/e2e-tests/tenant-ai-admin.spec.ts` (Gate 4 — UI change): login owner → hub status · upload doc → pending → approve · profile PUT → publish → widget-config greeting đổi.

## Ranh giới (HARD)

- UI Platform components only · Business logic KHÔNG trong razor — gọi `ITenantAiClient`.
- Mọi thao tác mutation → `Idempotency-Key`/confirm dialog (publish/purge-like actions có confirm).
- KHÔNG hiển thị caller key/signing secret · không page cho purge (ops-only).
- Preview-chat: label rõ "Draft preview — không ảnh hưởng khách thật".

## Tests bắt buộc (ShopERP.Tests)

- [ ] Client: map đúng contracts (profile/policy/usage/preview) · fail → null không throw · disabled → zero HTTP
- [ ] Flag off → hub empty state + zero HTTP (pattern `FlagOff_MakesZeroHttpCalls`)
- [ ] Approve/reject flows: client gửi đúng payload + reason required

## Acceptance

- [ ] Tests xanh · build VanAn.sln 0 err · ShopERP.Tests không regression · guard PASSED
- [ ] Owner An Tâm tự: sửa greeting → preview → publish → widget storefront hiện text mới · upload FAQ → approve → AI trả lời được · xem usage — **toàn bộ không cần ops script**
- [ ] Eval-gate trước publish (NFR-10): nếu ≤1 ngày effort thì cảnh báo khi publish mà eval chưa chạy (soft warning), không block — ghi deviation nếu bỏ qua

## Không được làm

- Không thêm P3 external knowledge UI (toggle external = read-only) · không purge UI · không sửa vanan-ai API trừ khi thiếu thật (thì deviation note)

## Delivered

**vanan-ai (additive — deviation vì backend thiếu thật cho UI):**
- `GET /v1/admin/knowledge/documents?status=&page=&size=` — paged docs list, filter `pending_review|approved|rejected` (invalid → empty fail-closed), item gồm `content` + `sanitized`. Lý do: `/admin/knowledge/pending` chỉ trả summary không content → không review được; không endpoint nào list approved/rejected.
- `ProfileResponse` +`RecentVersions` (≤20, mọi status, mới nhất trước) — rollback dropdown cần version history; trước chỉ trả draft+published.

**ShopERP:**
- `TenantAiApiClient`: +10 methods (`GetProfileAsync`, `PutProfileAsync`, `PublishConfigAsync`, `RollbackConfigAsync`, `GetPolicyAsync`, `PutPolicyAsync`, `PreviewChatAsync` 30s, `GetDocumentsAsync`, `DeleteDocumentAsync`, `GetUsageAsync`) + DTO mirror contracts. DTO hậu tố `Response` (tránh trùng tên class page trong namespace `Pages.Admin.TenantAi`).
- `TenantAiProfileJson` — round-trip merge persona/business JSON ↔ form fields (giữ unknown keys; keys persona `pronoun/style/greeting/suggestions`, business `about/address/phone/opening_hours` + alias vi).
- `Components/Shared/AiConversationReviewPanel.razor` — extract từ ShopInbox P9 (reuse, tránh trùng); ShopInbox refactor dùng panel, giữ lazy-mount + flag gate.
- 6 pages `Components/Pages/Admin/TenantAi/`: `TenantAiHub` (`/admin/tenant-ai` — status cards + quick links + flag-off empty state zero-HTTP) · `TenantAiProfile` (form + preview-chat sandbox label "Draft preview" + publish confirm **kèm soft-warning eval NFR-10** + rollback dropdown) · `TenantAiKnowledge` (tab Chờ duyệt approve/reject+reason · tab Tài liệu filter/upload/delete confirm) · `TenantAiPolicy` (12 intent toggles → `{"Intent":false}` + refused-topics chips + externalAllow read-only) · `TenantAiConversations` (reuse panel) · `TenantAiUsage` (7/30/90 ngày grid + tổng).
- Nav `ShopErpMenuService` nhóm "Cấu hình & thiết lập" + item robot `/admin/tenant-ai`; Sitemap `link-tenant-ai`. Auth: `Policy = "StaffOrAbove"` (Owner kèm trong policy).
- Eval-gate NFR-10 → soft warning trong confirm publish (không block — theo card).

**Tests:** AdminApiTests +2 (`Documents_List_FilterByStatus_IncludesContent`, `Profile_IncludesRecentVersions`) → **15/15** · `TenantAiAdminClientTests` **14/14** (paths/payloads/fail-safe/zero-HTTP) · `TenantAiHubTests` bUnit **2/2** (flag-off empty state + zero HTTP · flag-on load 1 lần).

**Validate:** vanan-ai sln build 0 err · VanAn.sln build 0 err · ShopERP.Tests **330/330** (+3 regression bUnit → **333**) · **guard-check ALL PASSED** (`guard-report-20261107-133834.txt`).

**✅ DEPLOY + RV DONE (2026-11-07):**
- `72700a96` → CD #42348348820 (vanan-ai + shoperp deployed, smoke PASS).
- **RV sweep-first** (`rv-tenantai-admin-sweep.cjs` + spec trên An Tâm `e2d91cdf`): 45 checks → **39 PASS · 6 FAIL** → phân loại: 4 FAIL probe-timing (Blazor circuit warmup, timeout ngắn) + **2 BUG THẬT**:
  - **BUG-1 (critical):** `TenantAiKnowledge` pending tab crash 500 khi có ≥1 pending doc — `@bind="_rejectReasons[d.Id]"` đọc dictionary indexer chưa seed → `KeyNotFoundException` trong BuildRenderTree (prerender + interactive). Che toàn bộ approval-gate flow. Fix `2fa5cf67`: seed `_rejectReasons.TryAdd(d.Id,"")` khi load pending + regression bUnit `TenantAiKnowledgeTests`.
  - **BUG-2 (cosmetic):** rollback dropdown label render literal `v@v.Version` — Razor parse `v@v.` như email. Fix `23b061a2`: `v@(v.Version)` + regression bUnit `TenantAiProfileTests`.
  - **Spec fixes (test-only):** blur textarea trước click nút disabled-until-valid (`@bind`=onchange → Playwright deadlock) · click `tab-pending` sau upload (page không auto-switch) · profile test giờ **self-restore**: rollback về bản published gốc → không đổi config production.
- `2fa5cf67` → CD #42351336029 · `23b061a2` → CD #42352725876 — ShopERP deployed, smoke PASS.
- **RV final production:** L1 image `23b061a2` cả 2 VPS + markers (Contracts `RecentVersions`, ShopERP `tenant-ai-hub`, vanan-ai health 200) · L2 6 routes 200 · L3 e2e `tenant-ai-admin.spec.ts` **6/6 PASS** (nav · sitemap · hub render · cards+links · upload→pending→approve→delete · draft→preview→publish→rollback restore).
- **Production cleanup:** orphan doc "E2E FAQ" đã xóa · published profile restore về "Shi" (v10) — `rv-tenantai-admin-cleanup.cjs` tái dùng được.
- **RV tooling:** `playwright-rv-tenantai-admin.config.ts` + `rv-tenantai-admin-auth.cjs` (storageState impersonate An Tâm) + `rv-tenantai-admin-sweep.cjs` (45-check sweep) + `rv-tenantai-admin-cleanup.cjs`.
