# Task AI-P7 — Pilot Rollout: Model Verify + Knowledge Readiness + Go-live

> **Status:** ✅ APPROVED 2026-11-06
> **Gate:** P1-G5 — SRS v1.1 [D3][D4] · §14 · **→ checkpoint CP4 (production RV + sign-off)**
> **Phụ thuộc:** **P6b DONE** (admin API §6.3 + hygiene — BLOCKER: bước 3 cần `PUT profile` + `publish`, bước 4 dùng `Get-AiConversations`/`Get-AiUsage`) · P6 (CP3 PASS 44/0/1skip — An Tâm `e2d91cdf` đã có `AIChat_Enabled` + 22 products ingest + ví 1000cr) · PO-6 (tenant — thực chất đã chọn An Tâm ở CP3) · PO-7 nếu phát sinh billing

## Objective

Đưa AI lên production thật cho 1 tenant F&B. Chỉ enable sau khi knowledge readiness đủ — widget trống knowledge là thất bại UX, không phải bug code.

## Bước 1 — Model verify (trước mọi thứ)

- Call trực tiếp Gemini API từ VPS `vanan-ai`: `generateContent` với `gemini-3.5-flash-lite` + `embedContent` với `text-embedding-004` → confirm model IDs tồn tại + latency + dims trả về khớp config `EmbeddingDimensions`.
- Fail → đổi `TenantAiLlm:Model` sang model đã production-verified trong `SmartEntryLlmOptions` (codebase đang dùng) → re-verify. Ghi quyết định vào card.
- **✅ DONE 2026-11-07 — kết quả:** chat `gemini-3.5-flash-lite` **200 / 550ms** ✓. Embedding `text-embedding-004` **404** (model không tồn tại với key/API version này — ListModels chỉ có `gemini-embedding-001/2`). **Quyết định:** `gemini-embedding-001` + `outputDimensionality=768` — verify **200 / 444ms / 768 dims** ✓ (giữ `vector(768)`, không migration). Code: `GeminiEmbeddingService` gửi `outputDimensionality=Dimensions` + default đổi `gemini-embedding-001` (appsettings + compose default). Deviation so với SRS [D3] (ghi `text-embedding-004`) — cần sync SRS [D3] note.

## Bước 2 — Chọn pilot tenant (PO-6)

- Survey tenant F&B production: menu đủ món (≥15), có mô tả, cấu trúc dữ liệu tốt. **Ưu tiên nhà hàng chay nếu tồn tại** (đủ menu+dinh dưỡng+brand story — đúng đánh giá khả thi gốc). Fallback: Vạn An Cafe.
- Confirm với user trước khi tiếp.
- **Note:** CP3 ops đã dùng **An Tâm `e2d91cdf`** (22 products ingest + ví 1000cr + `AIChat_Enabled`). Nếu user confirm An Tâm → bước này chỉ cần xác nhận, không survey lại.
- **✅ DONE 2026-11-07 — PO-6 = An Tâm `e2d91cdf`** (user chốt). **Deviation D4:** An Tâm là **SPA** (22 dịch vụ massage/facial — survey production ShopERP, không phải F&B/nhà hàng chay như tài liệu cũ ghi nhầm). Lý do giữ: đã wire sẵn toàn bộ (flags ON cả 2 · 22 products khớp ShopERP↔vanan-ai · ví 1000cr · e2e spec pass CP3). Hệ quả: NutritionQuery/calories không áp dụng cho spa → eval set thiên về menu/price/story/faq/booking; "món" = dịch vụ.

## Bước 3 — Knowledge readiness (dùng operator tooling P6b/P2)

- [x] **Menu trong ShopERP (source of truth):** ✅ 2026-11-07 — phát hiện menu ShopERP 22 items là **placeholder sai** (massage/giác hơi — web Shi thật 65 dịch vụ, 8 nhóm, giá khác). User chốt **web = truth** → soft-delete 22 cũ (`.bak-p7` backup) + insert **65 dịch vụ Shi thật** từ shibeautyspa.vn → `products:batch fullSync` → `upserted:65, removed:22`, knowledge/status products=65.
- [x] Brand story + FAQ + policy + menu-note: **5 docs ingested + approved** — facts từ web Shi (hotline 0888 85 85 58 · giờ T2–T6 9–19:30/T7–CN 8–19:30 · 4 cơ sở Bình Thạnh/Q6/Dĩ An/Thủ Dầu Một · soi da AI miễn phí · giá niêm yết · bảo hành nốt ruồi/triệt lông · chính sách mụn viêm). Seed script `seed-an-tam-ai-docs.ps1` cũ viết cho quán chay — **KHÔNG dùng** (sai ngành).
- [x] AI profile: persona **"Shi"** (xưng Shi/bạn, phong cách chuyên viên spa, không chẩn đoán y khoa) + business profile (4 cơ sở, hotline, giờ) → `PUT profile` draft v1 → preview-chat → `publish` **v1 published 2026-11-07** · widget-config trả `aiName:"Shi"` + greeting + suggestions đúng persona.
- [x] Eval: **cơ chế thực tế** = `p7_eval.py` trên VPS (22 cases spa-realistic → preview-chat → chấm). **Run 1: ~82%** — 4 intent misses (keyword rules F&B-flavored). **Fix:** IntentRouter patch `8b655fee` (+spa keywords Faq/MenuQuery — user approve patch, deployed). **Run 2: 19/22 strict ≈ 95% thực** — #3 Biophyto đáp đúng "1,200,000đ" (scoring miss format) · #14 massage Thái → grounded deflection tốt hơn refuse · #4 "Detox 4F giá" miss thật duy nhất (ghi P8 investigate). Hallucination: 0 · latency p95 0.61s.

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
