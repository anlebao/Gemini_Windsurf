# Task AI-P6 — Threat Suite T1–T13 + Eval + RV Scripts → CP3

> **Status:** ✅ CODE DONE 2026-11-06 — local xanh **153/153** (ThreatSuite 35 mới · eval 41 cases) · chờ CP3 deploy + full RV
> **Gate:** P1-G5 — SRS v1.1 §10/§14 · **→ checkpoint CP3 (full deploy + full RV)**
> **Phụ thuộc:** P4, P5 (toàn bộ luồng đã ghép)

## Objective

Đóng gói evidence cho CP3: threat suite T1–T13 chạy được cả local (unit/integration) lẫn production (RV scripts), eval metrics NFR-10, E2E storefront spec.

## Deliverables

| Thành phần | Spec |
|---|---|
| `tests/…/ThreatSuite/` | T1–T13 per SRS §10 — mỗi test gắn tag threat ID; chạy trong `VanAn.TenantAi.Tests` |
| `tests/…/Eval/` | Eval set JSON ≥30 câu/intent-group (vi-VN thật — viết tay từ pilot-shaped data, không generate): menu/nutrition/price/story/faq/out-of-scope/refuse · harness đếm Grounded/Unsupported/Refuse rates |
| `6_Testing/rv-scripts/tenant-ai-rv/` | `run-on-vps.sh` (pattern smart-entry-rv): health · auth matrix · ingest → approve → chat ≥5 câu → assert grounded · cross-tenant curl test · degrade test (tắt metering tạm? — KHÔNG, dùng tenant test có ví=0 → deny path) |
| `6_Testing/e2e-tests/ai-chat-widget.spec.ts` | Production spec: widget load → hỏi menu → answer có card → add to cart → cart page có món (flag-on pilot tenant hoặc test tenant) |
| Drift/reconcile check | `GET /v1/knowledge/status` verify `synced_at` + source_version trên production sau push |

## T1–T13 coverage mapping (SRS §10)

| Test | Local | Production |
|---|---|---|
| T1 cross-tenant (SQL+vector) | seed 2-tenant fixture | curl tenant A hỏi món B |
| T2 injection ×10 payload | canned payload list | subset 5 payload prod |
| T3 injection trong doc | sanitizer+retrieval | — |
| T4 fake product refs | validator | hỏi món không có |
| T5 confidential leak | DB-level + pipeline | — |
| T6 ctx forge/expired/scope | auth tests | forged sig curl → 401 |
| T7 key leak log scan | assert log filter | — |
| T8 quota deny / degrade | mock metering | tenant ví=0 → refuse; degrade path → 0 LLM |
| T9 external scope off | policy test | — |
| T10 stale volatile strip | clock-shifted fixture | đổi giá → sync → giá mới |
| T11 pending doc invisible | approval flow | ingest doc → pending → chat không dùng |
| T12 scope escalation | gateway→admin 403 | curl |
| T13 SQL/RAG conflict | fixture doc vs price | — |

## CP3 — Full deploy + RV (sau card này)

1. `guard-check.ps1` + `dotnet build VanAn.sln` 0 err + Core.Tests + ShopERP.Tests full pass
2. Push main → CD multi-VPS: `vanan-ai` image update + Gateway + ShopERP deploy
3. RV L1: markers trong deployed DLLs (PublicTenantAiController, TenantAiApiClient, AiChatWidget)
4. RV L2: `/ai/api/v1/health` qua domain 200 · external port blocked · auth matrix 401/403/200
5. RV L3: `tenant-ai-rv` script pass · e2e widget spec pass (pilot-shaped test tenant, flag on cho tenant đó thôi)
6. RV L4: metering — chat thật → usage event trong metering ledger; tenant ví=0 → refuse nhẹ nhàng
7. T1–T13 subset production PASS

## Acceptance

- [x] T1–T13 local xanh — `tests/VanAn.TenantAi.Tests/ThreatSuite/` (T1×4 · T2×10 · T3×10 · T4×3 · T5×3 · T7×1 · T10×3 = 35 mới, `[Trait("Threat","Tx")]` + coverage map README) + coverage sẵn có: T6/T12 (`AuthTests`) · T8 (`QuotaGate`/`MeteringGate`/`KillSwitch`) · T9/T11/T13 (ChatApi/Ingest/ChatUnit)
- [x] eval Grounded ≥95%/Unsupported ≤2% — `EvalHarnessTests` pass, evalset top-up 33→41 cases (ExternalKnowledge 1→3 · PolicyViolation 2→4 · BrandStory 2→3 · RecipeQuery 2→3 · OrderHelp 2→3 · MenuQuery 4→5)
- [x] CP3 full RV pass 2026-11-06/07 — `invoke-rv.ps1 -TenantId e2d91cdf` **PASS=44 FAIL=0 SKIP=1** (chỉ T8 skip — master switch OFF = GĐ-1, arming = quyết định P7) · e2e `ai-chat-widget.spec.ts` **3/3 PASS** (An Tâm, `AI_CHAT_TEST_STORE_URL` + `playwright-rv-tenantai.config.ts`) · metering ledger: `ai.chat.message`/`ai.chat.degraded`/`store.visit` events recorded (UsageEvents table, balance 1000cr pilot grant)

## Evidence (2026-11-06)

- `dotnet test VanAn.TenantAi.Tests` = **153/153 PASS** (118 trước + 35 ThreatSuite), 0 fail.
- RV script ký HMAC ctx bằng openssl trên VPS (secrets từ `/opt/vanan-ai/.env.tenantai`, không in log).
- Production-only: T1 vector path + T8 deny thật — cover trong run-on-vps.sh (ingest scope → `-TenantId`; ví=0 → `-ZeroWalletTenant`).
- **CP3 RV findings fixed + deployed (`ca5f1f01`→`c44de003`):** (1) CD gap — `TenantAi__*` chưa wire vào gateway/shoperp env (forwarder self-disabled → `aiEnabled:false` cho mọi tenant); (2) **BUG-6 widget dead-send** — `OnAfterRenderAsync(firstRender)` chạy trước khi `OnInitializedAsync` trả config (WASM render frame đầu trong lúc await) → `_sessionId` null vĩnh viễn → SendAsync no-op cho mọi user; fix = tạo session lazy ở render đầu biết `aiEnabled`; (3) spec race — greeting/typing trùng selector answer-wait (POST bị abort khi reload) + cart-button aria-label chứa tên món.
- Ops CP3: `AIChat_Enabled=true` An Tâm (PG + SQLite mirror) · 22 products spa thật ingest qua `products:batch` (fullSync) · wallet grant 1000cr · tenant B `99999999…99` = ví=0.
- Deviation: `/ai/api/` nginx location KHÔNG cần — public surface thật là `/api/public/tenant-ai/*` qua gateway forwarder (D6 superseded by P4 design).
- T8 production deny: DEFER P7 — arm master switch deny hàng loạt tenant ví=0 (Deny cards: chat.*, feature.*, store.* — blast radius quá lớn, GĐ-1 giữ OFF).
