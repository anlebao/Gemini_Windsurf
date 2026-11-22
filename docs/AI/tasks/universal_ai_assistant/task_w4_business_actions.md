# TASK W4: BUSINESS ACTIONS — HANDOFF + CAPTURE + BOOKING

> Wave: W4 · Mode: IMPLEMENT (sau W2+W3; có thể tách W4a/W4b)
> Master plan §W4 · SRS v2.0 §8 (capability ladder L3-L5)
> Depends on: W1–W3; `HANDOFF_TARGET_DECISION.md` (W0)

## Sản phẩm giao được — **moat của sản phẩm**

> AI không chỉ trả lời: khách hỏi vượt khả năng → AI chuyển sang nhân viên shop kèm context; khách muốn đặt lịch/giữ hàng → AI tạo yêu cầu qua flow Booking/KhachLink hiện có; lead được capture có consent. **Đây là khác biệt với mọi chatbot thường.**

## Scope IN

### L3 Capture (lead)
- Lead form trong chat khi cần (AI không biết / user yêu cầu / sau N câu hỏi): tên + SĐT/email — chỉ khi notice rõ + user action.
- Lead lưu theo tenant, staff xem được trong dashboard (scoped + audited).

### L4 Handoff (AI → người thật)
- Trigger: `Refused` lặp, escalation keyword, user request, hoặc confidence thấp kéo dài.
- Nâng cấp chip "Hỏi chủ quán trực tiếp" (AI-P8 hiện có) thành flow thật:
  - Handoff packet: session context đã consent (câu hỏi gần nhất, intent, contact nếu user cho).
  - Đích đến theo `HANDOFF_TARGET_DECISION.md` — candidate: KhachLink inbox/lead trước.
  - Notify staff qua kênh shop dùng; trạng thái handoff hiển thị cho khách ("đã chuyển cho shop, sẽ phản hồi sớm").
- Audit: mọi handoff ghi nhận actor/system/action.

### L5 Convert / Booking
- Assistant nhận diện intent đặt lịch/mua → đề xuất action → **đi qua approved flow hiện có** (Booking system, KhachLink cart/checkout) — AI không tự commit order/booking.
- Deep-link hoặc embedded flow tùy surface; confirm step rõ ràng.
- Mọi business action tôn trọng: không đổi giá/tồn kho, không xác nhận thanh toán (SRS nguyên tắc).

### Conversation history & analytics nâng cao
- Owner xem conversation đã consent-scoped; analytics: handoff rate, containment rate, top questions, conversion.

## Scope OUT

- L6 Execute tự động (order/booking không cần confirm) → W6
- Voice handoff → W6
- Đa kênh notify phức tạp (SMS/Zalo OA) → theo integration sẵn có, không xây mới

## Tasks

1. Lead capture model + storage + consent record.
2. Handoff service: trigger detection, packet build, notify dispatch, status tracking.
3. Nâng cấp widget/link UI: handoff states, lead form, "đã chuyển shop" UX.
4. Booking/KhachLink integration contract (theo Q5 + existing APIs — không tạo order path mới).
5. Staff notification channel (theo decision).
6. Dashboard: leads list, handoff log, analytics.
7. Tests: handoff trigger matrix, consent enforcement, booking flow E2E, regression chat thường, isolation.

## Acceptance / Gate

- [ ] Kịch bản thật end-to-end: khách hỏi → AI không biết → handoff → staff nhận đủ context → phản hồi khách
- [ ] Lead capture có consent; từ chối consent vẫn chat bình thường
- [ ] Booking request tạo được trong hệ thống hiện có qua approved flow
- [ ] AI không tự tạo order/commit giao dịch (AT-022)
- [ ] Analytics đủ đo containment/handoff/conversion
- [ ] Audit đầy đủ handoff + business actions

## Risks

- Handoff đích đến chưa rõ → W0 quyết; nếu KhachLink inbox chưa sẵn sàng, fallback email/Zalo notify tối thiểu.
- Booking integration phức tạp → bắt đầu bằng deep-link/lead trước, embedded flow sau.
- Context handoff lộ PII → consent-scoped packet, tối thiểu dữ liệu.
