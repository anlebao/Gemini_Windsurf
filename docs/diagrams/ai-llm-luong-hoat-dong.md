# AI/LLM — Smart Entry (kế toán) + Chat widget (storefront)

> Hai luồng LLM độc lập. Verified @476cd73f (2026-11-08).

## 1. Tổng quan — 2 đường LLM, ranh giới khác nhau

```mermaid
flowchart LR
    classDef ai fill:#c6f6d5,stroke:#2f855a,stroke-dasharray:5 5
    classDef human fill:#cfe3ff,stroke:#2b6cb0
    classDef ext fill:#e2e8f0,stroke:#718096
    SE["Smart Entry<br/>(trang phiếu kế toán)"]:::human --> P["Parser deterministic<br/>+ LLM enhance<br/>FAIL-CLOSED"]:::ai
    P --> G["Gemini API<br/>Flash (config)"]:::ext
    CW["AI Chat widget<br/>storefront /store/{slug}"]:::human --> GW2["Gateway<br/>signed X-Tenant-Ctx (HMAC)"]:::ai
    GW2 --> AI2["vanan-ai :5060<br/>+ pgvector"]:::ai
    P -.->|ghi bút toán| JE["JournalEntry<br/>(write-path)"]
    AI2 -.->|chỉ đọc approved<br/>knowledge — tư vấn,<br/>KHÔNG ghi giao dịch| KB["advice only"]
```

## 2. Smart Entry — text tự nhiên -> bút toán (fail-closed)

```mermaid
flowchart TB
    classDef human fill:#cfe3ff,stroke:#2b6cb0
    classDef auto fill:#c6f6d5,stroke:#2f855a
    classDef ai fill:#c6f6d5,stroke:#2f855a,stroke-dasharray:5 5
    classDef error fill:#fed7d7,stroke:#c53030

    IN["Kế toán nhập câu:<br/>'chi 500k tiền điện tháng 10'"]:::human
    IN --> DP["Deterministic parse<br/>(keyword/rule -> intent)"]:::auto
    DP -->|đủ field| RES
    DP -->|thiếu/mơ hồ| LLM["LlmEnhanceVoucherTextParseService<br/>Gemini Flash — enhance fields"]:::ai
    LLM -->|LLM lỗi/timeout| FP["fallback: parse deterministic<br/>HOẶC hỏi lại user (slot matrix)"]:::error
    LLM --> RES["VoucherTypeRegistry<br/>ResolveByIntent"]:::auto
    RES -->|unresolved| MJ["manual journal path"]:::error
    RES --> CF{"Preview + user xác nhận"}:::human
    CF -->|sửa| IN
    CF -->|confirm| STR["Strategy.PostAsync<br/>-> VoucherNumberService seq"]:::auto
    STR --> JE[("JournalEntry (append-only)<br/>ref voucher type")]
```

**Nguyên tắc:** LLM chỉ *enhance* parse — không được tự ghi; deterministic fallback luôn sẵn sàng (fail-closed); user luôn preview trước khi post.

## 3. Storefront AI Chat — widget -> signed ctx -> vanan-ai

```mermaid
sequenceDiagram
    participant V as Visitor (storefront)
    participant W as AiChatWidget (KhachLink)
    participant G as Gateway
    participant T as TenantAiContextSigner
    participant A as vanan-ai :5060 + pgvector
    participant M as metering (degrade policy)
    V->>W: hỏi (gõ / voice-input.js<br/>auto-submit ~2.5s im lặng)
    W->>G: GET /api/public/tenant-ai/search<br/>(synthetic session)
    G->>T: ký X-Tenant-Ctx (HMAC ±5ph, callerId)
    G->>A: POST /v1/chat + signed ctx + X-Ai-Key
    A->>A: auth 3 lớp (key -> scope -> signed ctx)<br/>+ tenant allowlist
    A->>M: metering check (AI = stricter degrade)
    A->>A: retrieve approved-only knowledge<br/>(embed pgvector) + staleness strip
    A-->>G: answer (advisory, NO transaction)
    G-->>W: render newest-first + suggestions
    Note over A: pending_review knowledge KHÔNG<br/>được retrieve công khai
```

## 4. Ranh giới AI — cái AI ĐƯỢC và KHÔNG ĐƯỢC làm

```mermaid
flowchart TB
    classDef ok fill:#c6f6d5,stroke:#2f855a
    classDef no fill:#fed7d7,stroke:#c53030
    classDef gate fill:#f6e3c5,stroke:#b7791f
    OK["ĐƯỢC:<br/>· enhance parse Smart Entry (có preview)<br/>· trả lời FAQ/gợi ý sản phẩm storefront<br/>· semantic search /store/{slug}<br/>· advisory Fin-Intelligence (read-only)"]:::ok
    GATE["GATES:<br/>· 3-lớp auth (key+scope+signed ctx)<br/>· approved-only knowledge<br/>· staleness strip giá/tồn kho<br/>· metering AI degrade<br/>· tenant allowlist + ctx HMAC"]:::gate
    NO["KHÔNG:<br/>· tự ghi bút toán/giao dịch<br/>· retrieve cross-tenant knowledge<br/>· pending_review knowledge công khai<br/>· đọc PII trực tiếp"]:::no
```

## Ghi chú giao tiếp chính

- **Smart Entry:** LLM = bước *tăng cường* giữa deterministic parse và `ResolveByIntent` — không thay thế registry/strategy; LLM fail → fallback deterministic (fail-closed), không fail-open.
- **Chat widget:** search endpoint nhân tạo session → cùng `/v1/chat`; ctx HMAC ±5ph + callerId match + tenant allowlist (3 lớp); widget anonymous nhưng request do Gateway ký — tenant nào ctx đó, không leo quyền.
- **Knowledge boundary:** chỉ `approved` knowledge được retrieve; `pending_review` cần approve trước; staleness stripping gỡ claim volatile (giá/tồn) trước khi trả lời.
- **Metering:** AI có degrade policy chặt hơn caller thường (quota → degrade trước deny).
- **Voice input:** `window.vananVoiceInput` (vi-VN, interim+silence auto-final, `Voice_Note_Enabled` gate) — transcript vào cùng text path.
