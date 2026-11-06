# Task AI-P7 — Pilot Rollout: Model Verify + Knowledge Readiness + Go-live

> **Status:** ✅ APPROVED 2026-11-06
> **Gate:** P1-G5 — SRS v1.1 [D3][D4] · §14 · **→ checkpoint CP4 (production RV + sign-off)**
> **Phụ thuộc:** P6 (CP3 đã pass) · PO-6 (chọn tenant) · PO-7 nếu phát sinh billing

## Objective

Đưa AI lên production thật cho 1 tenant F&B. Chỉ enable sau khi knowledge readiness đủ — widget trống knowledge là thất bại UX, không phải bug code.

## Bước 1 — Model verify (trước mọi thứ)

- Call trực tiếp Gemini API từ VPS `vanan-ai`: `generateContent` với `gemini-3.5-flash-lite` + `embedContent` với `text-embedding-004` → confirm model IDs tồn tại + latency + dims trả về khớp config `EmbeddingDimensions`.
- Fail → đổi `TenantAiLlm:Model` sang model đã production-verified trong `SmartEntryLlmOptions` (codebase đang dùng) → re-verify. Ghi quyết định vào card.

## Bước 2 — Chọn pilot tenant (PO-6)

- Survey tenant F&B production: menu đủ món (≥15), có mô tả, cấu trúc dữ liệu tốt. **Ưu tiên nhà hàng chay nếu tồn tại** (đủ menu+dinh dưỡng+brand story — đúng đánh giá khả thi gốc). Fallback: Vạn An Cafe.
- Confirm với user trước khi tiếp.

## Bước 3 — Knowledge readiness (dùng operator tooling P2)

- [ ] Product sync: `fullSync` menu tenant → `GET /v1/knowledge/status` count khớp menu
- [ ] Brand story: soạn doc (lấy từ profile tenant thật / hỏi chủ quán) → ingest → **approve**
- [ ] FAQ: ≥5 câu thật của loại hình (giờ mở cửa, đặt bàn, giao hàng, dị ứng) → ingest → approve
- [ ] AI profile: tên/xưng hô/phong cách theo bản sắc tenant → `PUT profile` → preview-chat ≥5 câu → **publish**
- [ ] Eval set tenant này: ≥20 câu hỏi khách thật → chạy eval → Grounded ≥95% trước khi bật

## Bước 4 — Enable + RV production (CP4)

- [ ] `TenantAiEnabled` = ON cho đúng tenant pilot (PG + verify mirror)
- [ ] Widget config qua domain trả `aiEnabled=true` · widget render trên storefront thật
- [ ] Chat e2e ≥10 câu production: menu/calories/story/out-of-scope/competitor-deflect — log đẹp, grounded, refuse đúng
- [ ] Add to cart → checkout flow Option C hoạt động (order tạo bởi Gateway, đúng tenant)
- [ ] Metering: usage events xuất hiện trong ledger tenant đó · deny path verify (tạm set ví thấp → refuse → restore)
- [ ] Cross-tenant trên production: hỏi món tenant khác → không tìm thấy
- [ ] Latency thực đo: P95 ≤8s trên ≥20 requests
- [ ] Để chạy 48–72h quan sát: conversation review, tỷ lệ refuse, unsupported claims, chi phí token → báo cáo user

## Rollback plan

- `TenantAiEnabled` OFF → widget biến mất ngay (không deploy)
- `TenantAi:Enabled=false` env → toàn hệ tắt
- Rate card `ai.chat.message` enforcement tạm đổi MeterOnly nếu deny gây nhiễu

## Acceptance

- [ ] Pilot sign-off từ user sau ≥48h quan sát · metrics report đính kèm card · lessons-learned ghi vào card + cân nhắc cập nhật rules/knowledge-base
