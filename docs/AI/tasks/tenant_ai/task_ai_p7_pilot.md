# Task AI-P7 — Pilot Rollout: Model Verify + Knowledge Readiness + Go-live

> **Status:** ✅ APPROVED 2026-11-06
> **Gate:** P1-G5 — SRS v1.1 [D3][D4] · §14 · **→ checkpoint CP4 (production RV + sign-off)**
> **Phụ thuộc:** **P6b DONE** (admin API §6.3 + hygiene — BLOCKER: bước 3 cần `PUT profile` + `publish`, bước 4 dùng `Get-AiConversations`/`Get-AiUsage`) · P6 (CP3 PASS 44/0/1skip — An Tâm `e2d91cdf` đã có `AIChat_Enabled` + 22 products ingest + ví 1000cr) · PO-6 (tenant — thực chất đã chọn An Tâm ở CP3) · PO-7 nếu phát sinh billing

## Objective

Đưa AI lên production thật cho 1 tenant F&B. Chỉ enable sau khi knowledge readiness đủ — widget trống knowledge là thất bại UX, không phải bug code.

## Bước 1 — Model verify (trước mọi thứ)

- Call trực tiếp Gemini API từ VPS `vanan-ai`: `generateContent` với `gemini-3.5-flash-lite` + `embedContent` với `text-embedding-004` → confirm model IDs tồn tại + latency + dims trả về khớp config `EmbeddingDimensions`.
- Fail → đổi `TenantAiLlm:Model` sang model đã production-verified trong `SmartEntryLlmOptions` (codebase đang dùng) → re-verify. Ghi quyết định vào card.

## Bước 2 — Chọn pilot tenant (PO-6)

- Survey tenant F&B production: menu đủ món (≥15), có mô tả, cấu trúc dữ liệu tốt. **Ưu tiên nhà hàng chay nếu tồn tại** (đủ menu+dinh dưỡng+brand story — đúng đánh giá khả thi gốc). Fallback: Vạn An Cafe.
- Confirm với user trước khi tiếp.
- **Note:** CP3 ops đã dùng **An Tâm `e2d91cdf`** (22 products ingest + ví 1000cr + `AIChat_Enabled`). Nếu user confirm An Tâm → bước này chỉ cần xác nhận, không survey lại.

## Bước 3 — Knowledge readiness (dùng operator tooling P6b/P2)

- [ ] **Menu trong ShopERP (source of truth):** xác nhận/nhập menu thật vào ShopERP của tenant. Nightly reconcile **re-push fullSync từ ShopERP DB** — nếu menu không có trong ShopERP, reconcile sẽ ghi đè/xóa knowledge đã ingest tay ở CP3. Sau nhập → `POST /api/tenant-ai/sync` (P6b) hoặc `fullSync` → `GET /v1/knowledge/status` count khớp menu
- [ ] Brand story: soạn doc (lấy từ profile tenant thật / hỏi chủ quán) → **owner điền hết `[TODO]` facts** (năm thành lập, địa chỉ, giờ, SĐT — không ingest doc còn placeholder) → ingest → **approve**
- [ ] FAQ: ≥5 câu thật của loại hình (giờ mở cửa, đặt bàn, giao hàng, dị ứng) → ingest → approve
- [ ] AI profile: tên/xưng hô/phong cách theo bản sắc tenant → `Put-AiProfile` → `preview-chat` ≥5 câu → `Publish-AiConfig` (P6b ops)
- [ ] Eval set tenant này: ≥20 câu hỏi khách thật → chạy eval → Grounded ≥95% trước khi bật. **Cơ chế:** `Invoke-AiEval` trong `scripts/tenant-ai-ops.ps1` (đọc cases JSON → loop `preview-chat` → chấm Grounded/Unsupported) — thêm function nếu chưa có

## Bước 4 — Enable + RV production (CP4)

- [ ] Cả 2 flag ON cho đúng tenant pilot: `AIChat_Enabled` (widget — đã ON từ CP3) + `TenantAiEnabled` (sync/metering gate) — PG + verify mirror
- [ ] Flag regression: tắt `AIChat_Enabled` → widget ẩn (SRS P1-G5 "flag off → widget ẩn") → bật lại → widget hiện
- [ ] Widget config qua domain trả `aiEnabled=true` · widget render trên storefront thật
- [ ] Chat e2e ≥10 câu production: menu/calories/story/out-of-scope/competitor-deflect — log đẹp, grounded, refuse đúng
- [ ] **Session resume:** chat ≥2 câu → reload trang → history còn (`GET /v1/chat/sessions/{sid}/messages` — FR-7)
- [ ] **Rate-limit spot-check:** burst >60 req/min 1 IP → 429 · cap 20 msg/session/giờ (PO-3 — verify 1 session chạm cap → refuse nhẹ nhàng)
- [ ] **Kill-switch:** `TenantAi:Enabled=false` trên vanan-ai → widget config trả `aiEnabled=false` → restore
- [ ] Add to cart → checkout flow Option C hoạt động (order tạo bởi Gateway, đúng tenant)
- [ ] Metering: usage events xuất hiện trong ledger tenant đó · deny path verify (tạm set ví thấp → refuse → restore)
- [ ] **Quyết định arm Deny scope (T8 defer từ P6):** arm enforcement cho `ai.chat.message` — xác định blast radius (`feature.*`/`store.*` có chung master switch không) → ghi quyết định vào card
- [ ] Cross-tenant trên production: hỏi món tenant khác → không tìm thấy
- [ ] Latency thực đo: P95 ≤8s trên ≥20 requests
- [ ] Để chạy 48–72h quan sát: conversation review (`Get-AiConversations`/`Get-AiUsage` — P6b ops), tỷ lệ refuse, unsupported claims, chi phí token → báo cáo user
- [ ] Trong observation: verify `RetentionCleanupService` pass log ≥1 lần (G10) · drift warning hoặc status count khớp sau reconcile (G16) · quyết `ai.embedding.batch` có cần ingest để đo chi phí (deviation P5 "thêm khi P7 cần đo") — nếu không đo, ghi accept

## Rollback plan

- `TenantAiEnabled`/`AIChat_Enabled` OFF → widget biến mất ngay (không deploy)
- `TenantAi:Enabled=false` env → toàn hệ tắt
- Rate card `ai.chat.message` enforcement tạm đổi MeterOnly nếu deny gây nhiễu
- Abort hoàn toàn: `Remove-AiTenant -Confirm` (purge endpoint P6b) — xóa sạch knowledge + conversations + config tenant pilot

## Acceptance

- [ ] Pilot sign-off từ user sau ≥48h quan sát · metrics report đính kèm card · lessons-learned ghi vào card + cân nhắc cập nhật rules/knowledge-base

> **Scope note (SRS 100%):** Card này đóng **SRS P1**. SRS tổng 100% cần thêm: `task_ai_p8_admin_ui` (P2 `/admin/tenant-ai/*` + NavMenu/Sitemap + E2E — phụ thuộc P6b admin API) · P3 External Knowledge Gateway · P4 Ecosystem AI + O4 Directory SSR widget. Roadmap sau pilot sign-off — KHÔNG gom vào P7 (audit `tenant-ai-srs-compliance-audit.md` §4, §6).
