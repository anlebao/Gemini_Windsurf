# Task AI-P6 — Threat Suite T1–T13 + Eval + RV Scripts → CP3

> **Status:** ✅ APPROVED 2026-11-06
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

- [ ] T1–T13 local xanh · eval Grounded ≥95%/Unsupported ≤2% · CP3 full RV pass · evidence ghi card
