# MASTER PLAN: VẠN AN TENANT AI (`vanan-ai`) — Tenant Intelligence Platform

> **Ngày:** 2026-11-06 · **Trạng thái:** ✅ APPROVED — **P0..P6 CODE DONE + DEPLOYED** (CD `7717879f` success · LLM key set + non-degraded RV pass `33daac76`) · ⏳ **chờ CP3 RV + P6b (admin API — unblock P7) + P7 pilot An Tâm**
> **SRS:** `docs/requirements/van_an_tenant_ai_srs_v1.md` (v1.1 Consolidated Baseline — §0 changelog 12 verdict)
> **Precedent:** `docs/AI/plans/phi-nen-tang-metering-master-plan.md` (service độc lập cùng pattern)
> **Chiến lược thực thi:** phase nhỏ · card → duyệt → implement tuần tự · **chỉ push + RV ở checkpoint có giá trị cao** · local build+test là gate chính giữa các checkpoint

---

## 1. MỤC TIÊU

Build `vanan-ai` — service độc lập đầu tiên của Vạn An Tenant Intelligence Platform: multi-tenant AI assistant có tri thức/persona/giới hạn riêng per tenant, grounded trên knowledge của tenant, tích hợp metering, gợi ý món → cart theo Option C. Bảo toàn base code: không động `1_Shared`/`3_CoreHub`, mọi thay đổi ở Gateway/ShopERP là **additive + flag-gated**.

---

## 2. QUYẾT ĐỊNH ĐÃ CHỐT (user + SRS v1.1)

| # | Quyết định |
|---|---|
| D1 | Service riêng `9_TenantAI/vanan-ai` — sln riêng, entities KHÔNG vào `1_Shared/Domain.cs`, không reference `1_Shared`/`3_CoreHub` |
| D2 | DB = PostgreSQL + `pgvector` (container `pgvector/pgvector:pg16` trong compose riêng) · index **HNSW** default |
| D3 | Gemini: chat `gemini-3.5-flash-lite` + embedding `text-embedding-004` — **config-driven, verify API trước deploy** |
| D4 | Pilot = tenant F&B production (ưu tiên nhà hàng chay nếu có) |
| D5 | P1 có gợi ý đặt món → cart (AI không tạo order) |
| D6 | VPS riêng `vanan-ai` + nginx path `…/ai/api/` → internal, external port đóng |
| D7 | Auth 3 lớp: API key + caller **scopes** + **HMAC-signed Tenant Context** (secret tách API key) |

### Mục 【PO】 cần duyệt kèm (SRS §4)

| # | Item | Đề xuất của tôi |
|---|---|---|
| PO-1 | Metering check-fail + cache hết hạn: `degrade` vs `refuse` | `degrade` (structured-only, 0 LLM cost, AI vẫn trả lời được phần structured) |
| PO-2 | Degraded answer tính phí? | ServiceCode riêng `ai.chat.degraded` MeterOnly — đo được, không chặn |
| PO-3 | Session cap | 20 msg/session/giờ |
| PO-4 | Retention | 90 ngày |
| PO-5 | Product sync auto-trusted (ngoại lệ approval gate) | Có — nguồn là ShopERP hệ thống, không phải user-generated content |
| PO-6 | Tenant pilot cụ thể | Chọn ở P7 sau khi survey tenant F&B production |
| PO-7 | Billing tier Gemini khi free-tier thiếu | Quyết khi RV quota thật |

---

## 3. ROADMAP ↔ DEPLOY CHECKPOINTS

**Nguyên tắc:** push/deploy chỉ khi (a) validate infra, (b) validate AI value prop, (c) validate E2E storefront, (d) go-live pilot. Giữa các checkpoint: `dotnet build VanAn.TenantAi.sln` + test suite là gate.

| Checkpoint | Sau cards | Deploy gì | RV gì | Giá trị |
|---|---|---|---|---|
| **CP1 — Infra validation** | P0 + P0b | `vanan-ai` image → VPS + PG + nginx path | Health qua domain · external blocked · auth matrix (401/403) · PG connect | Bắt sớm lỗi VPS/nginx/PG/firewall/secrets khi code còn nhỏ — không debug chồng chất |
| **CP2 — AI core** | P1 + P2 + P3 | `vanan-ai` update | API-level: ingest → approve → chat → grounded answer · T1/T5/T11/T13 qua direct API | Chứng minh value prop AI **trước khi** đụng storefront — nếu pipeline tệ thì chưa tốn công UI |
| **CP3 — E2E storefront** | P4 + P5 + P6 | `vanan-ai` + **Gateway + ShopERP** (lần đầu đụng main sln — additive, flag off) | Full RV L1–L4 · T1–T13 · widget→chat→card→cart→order Option C · metering deny/degrade | Validate toàn bộ luồng user-facing trên production, flag tắt cho tenant thường |
| **CP4 — Pilot go-live** | P7 | Flag ON pilot tenant | RV production trên tenant thật + knowledge readiness + sign-off | Go-live |

**Push policy:** vanan-ai deploy độc lập → push `main` bình thường (không chạm VanAn.sln). P4/P5 đụng Gateway/ShopERP → bắt buộc `guard-check.ps1` + `dotnet build VanAn.sln` + Core.Tests/ShopERP.Tests trước push. FAST PUSH chỉ khi CI đã validate trong session.

---

## 4. TASK CARDS ↔ GATES (9 cards — `docs/AI/tasks/tenant_ai/`)

| Card | Gate | Nội dung | Push/RV | Phụ thuộc | Trạng thái |
|---|---|---|---|---|---|
| `task_ai_p0_skeleton` | G1 | sln + 5 projects + Dockerfile + compose PG/pgvector + health + CI wiring (image only) | Build image | — | ✅ DONE `983d35be` (tests 3/3) |
| `task_ai_p0b_auth` | G1 | CallerKeys + scopes + HMAC tenant ctx + tenant allowlist + auth middleware | **→ CP1** | P0 | ✅ DONE code `983d35be` (18/18) · ⏳ CP1 deploy chờ VPS |
| `task_ai_p1_schema` | G2 | Domain entities + EF config + migrations + pgvector | local | P0 | ✅ DONE `983d35be` (23/23, 5 PG Testcontainers) |
| `task_ai_p2_ingest` | G2 | Ingest API + approval gate + ShopERP sync client + operator script + GeminiEmbeddingService + chunk pipeline | local | P1 | ✅ DONE `a1b31795` |
| `task_ai_p3_chat` | G3 | IntentRouter + PolicyEngine + UsagePermission + Retriever + ILlmChatClient/GeminiChatClient + AnswerValidator + eval harness | **→ CP2** | P1, P2 | ✅ DONE code `8dae1fce` (99/99, eval gate pass) · ⏳ CP2 deploy chờ VPS |
| `task_ai_p4_storefront` | G4 | Gateway PublicTenantAiController + ctx signing + AiChatWidget UI.Platform + KhachLink tích hợp + product cards→cart | local (main sln validate) | P3 | ✅ DONE `b0ac52b2` (deployed · ⏳ e2e spec chờ CP3) |
| `task_ai_p5_metering` | G5 | MeteringApiClient trong vanan-ai + check/degrade/ingest + `TenantAiEnabled` flag + widget enable | local | P3 | ✅ DONE `b0ac52b2` (deployed · rate cards + caller đã tạo production) |
| `task_ai_p6_tests_rv` | G5 | T1–T13 suite + NFR-10 eval set + RV scripts production | **→ CP3** | P4, P5 | ✅ CODE DONE `7717879f` (153/153, eval 41) · ⏳ CP3 RV chưa chạy |
| `task_ai_p6b_admin_api_hygiene` | G5 | Admin API đủ §6.3 (profile/policy/publish/rollback/conversations/usage) + retention cleanup + tenant purge + manual sync + drift check — **unblock P7** | local (main sln validate) | P3 | 🟡 DRAFT 2026-11-07 (audit `tenant-ai-srs-compliance-audit.md`) |
| `task_ai_p7_pilot` | G5 | Model verify API + knowledge readiness pilot + flag on + RV production + sign-off | **→ CP4** | P6 | 🟡 IN PROGRESS — B1–B4-API DONE · chờ observation 48–72h |
| `task_ai_p8_unified_chat_widget` | post-P1 | Unified widget 1 bubble: AI mode + "Chat với chủ quán" → RealtimeChatPanel inline + auto-escalate khi refuse | local (main sln) | P4 | ✅ APPROVED 2026-11-07 |
| `task_ai_p9_inbox_ai_review` | post-P1 | ShopInbox tab "AI trả lời khách" read-only qua admin API P6b | local (main sln) | P6b | ✅ DEPLOYED `06a8b993` |
| `task_ai_p10_admin_ui` | **P2** | Admin UI `/admin/tenant-ai/*` đủ §3.1-P2: hub + profile/preview/publish + knowledge CRUD/approve + policy + usage + nav/sitemap + E2E | local (main sln) | P6b, P9 | ✅ DEPLOYED `23b061a2` 2026-11-07 — e2e prod 6/6 |

**Gate rule:** không viết card tiếp theo song song — implement tuần tự, card xong = tests xanh + note deviation vào card.

---

## 5. THIẾT KẾ CHI TIẾT (tóm tắt — chi tiết SRS §5–§7)

### 5.1 Layout code

```
9_TenantAI/vanan-ai/
  VanAn.TenantAi.sln            # KHÔNG vào VanAn.sln (pattern metering)
  Dockerfile                    # SDK 8.0 → aspnet 8.0
  docker-compose.tenantai.yml   # api + postgres pgvector/pgvector:pg16 (volume riêng)
  src/
    VanAn.TenantAi.Api/         # minimal API — /v1/* + auth middleware + health
    VanAn.TenantAi.Domain/      # entities (pure) — KHÔNG vào 1_Shared
    VanAn.TenantAi.Contracts/   # DTO (ChatRequest/Response, ingest DTOs, admin DTOs)
    VanAn.TenantAi.Infra/       # TenantAiDbContext (EF Core PG + Pgvector), repos
    VanAn.TenantAi.Ai/          # IntentRouter, PolicyEngine, UsagePermission,
                                # Retriever, ILlmChatClient, IEmbeddingProvider,
                                # GeminiChatClient, GeminiEmbeddingService, Validator
  tests/
    VanAn.TenantAi.Tests/       # unit + integration (Testcontainers PG hoặc docker compose test)
```

### 5.2 Thay đổi main sln (additive, flag-gated — chỉ P4/P5)

| File | Thay đổi |
|---|---|
| `2_Gateway/Controllers/PublicTenantAiController.cs` | MỚI — anonymous + rate-limited (pattern `PublicBookingController`), resolve tenant → sign ctx → forward |
| `2_Gateway/Services/TenantAiContextSigner.cs` | MỚI — HMAC sign `X-Tenant-Ctx` |
| `UI.Platform/Components/Ai/AiChatWidget.razor` | MỚI — chat widget + product card render (UI Platform bắt buộc) |
| `5_WebApps/KhachLink/…` | Mount widget khi `GET /v1/config/widget` trả `aiEnabled=true` |
| `5_WebApps/ShopERP/Services/TenantAiApiClient.cs` + `TenantAiKnowledgeSyncService.cs` | MỚI — pattern `MeteringApiClient` (timeout, circuit breaker, self-disable) |
| `5_WebApps/ShopERP` feature flag | `TenantAiEnabled` qua `IShopFeatureSettingsService` |

**Không động:** `1_Shared/Domain.cs` (Gate 5 an toàn — entities AI ở sln riêng), `3_CoreHub` code hiện hữu, OrderService/checkout.

### 5.3 Secrets (env-only, không commit)

`TenantAiLlm__ApiKey` · `TenantAi__GatewayCallerKey` · `TenantAi__ShopErpACallerKey` · `TenantAi__CtxSigningSecret` (Gateway + vanan-ai) · `Metering__*` (vanan-ai→metering) · `TenantAi__BaseUrl` (ShopERP client) · Bootstrap caller seed env (pattern `Metering__BootstrapCaller`).

### 5.4 CI/CD wiring (theo metering P1)

- `deployment-impact.yml`: app `tenantai` → `9_TenantAI/**`.
- `cd-multivps.yml`: matrix `tenantai` → `9_TenantAI/vanan-ai/Dockerfile` → `ghcr.io/anlebao/vanan-ai` + vào fail-closed fallback deploy_set (Pattern #32).
- VPS `vanan-ai` mới: provision docker + `/opt/vanan-ai` + compose + secrets (giống metering P5).
- Nginx gateway: location `…/ai/api/` → `http://<internal-ip>:<port>/` — deploy job nginx riêng hoặc kèm gateway deploy.

---

## 6. RỦI RO & TRIGGERS DỪNG

| Rủi ro | Trigger dừng / xử lý |
|---|---|
| VPS provision chậm (GCP manual như metering P5) | CP1 block → provision trước khi P0b; nếu không kịp, CP1 dùng VPS `vanan-metering` tạm? — KHÔNG, giữ nguyên tắc VPS riêng; đợi provision |
| Gemini verify fail (model ID không tồn tại/không được cấp) | Task card có bước verify sớm ở P3; fallback = đổi `TenantAiLlm__Model` sang model hiện dùng trong `SmartEntryLlmOptions` đã chạy production |
| pgvector trên PG16 image cần `CREATE EXTENSION vector` | Migration/ensure-schema script chạy `CREATE EXTENSION IF NOT EXISTS vector` ở startup (idempotent) |
| Knowledge sync drift | [G16] staleness + reconcile; drift >24h → admin alert |
| Main-sln changes phá build | Additive only + flag off; guard-check + full test trước CP3 |
| Chi phí Gemini free-tier | NFR-6 token metrics + degrade path; PO-7 escalation |
| **Hard stop giữ nguyên:** cross-tenant leak test T1 fail ở bất kỳ đâu → STOP, không qua gate |

---

## 7. THỨ TỰ THỰC THI

```
P0  skeleton        ── local build ──┐
P0b auth            ── local tests   ├─→ CP1: deploy + smoke RV (infra)
P1  schema          ── local build   │
P2  ingest          ── local tests   │
P3  chat pipeline   ── local tests   ├─→ CP2: deploy + API RV (AI core)
P4  storefront      ── main sln test │
P5  metering+flag   ── main sln test │
P6  tests+rv        ──               ├─→ CP3: full deploy + RV L1-L4 + T1-T13
P7  pilot           ──               └─→ CP4: flag on + production RV + sign-off
```

Mỗi card: đọc SRS section liên quan → implement → build + test → ghi "Delivered" vào card (pattern metering) → user duyệt card sau.

---

## 8. APPROVALS

- [x] User approve master plan + 9 task cards — **2026-11-06**
- [x] PO-1..PO-7 — **RESOLVED 2026-11-06 theo đề xuất:** degrade · `ai.chat.degraded` MeterOnly · cap 20/giờ · 90d · auto-trusted · pilot survey P7 · billing khi cần
- [x] P0 + P0b + P1 + P2 + P3 — **DONE code 2026-11-06** (commits `983d35be`/`a1b31795`/`8dae1fce` · tests 99/99 · guard PASSED)
- [x] Provision VPS `vanan-ai` + secrets — **DONE 2026-11-06** (e2-small asia-southeast1-b · int `10.148.0.7` · ext `34.21.228.114` · `deploy-tenantai` CD job · 8 GH secrets) — **CP1+CP2 DEPLOYED + RV PASS** (`d8ccfe8c`)
- [ ] `TENANTAI_LLM_APIKEY` (Gemini) — user cung cấp → non-degraded verify + latency P95 ≤8s
- [x] P4 storefront — **DONE code 2026-11-06** (build 0 err · tests mới 18/18 + 26/26 · ShopERP.Tests 306/306 · chưa push — chờ CP3)
- [x] P5 metering + flag — **DONE code 2026-11-06** (tests 118/118 vanan-ai · ShopERP.Tests 306/306 · guard ALL PASSED · chưa push — chờ CP3). Ops trước CP3: `vanan-ai:<key>` vào `METERING_CALLERS` + rate cards `ai.chat.message`(Deny)/`ai.chat.degraded`/`ai.embedding.batch`(MeterOnly)
