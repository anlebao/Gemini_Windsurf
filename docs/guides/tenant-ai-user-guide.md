# HƯỚNG DẪN SỬ DỤNG — TRỢ LÝ AI KHÁCH HÀNG (TENANT AI)

> ⚠️ **SUPERSEDED (2026-11-27):** tài liệu tổng hợp mới nhất (End-user → Technician, gồm Chat Learning Suggest W3f) tại **`docs/guides/van-an-ai-guide.md`**. File này giữ lại vì lịch sử.

> **Phiên bản:** 1.0 — 2026-11-14 (bao gồm AI-P11 "Đề xuất AI" từ chat)
> **Phạm vi:** SystemAdmin/DevOps · Shop Owner (ShopERP) · Buyer/Khách hàng (KhachLink)
> **Kiến trúc:** `vanan-ai` = service độc lập (VPS riêng, port 5060 nội bộ) — chat grounded RAG trên PostgreSQL + pgvector, LLM = Gemini, phí qua vanan-metering.

---

## MỤC LỤC

1. [Tổng quan nhanh](#1-tổng-quan-nhanh)
2. [Kiến trúc & luồng dữ liệu](#2-kiến-trúc--luồng-dữ-liệu)
3. [Dành cho SystemAdmin / DevOps](#3-dành-cho-systemadmin--devops)
4. [Dành cho Shop Owner](#4-dành-cho-shop-owner)
5. [Dành cho Buyer / Khách hàng](#5-dành-cho-buyer--khách-hàng)
6. [Vận hành nâng cao & troubleshooting](#6-vận-hành-nâng-cao--troubleshooting)
7. [Câu hỏi thường gặp (FAQ)](#7-câu-hỏi-thường-gặp-faq)

---

## 1. Tổng quan nhanh

- Trợ lý AI trả lời khách trên **widget chat storefront** (KhachLink), grounded trên "tri thức" của từng shop: sản phẩm, FAQ, chính sách, câu chuyện thương hiệu.
- AI **không tự học** từ hội thoại. Kiến thức chỉ vào qua 2 cửa: **đồng bộ sản phẩm tự động** và **document do người tạo/duyệt**.
- Mọi document mới ở trạng thái **`pending_review`** — phải được Owner/Admin **duyệt + chọn classification** thì mới được AI dùng.
- Owner có thể **đề xuất** cặp hỏi–đáp hay trong chat với khách thành document nháp (nút "Đề xuất AI" trong ShopInbox) — vẫn qua bước duyệt.
- Phân quyền tri thức theo `classification`: **Public** = AI trả lời khách trên storefront; **Internal/Confidential** = chỉ kênh nội bộ; **Restricted** = không bao giờ cho AI dùng.
- Khi AI không trả lời được (hết quota, service lỗi, câu hỏi ngoài phạm vi) → trả lời template lịch sự, **không crash**, không thu phí LLM.

| Đối tượng | Vào đâu | Làm gì |
|---|---|---|
| Buyer | KhachLink storefront → widget chat | Hỏi đáp với AI / xin gặp chủ quán |
| Shop Owner | ShopERP → `/community/messages` + `/admin/tenant-ai/*` | Duyệt tri thức, đề xuất từ chat, giám sát |
| SystemAdmin | ShopERP → `/admin/tenant-ai` + VPS `vanan-ai` | Bật flag, config, vận hành service |

---

## 2. Kiến trúc & luồng dữ liệu

### 2.1. Sơ đồ thành phần

```
Khách (KhachLink PWA)
   │  POST /v1/chat (qua Gateway, kèm tenant ctx ký HMAC)
   ▼
Gateway ──────────────► vanan-ai (VPS riêng, :5060, chỉ VPC)
   │                        │
   │                        ├─ PostgreSQL + pgvector (knowledge, conversations)
   │                        ├─ Gemini (LLM chat + embeddings)
   │                        └─ vanan-metering (quota gate + usage)
   │
ShopERP (Blazor Server) ──── TenantAiApiClient (signed ctx)
   ├─ /admin/tenant-ai/knowledge  → duyệt/từ chối document
   ├─ /community/messages         → ShopInbox, nút "Đề xuất AI"
   └─ TenantAiKnowledgeSyncService → sync products:batch định kỳ
```

### 2.2. Pipeline trả lời (9 bước, trong `ChatOrchestrator`)

```
message → kill-switch? → quota gate (metering) → IntentRouter
   → PolicyEngine (refuse trước LLM nếu OutOfScope/PolicyViolation)
   → Retriever (pgvector semantic + SQL) → UsagePermissionFilter [G15]
   → LLM (prompt = persona + facts + history) → AnswerValidator
   → persist conversation_messages → response
```

Nguyên tắc cứng (đã kiểm chứng bằng ThreatSuite):

- **[G6]** Intent bị refuse → dừng trước mọi LLM call.
- **[G14]** Document phải `approved` mới retrievable; `pending_review` invisible.
- **[G15]** Mọi context item qua permission filter `intent × classification × channel` — thiếu rule = fail closed (chỉ Public qua kênh public).
- **[G8]** `messageId` dedupe — replay trả lại câu trả lời đã lưu.
- Tenant isolation ở mọi tầng (ctx ký HMAC, query luôn kèm `tenant_id`).

### 2.3. Intent routing (`IntentRouter`)

1. **Keyword rules** (match chuỗi không dấu, thứ tự ưu tiên cố định): PolicyViolation → ExternalKnowledge → Nutrition → Availability → Recipe → BrandStory → OrderHelp → **Faq** → Price → Menu → ChitChat.
2. Không khớp rule → **LLM classify** (1 nhãn). LLM **không bao giờ** được trả `PolicyViolation` — nhãn đó chỉ đến từ rule/topic từ chối (chống injection).
3. Vẫn không chắc → `OutOfScope` → refuse nhẹ nhàng.

### 2.4. Classification & ai đọc được gì

| Classification | Public storefront | Kênh internal/preview |
|---|---|---|
| `public` | ✅ | ✅ |
| `internal` | ❌ (deny) | ✅ |
| `confidential` | ❌ | theo rule |
| `restricted` | ❌ tuyệt đối — không approve được qua UI/API thường |

> ⚠️ Doc tạo từ "Đề xuất AI" ingest với `internal` (an toàn mặc định). **Owner phải chủ động đổi sang `public` khi approve** nếu muốn AI public trả lời.

---

## 3. Dành cho SystemAdmin / DevOps

### 3.1. Hạ tầng

- Service `vanan-ai` chạy trên VPS riêng (`vanan-ai`, asia-southeast1-b), compose `/opt/vanan-ai/docker-compose.tenantai.yml`, container `vanan-ai-1` + `vanan-ai-pg-1` (PostgreSQL + pgvector).
- Port 5060 **internal-only** — không expose public; truy cập qua VPC/Gateway.
- Secrets trong `/opt/vanan-ai/.env.tenantai` (KHÔNG commit, KHÔNG in log): `TENANTAI_CTX_SIGNING_SECRET`, `TENANTAI_BOOTSTRAP_CALLER`, `TENANTAI_BOOTSTRAP_INGEST`, connection string DB, Gemini key.
- Health: `GET http://localhost:5060/v1/health` → `{"status":"ok","db":"up",...}`.

### 3.2. Bật TenantAI cho một tenant (feature flag)

Flag vận hành (không nằm trong DTO settings): `ShopFeature:{tenantId}:TenantAiEnabled` trong bảng `SystemSettings` của ShopERP SQLite (`/app/keys/vanan_shoperp.db`).

```sql
INSERT OR REPLACE INTO SystemSettings (key, value)
VALUES ('ShopFeature:<tenant-guid>:TenantAiEnabled', 'true');
```

- Flag OFF → nút "Đề xuất AI" ẩn, tab "AI trả lời khách" ẩn; human chat bình thường.
- Flag check log ở ShopERP; service TenantAI down → mọi gọi AI fail-soft (toast nhẹ, chat human không ảnh hưởng).

### 3.3. Cấu hình tenant (`tenant_ai_configs` + `ai_policies`)

- `tenant_ai_configs`: persona (`persona_json`), business profile, ai_name — có `version` + `status` (draft/published). Chat dùng config **published** mới nhất; admin API có `useDraft` để preview.
- `ai_policies` (theo `config_version`):
  - `refused_topics_json` — chủ đề cấm (vd `["18+, khiếm nhã","Mại dâm"]`) → `PolicyViolation`, refuse trước LLM.
  - `intent_scopes_json` — `{"Faq": false}` tắt intent theo tenant.
  - `external_allow_json` — whitelist external knowledge.
- `intent_routes` / `knowledge_usage_permissions`: tenant override system default; thiếu row = fail closed.

### 3.4. Deploy

- Push `main` → `cd-multivps.yml` tự build + deploy. Impact analysis chỉ deploy service có thay đổi (sửa `9_TenantAI/` → chỉ TenantAI VPS).
- Verify sau deploy: run log `Deploy to TenantAI VPS: success` + `Post-Deploy Smoke Test` + `/v1/health` từ trong VPS.

### 3.5. Metering

- `ai.chat.message` — tính phí cho câu trả lời LLM thật.
- `ai.chat.degraded` — MeterOnly (đo nhưng không charge) cho refused/degraded/zero-LLM.
- Quota gate trước pipeline: ví hết → `quota_denied` refuse nhẹ; check fail + cache hết hạn → degrade structured-only (0 LLM call).

### 3.6. Monitoring & debug nhanh

```sql
-- Trên vanan-ai-pg-1: các lượt chat gần nhất của tenant
SELECT role, intent, refused, refuse_reason, degraded, left(content,60), created_at_utc
FROM conversation_messages
WHERE tenant_id='<guid>' ORDER BY created_at_utc DESC LIMIT 20;

-- Document knowledge của tenant
SELECT type, classification, approval_status, left(title,50), source_ref
FROM knowledge_documents WHERE tenant_id='<guid>' ORDER BY created_at_utc DESC;

-- Lý do AI refuse: intent + refuse_reason nằm ngay trong conversation_messages
-- (admin UI "AI trả lời khách" trong ShopInbox cũng hiển thị badges này)
```

Log app: `docker logs vanan-ai-1` — mỗi chat log 1 dòng `chat tenant=… intent=… refused=… degraded=… dropped=… ms=…`.

---

## 4. Dành cho Shop Owner

### 4.1. Điều kiện tiên quyết

1. Tenant đã được SystemAdmin bật `TenantAiEnabled`.
2. Đã có **config published** (persona + business profile) — nếu chưa, vào `/admin/tenant-ai/config` soạn + publish.
3. Ví metering còn số dư (chat tính phí theo message).

### 4.2. Đưa tri thức vào hệ thống — 3 cách

**Cách A — Đồng bộ sản phẩm (tự động):** sản phẩm từ ShopERP sync định kỳ sang `product_knowledge`. Owner chỉ cần cập nhật giá/tồn/mo tả đúng trong ShopERP. Stale check: `Knowledge:StaleHours` (mặc định 24h) — dữ liệu cũ quá sẽ bị đánh dấu stale, AI không gợi ý sản phẩm stale.

**Cách B — Tạo document thủ công:** `/admin/tenant-ai/knowledge` → tạo mới:

| Field | Gợi ý |
|---|---|
| Type | `faq` (hỏi-đáp), `policy` (chính sách), `story` (câu chuyện), `menu-note`… |
| Title | Câu hỏi ngắn hoặc chủ đề |
| Content | Nguyên văn câu trả lời chuẩn bạn muốn AI nói |
| Classification (lúc ingest) | Để `internal` — đổi khi approve |

Sau ingest → status `pending_review` → vào mục 4.3.

**Cách C — "Đề xuất AI" từ chat (AI-P11):** khi đang trả lời khách trong ShopInbox (`/community/messages`):

1. Trên **bubble tin nhắn của chính bạn** (phía phải) xuất hiện nút **"Đề xuất AI"** — chỉ khi có câu hỏi của khách phía trước và flag đang bật.
2. Bấm → dialog preview **nguyên văn** cặp hỏi–đáp → sửa title nếu muốn → **Gửi đề xuất**.
3. System tạo document `faq` + `pending_review`, `sourceRef = chat:{conversation}:{message}` — bấm lại cùng tin không tạo trùng.
4. Vào `/admin/tenant-ai/knowledge` duyệt như cách B.

> Chỉ **Owner** thấy nút này. Staff/khách không có.

### 4.3. Duyệt document (approve) — bước bắt buộc

`/admin/tenant-ai/knowledge` → tab pending:

1. Đọc kỹ nội dung — nhớ doc sẽ được AI **trích dẫn nguyên văn** cho khách.
2. Bấm **Duyệt** → chọn **classification**:
   - `Public` — AI trả lời khách trên storefront ✅ (đa số FAQ/policy nên là cái này)
   - `Internal` — chỉ kênh nội bộ, AI public **không** dùng
   - `Confidential` — hạn chế hơn nữa
   - (`Restricted` không xuất hiện — cố tình không cho duyệt)
3. Approve xong → hệ thống tự chunk + embed → vài giây sau retrievable.
4. Có thể **Từ chối** (bắt buộc nhập lý do) hoặc **Xóa**.

> ⚠️ Hay gặp: approve mà quên đổi `internal` → doc "vô hình" với AI public. Nếu lỡ: doc đã approved không đổi classification được qua UI — báo SystemAdmin cập nhật hoặc tạo lại doc mới.

### 4.4. Giám sát AI đang trả lời gì

ShopInbox → tab **"AI trả lời khách"**: xem lịch sử câu hỏi khách + câu AI trả lời, kèm badge `intent`, `confidence`, `refused`, `degraded`. Dùng để:

- Phát hiện câu AI trả lời sai/thiếu → tạo/sửa document tương ứng.
- Phát hiện pattern câu hỏi lặp lại mà AI refuse → bổ sung keyword rule hoặc document.

### 4.5. Test nhanh trước khi báo khách dùng

Hỏi thử trên storefront widget các câu tiêu biểu: giá món, giờ mở cửa, chính sách đổi trả, câu chuyện quán. Câu nào refuse → kiểm tra: doc đã `approved`+`public` chưa? intent có route đúng không (xem mục 6)?

---

## 5. Dành cho Buyer / Khách hàng

### 5.1. Cách dùng (rất đơn giản)

1. Vào trang shop trên KhachLink → bấm icon **chat** (góc màn hình).
2. Gõ câu hỏi tiếng Việt tự nhiên, vd: "Phở chay bao nhiêu tiền?", "Quán mở cửa mấy giờ?", "Có giao hàng không?"
3. AI trả lời kèm thông tin quán (giá, giờ, chính sách…). Câu nào AI không chắc sẽ nói lịch sự và gợi ý hỏi chủ quán.
4. Muốn nói chuyện với người thật → bấm **"Hỏi chủ quán trực tiếp"** (xuất hiện sau câu refuse hoặc qua tab chuyển) → chuyển sang human chat, owner trả lời trong ShopInbox.

### 5.2. Lưu ý

- AI chỉ biết những gì shop đã duyệt vào tri thức — trả lời sai/thiếu thì chủ quán sẽ bổ sung.
- Không cung cấp được thông tin cá nhân/đơn hàng cụ thể — những câu đó nên hỏi chủ quán.
- Hội thoại AI và chat với chủ quán là **2 kênh riêng** — AI không đọc được chat với chủ quán và ngược lại (trừ khi owner chủ động "Đề xuất AI").

---

## 6. Vận hành nâng cao & troubleshooting

### 6.1. Bảng lỗi thường gặp

| Triệu chứng | Nguyên nhân thường gặp | Cách xử lý |
|---|---|---|
| Không thấy nút "Đề xuất AI" | Flag `TenantAiEnabled` OFF; không phải tin của owner; tin không có câu hỏi khách phía trước; browser cache circuit cũ | Bật flag (3.2); Ctrl+F5; kiểm tra đúng bubble phải |
| Bấm "Đề xuất AI" không thấy dialog | (Đã fix ở `a229dda1`) thiếu `Microsoft.AspNetCore.Components.Web` → `@onclick` literal; closure bug index | Deploy ≥ `a229dda1` |
| Doc pending mãi không retrievable | Chưa approve; approve nhưng classification ≠ public; chunk/embed lỗi | Kiểm tra `approval_status`+`classification` trong DB/UI |
| AI trả "không thể hỗ trợ yêu cầu này" | Intent → OutOfScope/PolicyViolation (refuse trước retrieval); hoặc route `RequiresFacts` mà 0 facts sau filter | Xem `intent`+`refuse_reason` trong `conversation_messages`; bổ sung keyword Faq hoặc document public |
| Câu refund/đổi trả bị refuse | (Đã fix ở `de126437`) LLM classify gán PolicyViolation | Deploy ≥ `de126437`; keyword mới route về Faq |
| AI trả lời nhưng sai nội dung | Document nội dung chưa chuẩn; nhiều doc mâu thuẫn; stale product data | Sửa/xóa doc; kiểm tra `SyncedAtUtc` product |
| AI không gợi ý sản phẩm | Product `Available=false`, stale quá `StaleHours`, hoặc intent không phải ProductKnowledge | Kiểm tra product_knowledge + intent route |
| Chat báo quota/lỗi | Ví metering = 0 → `quota_denied`; TenantAI down → degraded template | Nạp ví; check `/v1/health` + `docker logs vanan-ai-1` |

### 6.2. Thêm keyword intent mới (dev)

Sửa `Rules` trong `9_TenantAI/vanan-ai/src/VanAn.TenantAi.Ai/IntentRouter.cs` — thứ tự rule = ưu tiên. Keyword match trên text **đã bỏ dấu**. Nhớ: thêm test `InlineData` trong `ChatUnitTests.IntentMatrix_RulesRouteCorrectly`.

### 6.3. Intent routes & permission matrix tùy biến tenant

- `intent_routes`: override `AllowedSourceFlags`/`RequiresFacts`/`RefuseTemplate` per tenant (vd bật `ExternalKnowledge` cho tenant cho phép).
- `knowledge_usage_permissions`: rule `intent × classification × channel → Allow/Deny` — thiếu = fail closed.
- Cả hai qua admin API (xem `task_ai_p6b_admin_api_hygiene.md`) hoặc seed trực tiếp.

### 6.4. Retention & privacy

- `conversation_messages` retention **90 ngày** (`TenantAiChat:RetentionDays`) — tự xóa.
- Chat AI **không** chảy vào human messaging store và ngược lại.
- PII trong document do người duyệt chịu trách nhiệm — vì vậy ingest mặc định `internal`.

---

## 7. Câu hỏi thường gặp (FAQ)

**H: AI có tự học từ câu trả lời của chủ quán không?**
Không. Muốn AI "học" phải dùng nút "Đề xuất AI" → document → approve. Không có auto-ingest từ chat.

**H: Sao AI trả lời "không hỗ trợ được" dù đã có document?**
Thường do 1 trong 3: doc chưa approve / classification chưa `public` / câu hỏi bị route sang intent refuse (OutOfScope/PolicyViolation). Check `conversation_messages.intent` + `refuse_reason`.

**H: Phân biệt Internal vs Public thế nào?**
Public = khách storefront đọc được. Internal = chỉ nội bộ (vd ghi chú vận hành). Khi nghi ngờ → internal, nâng public sau.

**H: "Đề xuất AI" có gửi nguyên văn chat không?**
Có — content = "Khách hỏi: …\n\nChủ quán đáp: …" nguyên văn, không rewrite. Title có thể sửa trước khi gửi.

**H: Bấm "Đề xuất AI" 2 lần có tạo 2 doc không?**
Không — `sourceRef = chat:{conversationId}:{messageId}` dedupe theo tenant.

**H: AI trả lời có bị cắt khi hết tiền không?**
Không — quota refuse trả template lịch sự trước khi tốn LLM call; phí chỉ trừ sau khi có câu trả lời thật.

**H: Muốn AI trả lời về thời tiết/tin tức?**
`ExternalKnowledge` mặc định **tắt** (AllowedSourceFlags.None → refuse). Bật per-tenant qua `intent_routes` override nếu thật sự cần.
