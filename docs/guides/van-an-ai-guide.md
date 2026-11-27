# VAN AN AI — HƯỚNG DẪN SỬ DỤNG & VẬN HÀNH (End-user → Technician)

> **Phiên bản:** 1.0 — 2026-11-27
> **Phạm vi:** Khách hàng (end-user) · Chủ shop (Shop Owner) · SystemAdmin/DevOps · Technician (kỹ thuật vận hành & mở rộng)
> **Thay thế:** `docs/guides/tenant-ai-user-guide.md` (v1.0, 2026-11-14) — tài liệu này hợp nhất + bổ sung **Chat Learning Suggest (W3f, 2026-11-27)** và các tính năng AI liên quan (AI Link, semantic search, auto-builder, OCR menu).
> **Kiến trúc:** `vanan-ai` = service độc lập (VPS riêng, port 5060 nội bộ) — chat grounded RAG trên PostgreSQL + pgvector, LLM + embeddings = Gemini, phí qua vanan-metering. SRS: `docs/requirements/SRS_VanAn_AI_Assistant_Platform_v2.0.md` · Master plan: `docs/AI/plans/universal-ai-assistant-master-plan.md`.

---

## MỤC LỤC

1. [Tổng quan & đối tượng](#1-tổng-quan--đối-tượng)
2. [Mức End-user — Khách hàng](#2-mức-end-user--khách-hàng)
3. [Mức Shop Owner — Chủ shop](#3-mức-shop-owner--chủ-shop)
4. [Mức SystemAdmin / DevOps](#4-mức-systemadmin--devops)
5. [Mức Technician — Kiến trúc & vận hành kỹ thuật](#5-mức-technician--kiến-trúc--vận-hành-kỹ-thuật)
6. [Câu hỏi thường gặp (FAQ)](#6-câu-hỏi-thường-gặp-faq)

---

## 1. Tổng quan & đối tượng

**Van An AI** là trợ lý ảo riêng cho TỪNG cửa hàng, trả lời khách dựa trên **tri thức của chính cửa hàng đó** (sản phẩm, FAQ, chính sách, câu chuyện thương hiệu) — không phải chatbot hỏi đáp chung:

- **Grounded RAG:** mọi câu trả lời phải dựa trên dữ liệu đã được xác thực (product sync + document đã duyệt) — chống bịa (hallucination).
- **Cô lập tenant tuyệt đối:** AI của cửa hàng A không bao giờ nhìn thấy tri thức của cửa hàng B.
- **Human-in-the-loop:** mọi tri thức mới phải qua **bước duyệt của chủ shop** — kể cả khi AI tự đề xuất (tính năng mới W3f).
- **Không đổ vỡ:** AI lỗi/không trả lời được → trả lời template lịch sự, không crash, không thu phí LLM.

**Bề mặt (surface) AI có mặt ở đâu:**

| Surface | Đối tượng dùng | Đường vào |
|---|---|---|
| Widget chat storefront | Khách hàng | KhachLink storefront → ô chat "Hỏi AI" |
| **AI Link** (`/a/{slug}` + QR) | Khách hàng | Link/QR in tại quầy, website, tờ rơi |
| **Semantic search storefront** | Khách hàng | Ô tìm kiếm trên trang cửa hàng (hỏi bằng câu tự nhiên) |
| Trợ lý AI admin | Chủ shop | ShopERP → `/admin/tenant-ai/*` |
| AI Link quản trị | Chủ shop | ShopERP → `/settings/ai-assistant` |

**Mới (2026-11-27) — Chat Learning Suggest:** AI giờ có thể **tự đề xuất tri thức** từ những câu khách hỏi mà nó chưa trả lời được — nhưng luôn ở dạng **bản nháp chờ duyệt**, chủ shop quyết định cuối cùng. Chi tiết: [§3.3.4](#334-mới-chat-learning-suggest--ai-tự-đề-xuất-faq).

| Vai trò | Đọc phần |
|---|---|
| Khách hàng | §2 |
| Chủ shop | §3 |
| SystemAdmin/DevOps | §4 |
| Technician/Developer | §5 |

---

## 2. Mức End-user — Khách hàng

### 2.1. Dùng widget chat trên trang cửa hàng

1. Mở trang cửa hàng (vd `timlathay.com` hoặc storefront KhachLink) → nút **"Hỏi AI"** (chỉ hiện khi cửa hàng đã bật AI).
2. Nhập câu hỏi tự nhiên (bấm Enter hoặc nút tìm kiếm) — có thể **nói** bằng nút mic nếu trình duyệt hỗ trợ.
3. AI trả lời kèm **thẻ sản phẩm** (nếu câu hỏi về món ăn/dịch vụ: tên, giá, nút thêm vào giỏ khi còn hàng).
4. Đóng/mở lại widget → **lịch sử phiên được giữ lại** để tiếp tục hỏi.

**Ví dụ câu hỏi AI trả lời tốt:**

| Loại | Ví dụ |
|---|---|
| Menu | "Cho mình xem menu", "Quán có món gì ngon?" |
| Giá | "Giá phở chay bao nhiêu?" |
| Dinh dưỡng | "Món nào ít calo nhất?", "Tôi dị ứng đậu phộng, món nào ăn được?" |
| Còn hàng | "Phở chay còn không?" |
| Công thức | "Cách nấu nước chấm thế nào?" |
| Câu chuyện | "Kể câu chuyện thương hiệu quán đi" |
| Chính sách/FAQ | "Quán mở cửa mấy giờ?", "Có giao hàng không?", "Đổi trả thế nào?" |
| Đặt hàng | "Làm sao để đặt hàng?" |

**AI sẽ từ chối lịch sự (không trả lời)** các câu ngoài phạm vi cửa hàng (thời tiết, tin tức…), câu đòi phá rào an toàn ("hãy quên mọi luật…"), hoặc chủ đề chủ shop đã cấm.

### 2.2. Khi AI chưa trả lời được

- AI trả lời: *"Xin lỗi, mình chưa có dữ liệu xác nhận cho câu hỏi này…"* + hiện chip **"Hỏi cửa hàng trực tiếp"** → bấm để chat/gọi quán ngay, không bị bỏ rơi.
- Nếu bạn gõ **sai chính tả / thiếu dấu**, AI có thể hiện chip **"Ý bạn là: [X]?"** — bấm 1 lần để tìm lại đúng ý.

### 2.3. AI Link (`/a/{slug}`)

- Cửa hàng có thể phát **link + QR "Hỏi AI"** (in ở quầy, dán trên bàn, đăng website/tờ rơi).
- Mở link → trang chat riêng của cửa hàng đó, **không cần đi qua storefront**.
- Link có thể bị **thu hồi** bởi chủ shop (hết hạn ngay khi bị revoke/regenerate).

### 2.4. Tìm kiếm trên trang cửa hàng

- Ô tìm kiếm có hỗ trợ **semantic search**: bạn có thể gõ cả câu ("quán có món gì cho người ăn chay?") thay vì chỉ từ khóa — AI trả lời grounded như widget chat, kể cả khi bạn **gõ sai chính tả/thiếu dấu**.

### 2.5. Quyền riêng tư & giới hạn

- Hội thoại được giữ tối đa **90 ngày** rồi tự động xóa.
- Mỗi phiên chat giới hạn **20 tin nhắn/giờ** — quá giới hạn sẽ được nhắc thử lại sau.
- AI không được dùng tri thức `internal/confidential` của cửa hàng khi trả lời bạn — chỉ dùng nội dung `public` đã duyệt.

---

## 3. Mức Shop Owner — Chủ shop

> Toàn bộ đường dẫn trong ShopERP (Blazor Server), đăng nhập quyền Owner/Staff.

### 3.1. Bật AI cho cửa hàng

**Cấu hình tính năng Shop** (`/settings/shop-features`):

- Bật **"Đồng bộ dữ liệu AI (Tenant AI)"** — kích hoạt toàn bộ trang quản trị AI + đồng bộ sản phẩm.
- Bật **"AI Chatbox"** — hiện widget chat cho khách trên storefront.
- (Tuỳ chọn) **"Gọi thoại/video"** — nút gọi cửa hàng trên storefront (module voice riêng, xem `10_Voice/`).

Khi AI tắt → widget ẩn trên storefront, khách không thấy lỗi.

### 3.2. Hub trợ lý AI (`/admin/tenant-ai`)

Trang tổng: trạng thái service, **độ phủ tri thức** (N sản phẩm đang bán + M tài liệu đã duyệt), cảnh báo khi **corpus mỏng** (AI "dốt" vì thiếu dữ liệu — không phải lỗi kỹ thuật).

**Checklist ra mắt AI cho cửa hàng:**
1. Sản phẩm > 0 (đã sync từ ShopERP).
2. Tài liệu đã duyệt ≥ 3 (FAQ/chính sách/câu chuyện).
3. Hồ sơ AI đã publish (tên + persona).
4. Thử chat trên storefront (mở app, hỏi 1-2 câu thật).

### 3.3. Tri thức AI (`/admin/tenant-ai/knowledge`)

#### 3.3.1. Sản phẩm — tự động, không cần làm gì

- Sản phẩm/dịch vụ (tên, giá, calo, nguyên liệu, dị ứng, còn hàng, nhóm) được **đồng bộ tự động từ ShopERP**.
- Cập nhật ở ERP → AI thấy được (trong vòng vài phút; nếu quá **24 giờ** chưa sync, AI nói "thông tin có thể cũ" thay vì khẳng định sai).

#### 3.3.2. Document — tạo & duyệt (cổng bắt buộc [G14])

- **5 loại:** `faq` · `story` · `policy` · `menu-note` · `recipe`.
- **4 mức phân loại (classification):**

| Classification | Khách trên storefront thấy? |
|---|---|
| `public` | ✅ AI trả lời khách |
| `internal` | ❌ chỉ kênh nội bộ |
| `confidential` | ❌ theo rule |
| `restricted` | ❌ tuyệt đối — không đưa vào AI |

- Mọi document mới vào tab **"Chờ duyệt"** → owner **Duyệt** (chọn classification) hoặc **Từ chối** (kèm lý do). Chỉ document `approved + public` mới được AI dùng.
- ⚠️ Document sinh tự động (đề xuất AI, học từ chat) vào với classification **`internal`** (an toàn mặc định) — **phải chủ động đổi sang `public` khi duyệt** nếu muốn khách được trả lời.

#### 3.3.3. Nạp tri thức nhanh — Auto-Builder & OCR

- **Auto-Builder:** dán text thô (menu, bảng giá, FAQ, chính sách, giờ mở, khuyến mãi) → AI trích xuất thành **4 nhóm** (Menu & dịch vụ / Giới thiệu quán / Chính sách / Khuyến mãi) → xem trước → chọn nhập vào → vẫn qua **Chờ duyệt**.
- **OCR menu:** upload ảnh menu → nhận diện chữ → trích sản phẩm/dịch vụ (chế độ owner-upload).
- **Từ hội thoại (ShopInbox):** trong tin nhắn với khách có nút **"Đề xuất AI"** → cặp hỏi–đáp hay thành document nháp → duyệt.

#### 3.3.4. 🆕 MỚI — Chat Learning Suggest: AI tự đề xuất FAQ

AI giờ **tự học có kiểm soát**: hằng ngày, AI gom những câu khách hỏi mà nó **chưa trả lời được** (trả lời "chưa có dữ liệu xác nhận"). Câu nào được hỏi **lặp lại ≥ 3 lần** (kể cả viết khác nhau/chính tả sai) → AI soạn **bản nháp FAQ** (câu hỏi chuẩn + câu trả lời dự thảo, có cảnh báo chống bịa thông tin) → đưa vào **tab "Chờ duyệt"** của bạn.

> ⚠️ **Mặc định TẮT (opt-in):** mỗi cửa hàng phải tự bật tính năng này. Vào `/admin/tenant-ai/policy` → card **"Tự học có kiểm soát"** → bật toggle **"Cho phép AI đề xuất FAQ"** → Lưu. Chỉ khi cửa hàng bật (và SystemAdmin đã bật cờ toàn cục, xem §4.5) thì AI mới đề xuất bản nháp.

**Cách nhận biết bản nháp từ AI:**
- Badge **"🤖 AI đề xuất"** màu xanh ngay cạnh tiêu đề trong tab Chờ duyệt.
- **Tiêu đề = câu khách hỏi** (vd "Quán mở cửa mấy giờ?"), loại `faq`.
- Nguồn gốc `chat-learn` trong `sourceRef` (API/DB).

**Việc của bạn:** vào `/admin/tenant-ai/knowledge` → tab **Chờ duyệt** →
- **Sửa nội dung nếu chưa đúng** → **Duyệt** với classification **`public`** (để khách được trả lời) — hoặc
- **Từ chối** kèm lý do (AI sẽ không đề xuất lại câu đó).

**Giới hạn tự bảo vệ (không cần bạn can thiệp):** tối đa **5 đề xuất/cửa hàng/ngày**, tối đa **10 bản nháp chờ duyệt** cùng lúc; câu thuộc chủ đề cấm, hội thoại thử nghiệm (preview), câu chào hỏi/ngoài phạm vi → không bao giờ đề xuất.

> 💡 **Mẹo:** thay vì tự gõ từng FAQ, hãy để khách hỏi — câu hỏi lặp lại sẽ tự thành bản nháp. Bạn chỉ duyệt/sửa. Tắt tính năng bất cứ lúc nào tại cùng card **"Tự học có kiểm soát"** trên trang Chính sách; tắt toàn hệ thống do SystemAdmin quyết (xem §4.5).

### 3.4. Cá tính AI (`/admin/tenant-ai/profile`)

- **Tên AI** (vd "Trợ lý quán"), **persona** (giọng văn, greeting, gợi ý câu hỏi), **business profile** (mô tả cửa hàng).
- Bản nháp (draft) → **Publish** để áp dụng; có **version history + rollback** nếu cần quay lại.
- Greeting/suggestions trong persona JSON: `{"greeting": "...", "suggestions": ["..."]}` → widget hiển thị.

### 3.5. Chính sách (`/admin/tenant-ai/policy`)

- **Chủ đề cấm** (refused topics): khách hỏi vào chủ đề này → AI từ chối lịch sự.
- **Intent scopes**: tắt từng loại câu hỏi nếu không muốn AI trả lời (vd tắt "công thức").
- **Card "Tự học có kiểm soát"** (🆕): toggle opt-in bật/tắt **Chat Learning Suggest** cho riêng cửa hàng — mặc định TẮT. Chi tiết §3.3.4.

### 3.6. Hội thoại (`/admin/tenant-ai/conversations`)

- Xem lịch sử chat của khách với AI (đọc-only) để nắm khách quan tâm gì, AI trả lời ra sao.
- Đây là nơi phát hiện "khách hỏi nhiều mà AI chưa biết" — nguồn cảm hứng bổ sung tri thức (hoặc để Learning Suggest tự làm).

### 3.7. Sử dụng & phí (`/admin/tenant-ai/usage`)

- Theo dõi số tin AI đã trả lời (số liệu thống kê, phí nền tảng tính theo rate card — xem §4.4).
- Hết hạn mức/quota → AI tự ngưng lịch sự (không lỗi, không nợ).

### 3.8. AI Link (`/settings/ai-assistant`)

- **Tạo link** `/a/{slug}` + **QR** in quầy/tờ rơi; **nhúng** (embed) vào website; **thu hồi/tạo lại** khi cần.
- Link dùng chung persona + tri thức của Tenant AI — chỉ là **kênh phân phối**.

---

## 4. Mức SystemAdmin / DevOps

### 4.1. Bật/tắt toàn cục & theo tenant

- **Per-tenant:** `AIChat_Enabled` + `TenantAiEnabled` trong `/settings/shop-features` (per shop).
- **Toàn cục:** `TenantAi:Enabled=false` trong env của vanan-ai → AI off mọi nơi (widget ẩn, chat trả lời "tạm ngưng").
- **Chat Learning Suggest — 3 lớp AND** (tất cả phải ON): ① env `Learning:Enabled` (default `true`, kill-switch deploy-time) ② cờ toàn cục `system_settings["LearningSuggest:Enabled"]` (ops set bằng SQL, absent = ON) ③ opt-in per-tenant `system_settings["LearningSuggest:{tenantId}:Enabled"]` (absent = **OFF** — chủ shop/sysadmin bật qua card trên `/admin/tenant-ai/policy`).

### 4.2. Widget & giới hạn

| Cấu hình | Key | Default |
|---|---|---|
| Giới hạn tin/phiên/giờ | `TenantAi:SessionCap` (Gateway) | 20 |
| Rate limit public | `tenant-ai-public` policy | theo nginx/ASP.NET |
| Window lịch sử phiên | `TenantAiChat:HistoryWindow` | 10 tin |
| Retention hội thoại | `TenantAiChat:RetentionDays` | 90 ngày |

### 4.3. Vận hành service

- Chạy trên VPS **`vanan-ai`** (asia-southeast1-b), compose `/opt/vanan-ai/docker-compose.tenantai.yml`, container `vanan-ai-1` + `vanan-ai-pg-1` (PostgreSQL + pgvector).
- Port **5060 internal-only** — không expose public (chỉ VPC/Gateway).
- Health: `GET /v1/health` → `{"status":"ok","db":"up"}`.
- Log: `sudo docker logs vanan-ai-1` — tìm `learning suggest pass done tenants=…` (job hằng ngày), `chat tenant=…` (mỗi tin).
- Hội thoại cũ hơn 90 ngày tự xóa hàng ngày (`RetentionCleanupService`).

### 4.4. Phí nền tảng (metering)

- Câu trả lời LLM thật → **`ai.chat.message`** (tính phí, deny-enforced).
- Câu bị từ chối/lỗi/không dùng LLM → **`ai.chat.degraded`** (đếm được, **không thu phí**).
- Ví/quota cạn → AI từ chối lịch sự (`quota_denied`), widget có thể ẩn — khách không thấy lỗi.
- Chi tiết rate card, wallet, top-up: `.devin/rules/metering.md`.

### 4.5. Cấu hình chat learning suggest (mới)

| Key (env vanan-ai) | Ý nghĩa | Default |
|---|---|---|
| `Learning:Enabled` | Bật/tắt job | `true` |
| `Learning:MinClusterSize` | Số lần hỏi tối thiểu để đề xuất | 3 |
| `Learning:SimilarityThreshold` | Độ tương đồng (Dice-bigram) để gom câu trùng | 0.85 |
| `Learning:MaxSuggestionsPerTenant` | Tối đa đề xuất/cửa hàng/lượt | 5 |
| `Learning:MaxPendingPerTenant` | Tối đa bản nháp chờ duyệt/cửa hàng | 10 |
| `Learning:LookbackDays` | Cửa sổ hội thoại xét | 7 ngày |
| `Learning:StartupDelayMinutes` | Trì hoãn chạy lần đầu sau khi start | 60 phút |
| `Learning:CleanupIntervalHours` | Chu kỳ chạy | 24 giờ |

**Cờ runtime trong `system_settings` (PG của vanan-ai) — 2 lớp AND sau env:**

| Key | Lớp | Ý nghĩa | Default khi absent |
|---|---|---|---|
| `LearningSuggest:Enabled` | Toàn cục | Master switch runtime — ops tắt nhanh không cần redeploy | ON (`!= "false"` = ON) |
| `LearningSuggest:{tenantId}:Enabled` | Per-tenant | Opt-in của từng cửa hàng | **OFF** (phải set `"true"`) |

```bash
# Bật/tắt toàn cục (không cần redeploy):
sudo docker exec -e PGPASSWORD=<pg-pw> vanan-ai-pg-1 psql -U vanan_ai -d vanan_ai -c \
  "INSERT INTO system_settings (key, value, updated_at_utc) VALUES ('LearningSuggest:Enabled','false', now())
   ON CONFLICT (key) DO UPDATE SET value='false', updated_at_utc=now();"

# Bật opt-in cho 1 tenant (thường chủ shop tự bật qua UI /admin/tenant-ai/policy):
... VALUES ('LearningSuggest:<tenant-id>:Enabled','true', now()) ...
```

API nội bộ (scope `admin`, tenant ctx): `GET/PUT /v1/admin/tenants/{id}/learning` — body `{ "enabled": bool }`. UI: card **"Tự học có kiểm soát"** trên `/admin/tenant-ai/policy` (ShopERP → `TenantAiApiClient.Get/PutLearningSettingAsync`).

### 4.6. Troubleshooting nhanh

| Triệu chứng | Nguyên nhân thường gặp | Xử lý |
|---|---|---|
| Widget không hiện trên storefront | `AIChat_Enabled` tắt / service down / tenant không active | Kiểm tra `/settings/shop-features`; `/v1/health`; gateway logs |
| AI "dốt", toàn "chưa có dữ liệu" | Corpus mỏng (ít sản phẩm/docs duyệt) | Nạp tri thức §3.3; bật Learning Suggest (opt-in §3.5) |
| AI trả lời "tạm ngưng phục vụ" | Quota hết / `TenantAi:Enabled=false` | Kiểm tra ví metering; bật lại kill-switch |
| `quota_denied` trong log | Ví tenant = 0 | Top-up wallet (metering) |
| Ai nói "thông tin có thể cũ" | Sync sản phẩm > 24h | Kiểm tra TenantAiKnowledgeSyncService |
| Pending docs tăng bất thường | Learning Suggest đang đề xuất | Duyệt/từ chối; giảm `MaxSuggestionsPerTenant` |

---

## 5. Mức Technician — Kiến trúc & vận hành kỹ thuật

### 5.1. Kiến trúc & luồng dữ liệu

```
Khách (KhachLink PWA / AI Link / search)
   │  POST /api/public/tenant-ai/chat   (qua Gateway — tenant resolve server-side, T6)
   ▼
Gateway (5001) ───────────► vanan-ai (VPS riêng, :5060, internal-only)
   │                            │
   │                            ├─ PostgreSQL + pgvector  (knowledge_documents/chunks,
   │                            │   product_knowledge, conversation_*, intent_routes,
   │                            │   ai_policies, usage_permissions, configs, ingest_log)
   │                            ├─ Gemini  (ILlmChatClient + IEmbeddingProvider)
   │                            └─ vanan-metering  (QuotaGate + MeteringIngestQueue)
   │
ShopERP (5003) ──────────── TenantAiApiClient (HMAC signed ctx)
   ├─ /admin/tenant-ai/*          → profile/policy/knowledge/conversations/usage
   ├─ /settings/ai-assistant      → AssistantLink CRUD + QR
   └─ TenantAiKnowledgeSyncService → /v1/knowledge/products:batch định kỳ
```

- Tenant resolve **server-side** (Gateway): host-bound `KhachLinkInstance` thắng, client tenantId luôn được validate — không ký ctx mù.
- Ctx ký HMAC-SHA256 (`TenantContextSigner`), ±5 phút, kèm callerId.
- Chat attribution channels (allowlist): `ai_link` · `khachlink_storefront` · `website_widget` · `extension` · `android` · `public` (default) · `preview` (admin, bị loại khỏi learning).

### 5.2. Pipeline trả lời (9 bước — `ChatOrchestrator`)

```
message → kill-switch (TenantAi:Enabled) → replay dedupe (messageId, G8)
  → quota gate (metering, G7) → GetOrCreateSession → config → policy
  → refused-topic check (G6, trước mọi LLM) → IntentRouter (rule → LLM classify)
  → PolicyEngine (tenant routes override system) → Retriever (SQL + pgvector)
  → UsagePermissionFilter (G15: intent × classification × channel, fail-closed)
  → LLM (prompt = persona + guardrails + facts + history window 10)
  → AnswerValidator (schema + grounded check + stale 24h) → persist → response
```

- Mọi failure path → template (không throw): LLM down → structured fallback; schema fail → 1 retry → fallback.
- `messageId` idempotency: replay trả câu đã lưu, không tính phí 2 lần.
- NoData (không facts) → `offerHandoff=true` → widget hiện chip "Hỏi cửa hàng trực tiếp" — đồng thời là **tín hiệu đầu vào của Learning Suggest**.

### 5.3. Intent router (`IntentRouter`)

- Rule-first (keyword không dấu, thứ tự cố định): PolicyViolation → ExternalKnowledge → Nutrition → Availability → Recipe → BrandStory → OrderHelp → **Faq** → Price → Menu → ChitChat.
- Không khớp → LLM classify 1 nhãn; LLM **không bao giờ** được trả `PolicyViolation` (nhãn đó chỉ từ rule — chống injection).
- Còn không chắc → `OutOfScope` → refuse lịch sự.

### 5.4. Endpoints

**Public (qua Gateway, anonymous + rate-limited):**

| Endpoint | Chức năng |
|---|---|
| `POST /api/public/tenant-ai/chat` | Chat widget storefront |
| `GET  /api/public/tenant-ai/widget-config?tenantId=` | `{aiEnabled}` cho widget (200 ẩn khi tắt) |
| `GET  /api/public/tenant-ai/sessions/{sid}/messages?tenantId=` | Resume lịch sử phiên |
| `GET  /api/public/tenant-ai/search?query=&tenantId=` | Semantic search storefront (session định danh theo hash query) |
| `POST /api/public/assistant-link/{slug}/resolve` + `/chat` + `/widget-config` + `/sessions` | AI Link surface |

**Internal `vanan-ai` (`/v1/*`, caller key + scope + signed ctx):**

| Endpoint | Scope | Chức năng |
|---|---|---|
| `GET /v1/health` | — | Health + db |
| `POST /v1/chat` · `GET /v1/chat/sessions/{sid}/messages` | public-chat | Chat + resume |
| `GET /v1/config/widget` | public-chat | Widget config (greeting/suggestions) |
| `POST /v1/knowledge/products:batch` · `DELETE /v1/knowledge/products/{id}` | ingest | Sync sản phẩm (auto-trusted) |
| `POST /v1/knowledge/documents` · `DELETE /v1/knowledge/documents/{id}` | ingest | Document ingest (pending) |
| `GET /v1/knowledge/status` | ingest | Coverage/status (products, docs, stale) |
| `GET /v1/admin/knowledge/documents` · `GET /v1/admin/knowledge/pending` | admin | DS + chờ duyệt |
| `POST /v1/admin/knowledge/{id}/approve` · `…/reject` | admin | Duyệt (chọn classification) / từ chối |
| `POST /v1/admin/knowledge/extract` · `…/extract-groups` | admin | Auto-Builder (preview only) |
| `PUT/GET /v1/admin/tenants/{id}/profile` · `/policy` · `POST …/publish` · `…/rollback` | admin | Persona, policy, version |
| `GET/PUT /v1/admin/tenants/{id}/learning` | admin | Opt-in Chat Learning Suggest per-tenant (default OFF) |
| `GET /v1/admin/tenants/{id}/conversations` · `/usage` | admin | Review hội thoại + số liệu |
| `DELETE /v1/admin/tenants/{id}` | admin | Purge tenant (X-Confirm-Purge) |

### 5.5. Security model (threat suite)

| ID | Guard | Cơ chế |
|---|---|---|
| T1 | Cross-tenant leak | TenantId filter mọi query + vector SQL; tests T1 |
| T2 | Prompt injection | Rule `PolicyViolation` trước LLM + IngestSanitizer (T3) |
| T6 | Ctx forgery/expiry | HMAC-SHA256 + ±5 phút; server-side tenant resolve |
| T8 | Ví = 0 | QuotaGate → `quota_denied`, 0 LLM call |
| T11 | Pending doc invisible | Chỉ `approved+public+active` vào retrieval |
| T12 | Scope 403 | gateway key → ingest/admin = 403 |
| G14 | Approval gate | Ingest → `pending_review` → chunk/embed chỉ sau approve |
| G15 | Permission filter | `intent × classification × channel`; thiếu rule = fail closed |
| G16 | Staleness | `SyncedAtUtc` > 24h → đánh dấu stale, không khẳng định |

### 5.6. Chat Learning Suggest — thiết kế (W3f, 2026-11-27)

**Gate — 3 lớp AND** (mọi pass đọc lại mỗi lần chạy, không cache):
1. Env `Learning:Enabled` — kill-switch deploy-time (default `true`).
2. `system_settings["LearningSuggest:Enabled"]` — master switch runtime toàn cục (absent = ON; ops set `"false"` qua SQL, không cần redeploy).
3. `system_settings["LearningSuggest:{tenantId}:Enabled"]` — opt-in per-tenant (absent = **OFF**; set qua `PUT /v1/admin/tenants/{id}/learning` hoặc SQL). Pattern `InstanceFeatureGate`: global OFF thắng; purge tenant xóa luôn key opt-in.

**Input:** assistant turns trả lời bắt đầu bằng `ChatTemplates.NoData`, trong window `LookbackDays`, intent ∈ {Faq, Menu, Price, Availability, Recipe, Nutrition, BrandStory}, không refused, channel ≠ `preview`.

**Pipeline (mỗi tenant):**
1. Pair user question (assistant.MessageId = user.MessageId + `:reply`).
2. `QuestionClusterer` (pure): normalize (IntentRouter.Normalize) → lọc rác (4–200 ký tự) → group chính xác → merge greedy theo Dice-bigram ≥ `SimilarityThreshold` → giữ cụm ≥ `MinClusterSize`, representative = câu xuất hiện nhiều nhất.
3. Skip nếu pending chat-learn đã ≥ `MaxPendingPerTenant`; dừng khi tạo đủ `MaxSuggestionsPerTenant`.
4. Dedupe qua `SourceRef = "chat-learn:" + tenantId:N + ":" + sha256(normalized)[..16]` (trùng → skip; idempotent rerun).
5. LLM draft qua `FaqDraftGenerator` (prompt guardrail: không bịa giá/SĐT/chính sách; không chắc → ghi "cần chủ shop xác nhận"; JSON schema) — LLM off/fail/JSON hỏng → skip, không crash.
6. `KnowledgeIngestService.AddDocumentAsync(type=faq, classification="internal", SourceRef)` → sanitize (T3) + **PendingReview** → owner duyệt (chọn `public` để public chat dùng).

**Files:** `VanAn.TenantAi.Ai/QuestionClusterer.cs` · `VanAn.TenantAi.Ai/FaqDraftGenerator.cs` · `VanAn.TenantAi.Api/Services/ChatLearningSuggestService.cs` (BackgroundService, delay 60' + chu kỳ 24h) · `VanAn.TenantAi.Domain/SystemSetting.cs` + bảng `system_settings` (migration `AddSystemSettings`) · `TenantAdminService.Get/PutLearningSettingAsync` + 2 endpoints trong `Program.cs` · ShopERP: `TenantAiApiClient` + card "Tự học có kiểm soát" trong `TenantAiPolicy.razor` · tests: `QuestionClustererTests` (9) · `FaqDraftGeneratorTests` (8) · `ChatLearningSuggestServiceTests` (16 — caps, idempotent, LLM fail, cross-tenant, excluded intents/channels, **gate global/per-tenant**) · `AdminApiTests` (learning endpoints).

### 5.7. Testing & RV

- **TDD:** test trước cho mọi logic mới; bộ test `VanAn.TenantAi.Tests` (InMemory WAF + FakeLlm + FakeEmbedding; `SchemaMigrationTests` cần Docker/Testcontainers).
- **ThreatSuite:** `tests/…/ThreatSuite/` (T1–T10) — chạy cùng CI.
- **Eval:** `Eval/EvalHarnessTests` + `evalset.json`.
- **RV production (bắt buộc sau mỗi deploy):** `.devin/rules/runtime-verification.md` — 5 lớp, STOP at first failure. Precedent script tenant-ai: `6_Testing/rv-scripts/tenant-ai-rv/run-on-vps.sh` (health → auth matrix → ingest → pending invisible → approve → chat grounded → T1 → T2 → cleanup). Sweep W3f: `rv-w3f-sweep.sh` (deploy marker, NoData input path, seed+PG verify+cleanup, admin surface).
- **Vận hành job:** sau deploy, pass đầu chạy sau `StartupDelayMinutes` (60') → log `learning suggest pass done tenants=… clusters=… created=…`; bằng chứng = pending docs `source_ref LIKE 'chat-learn:%'` trong PG.

### 5.8. Mở rộng

- **Surface mới:** mọi kênh public dùng chung widget/backend qua Gateway — thêm channel vào allowlist `AiChatChannels` (Gateway) + `SanitizeChannel` (vanan-ai).
- **Loại tri thức mới:** thêm vào `DocTypes` (ingest) + `DocTypesByIntent` (retriever); classification mới cần review G15.
- **Search UX public:** mọi search UI trên public surface PHẢI dùng `VanAnPublicSearchBar` + `VanAnDidYouMeanChip` + `ITenantSearchService` (anti-drift — xem `docs/design/mobile-search-ux-pattern.md`).
- **AI voice (S2S):** chưa có (audit 2026-11-27) — candidate Gemini Live; voice hiện tại là người–người (`10_Voice/vanan-voice`).

---

## 6. Câu hỏi thường gặp (FAQ)

**Q: AI có tự học từ hội thoại không?**
A: Có kiểm soát (từ 2026-11-27): AI tự **đề xuất** FAQ từ câu hỏi lặp lại mà nó chưa trả lời được — nhưng **luôn là bản nháp chờ bạn duyệt**. Nó không tự sửa kiến thức, không tự publish, không học chéo cửa hàng.

**Q: Vì sao AI trả lời "chưa có dữ liệu xác nhận"?**
A: Câu hỏi thuộc phạm vi hợp lệ nhưng cửa hàng chưa có tri thức (sản phẩm/docs duyệt) — nạp thêm tri thức §3.3 hoặc để Learning Suggest đề xuất.

**Q: Làm sao biết AI trả lời đúng?**
A: AI chỉ grounded trên dữ liệu đã duyệt; mỗi câu trả lời kèm `sourceIds`. Data cũ > 24h bị đánh dấu "thông tin có thể cũ". Chủ shop nên duyệt kỹ docs `public`.

**Q: Khách hỏi bao nhiêu thì tốn phí?**
A: Chỉ câu trả lời LLM thật tính phí (`ai.chat.message`); câu bị từ chối/lỗi/không dùng LLM không thu phí. Xem rate card trong `.devin/rules/metering.md`.

**Q: Hội thoại khách lưu bao lâu?**
A: 90 ngày, sau đó tự xóa (retention).

**Q: Tắt AI ở đâu?**
A: Per-shop: `/settings/shop-features` (bỏ tick AI Chatbox). Toàn cục: `TenantAi:Enabled=false`. Learning Suggest per-shop: toggle "Tự học có kiểm soát" trên `/admin/tenant-ai/policy`; toàn cục: `Learning:Enabled=false` hoặc set `system_settings["LearningSuggest:Enabled"]='false'` (xem §4.5).

**Q: Thêm cửa hàng mới cần làm gì cho AI?**
A: Bật 2 flag → sync sản phẩm tự động → nạp ≥ 3 docs (Auto-Builder/OCR/FAQ) → publish profile → thử chat → theo dõi corpus trên hub.