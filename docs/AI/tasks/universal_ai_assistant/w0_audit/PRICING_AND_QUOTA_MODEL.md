# W0 DECISION: PRICING & QUOTA MODEL (Q3)

> Date: 2026-11-22 · Câu hỏi: ai trả tiền, rate card nào, hard-cap ra sao, attribution thế nào?

## Evidence

1. **Service codes đã tồn tại trong code** (`MeteringServiceCodes`):
   - `ai.chat.message` — **Deny-enforced** (LLM answer thật)
   - `ai.chat.degraded` — MeterOnly (refused/degraded/zero-LLM → đo nhưng không charge)
   - `ai.embedding.batch` — MeterOnly
2. **Rate card engine**: append-only versions, scope Global/TenantType/TenantStatus/Tenant, enforcement MeterOnly|Deny, creditRate per meterUnit.
3. **QuotaGate**: CheckBalanceAsync per tenant trước chat; cache 30s; fail → degradation policy (không unmetered LLM).
4. **Usage events**: có tenantId + serviceCode + qty + metadata + idempotencyKey; `GET /v1/usage/events` query được.
5. **Chưa verify**: rate card `ai.chat.message` đã seed trên prod DB chưa (cần check `vanan-metering` DB hoặc `/v1/rate-cards` list).
6. **Wallet semantics**: tenant có ví credit; usage trừ credit; check endpoint cho pre-flight.

## Mô hình đề xuất

### Ai trả

**Tenant (shop) trả** — mọi `ai.chat.message` của visitor trừ vào ví/quota của shop. Platform (Vạn An) không subsidize anonymous chat. Đây là quyết định SA-004 đã chốt ở SRS; audit xác nhận cơ chế đã sẵn sàng.

### Rate card cần có (verify/seed)

| ServiceCode | meterUnit | Enforcement | Ghi chú |
|---|---|---|---|
| `ai.chat.message` | message | Deny | ĐÃ CÓ trong code — verify seed + giá |
| `ai.chat.degraded` | message | MeterOnly | ĐÃ CÓ — degraded không charge |
| `ai.embedding.batch` | batch | MeterOnly | ingest-time |
| `ai.voice.session` / `ai.voice.minute` | session/minute | Deny (W6) | chưa cần W1 |
| `ai.widget.active` | — | — | chỉ nếu commercial model cần; defer |

### Hard-cap per assistant (mới — W1)

- `TenantAssistant.MonthlyBudgetCredits` (nullable = unlimited theo ví tenant) + `MonthlyBudgetMessages` optional.
- Trước mỗi chat: check ví (QuotaGate hiện có) **và** assistant budget counter (đếm từ usage events hoặc counter table).
- Vượt cap → trả `QUOTA_EXCEEDED` kiểu degrade: structured-only answer hoặc thông báo lịch sự. KHÔNG gọi LLM.
- Dashboard hiển thị: used/remaining + projection.

### Cost attribution (SEC-025)

Usage event metadata phải mang đủ:

```json
{
  "assistantRef": "{assistantId}",
  "channel": "ai_link|website_widget|khachlink_storefront|extension|android",
  "sessionRef": "{hash của sessionId — không raw}",
  "surface": "..."
}
```

- Kiểm tra `MeteringClient`/`MeteringIngestQueue` hiện có forward metadata chưa — nếu chưa, MODIFY tối thiểu (thêm field vào event, không đổi contract breaking).
- `ai.chat.degraded` cũng ghi attribution để đo abuse/containment.

### Abuse economics (minh họa — validate bằng pilot)

- Giả định: 1 message ≈ vài trăm→vài nghìn tokens tùy context; giá Gemini phụ thuộc model TenantAI đang dùng.
- Visitor spam: session cap 20/h + rate limit IP + assistant budget → bounded cost per session.
- Worst case 1 tenant bị spam: assistant budget cap giới hạn tổng thiệt hại = budget tháng của shop đó; tenant khác không ảnh hưởng (metering per-tenant sẵn).

## Cần PO quyết

- [ ] Giá `ai.chat.message` (credit/message) — lấy từ cost Gemini thật + margin
- [ ] Default monthly budget per assistant cho pilot (đề xuất: đủ ~500-1000 messages/tháng đầu)
- [ ] Vượt budget behavior: degrade structured-only vs hard stop "hết lượt" (đề xuất: degrade trước, hard stop khi structured cũng không khả dụng)
- [ ] Assistant add-on pricing (nếu tách khỏi gói tenant hiện có)
