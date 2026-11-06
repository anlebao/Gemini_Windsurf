# SRS — VẠN AN TENANT AI (`vanan-ai`) v1.1 — CONSOLIDATED BASELINE

> **Ngày:** 2026-11-06 · **Trạng thái:** ✅ APPROVED v1.1 — user duyệt 2026-11-06 · **【PO-1..PO-7】 RESOLVED theo đề xuất:** PO-1 `degrade` · PO-2 `ai.chat.degraded` MeterOnly · PO-3 cap 20 msg/session/giờ · PO-4 retention 90d · PO-5 product sync auto-trusted · PO-6 pilot survey ở P7 (ưu tiên nhà hàng chay) · PO-7 billing tier quyết khi cần
> **Nền tảng:** Service độc lập theo precedent `vanan-metering` · DB = **PostgreSQL + pgvector** (CHỐT) · LLM/Embedding = **Gemini API** (CHỐT, model config-driven)
> **Tên dài hạn:** Vạn An **Tenant Intelligence Platform (TIP)** — `vanan-ai` là service triển khai đầu tiên
> **Nguyên tắc:** Tenant Knowledge First · Tenant Context Isolation · Controlled External Knowledge · Grounded Answers · Tenant Identity · "AI là lớp giao tiếp thông minh, KHÔNG phải nguồn dữ liệu kinh doanh" · **Prompt không phải lớp bảo mật**
> **Quyết định user chốt (2026-11-06):** Q1 service riêng + PG/pgvector · Q2 Gemini Embedding · Q3 pilot = tenant F&B production (ưu tiên nhà hàng chay nếu có) · Q4 P1 có gợi ý đặt món (AI chỉ gợi ý, KHÔNG tạo order)

---

## 0. CHANGELOG v1.0 → v1.1 (external review consolidation)

| # | Review item | Verdict | Thay đổi |
|---|---|---|---|
| 0.1 | Tenant claim trust — key lộ = spoof tenant | **NHẬN** (triển khai nhẹ hơn đề xuất) | [G1] tách: caller scopes + **HMAC-signed Tenant Context** (không full JWT). Allowlist tenant áp cho `shoperp-*`; `gateway` phục vụ mọi tenant by design → mitigation = signed ctx + scope + audit |
| 0.2 | Metering fail-open mâu thuẫn fail-closed | **NHẬN cốt lõi, SỬA cách giải** | [G7]: check fail + cache hết hạn → **degrade structured-only, KHÔNG gọi LLM** (thay vì refuse hẳn hoặc allow mù). 【PO】 chọn degrade vs refuse |
| 0.3 | Confidential vào public context | **NHẬN hoàn toàn** | [G5] viết lại: public chat CHỈ retrieve `Public` + structured fields; Confidential → **Approved Public Fact Extraction** → Public mới dùng được |
| 0.4 | Chốt model/index quá sớm | **NHẬN một nửa** | Index default đổi **HNSW** (IVFFlat chỉ khi >1M chunks); dims config-driven; thêm bước verify model API pre-deploy. Phản biện: `gemini-3.5-flash-lite` là default production đã RV-verify, không phải chọn mò |
| 0.5 | P1 quá nặng | **NHẬN** | §15: task cards → **5 implementation gates** có gate criteria + test evidence bắt buộc |
| 0.6 | Admin P2 nhưng ai duyệt knowledge P1? | **NHẬN — lỗ hổng thật** | [G14] approval gate trong data model: `pending_review→approved`, public chỉ đọc `approved`; P1 duyệt qua admin API + operator script (không chờ UI) |
| 0.7 | Knowledge Usage Permission layer | **NHẬN** | [G15] post-retrieval context filter (intent × classification × channel) trước prompt — defense-in-depth |
| 0.8 | Stale data / version / authoritative source | **NHẬN** | [G16] `source_version` + staleness TTL: stale → strip volatile claims (giá/tồn kho), giữ stable facts + flag |
| 0.9 | Metrics: Grounded Answer Rate, Unsupported Claim Rate | **NHẬN** | NFR-10 + eval harness trong test suite |
| 0.10 | SQL/RAG conflict test | **NHẬN** | T13: structured data thắng vector khi xung đột (precedence test) |
| 0.11 | Pilot ưu tiên nhà hàng chay | **NHẬN** | §3.1: pilot = tenant F&B production đủ dữ liệu; ưu tiên nhà hàng chay nếu tồn tại |
| 0.12 | "Chưa giao code, đánh dấu quyết định PO" | **NHẬN** | Các mục 【PO】 rải trong tài liệu — không implement trước khi PO duyệt |

---

## 1. BỐI CẢNH & MỤC TIÊU

**Yêu cầu:** mỗi tenant trong Vạn An Ecosystem có một trợ lý AI tư vấn riêng — có tri thức, tính cách và **giới hạn trả lời riêng**. AI có kiến thức nền tổng quát nhưng khi tư vấn cho khách của tenant X thì phải **ưu tiên và tuân thủ tuyệt đối** phạm vi thông tin tenant X được phép cung cấp.

**Ví dụ chuẩn:** khách đang ở storefront một nhà hàng chay thì AI đóng vai **nhân viên tư vấn của chính nhà hàng đó** — chỉ giới thiệu món trong menu của quán, chỉ kể câu chuyện thương hiệu do chủ quán cung cấp — không phải một "chuyên gia ẩm thực tự do".

**Định vị:** Vạn An Tenant Intelligence Platform — một nền tảng AI dùng chung, nhiều trợ lý AI có bản sắc và tri thức riêng. **Tenant sở hữu knowledge + policy; Vạn An sở hữu AI infrastructure.** Không fine-tune per tenant: LLM chung + RAG + structured data + policy riêng.

**Mục tiêu SRS:** đủ để (a) build `vanan-ai` bảo toàn base code; (b) QA viết test nghiệm thu fail-closed; (c) tenant onboard không cần hiểu RAG/embedding/prompt engineering.

---

## 2. HIỆN TRẠNG (khảo sát codebase 2026-11-06 — đã xác minh)

| Thành phần | Hiện trạng | Kết luận |
|---|---|---|
| LLM integration | `3_CoreHub/Services/LlmEnhanceVoucherTextParseService` — Gemini API, options `SmartEntryLlm` (env `SmartEntryLlm__ApiKey`, rỗng = tắt), per-tenant flag `SmartEntryLlmEnabled` qua `IShopFeatureSettingsService`, schema validation + merge, **fail-closed** | ✅ **Reference implementation** — clone pattern; `vanan-ai` KHÔNG reference `3_CoreHub` |
| LLM production lesson | Comment trong code: `gemini-3.8-flash` mất 26s + 503 trên VPS (RV 2026-11-01) → default `gemini-3.5-flash-lite`; thinking-model JSON 15–40s → timeout 30s | ✅ Đưa vào NFR + model defaults (đã production-verified) |
| Embedding/Vector infra | `3_CoreHub/Infrastructure/SemanticSearch/`: `IEmbeddingService`, `IVectorStore`, `SqliteVectorStore`, `LocalEmbeddingService` (**hash stub**, "Production should use OpenAI text-embedding-3-small"), `IndexingPipeline`, `DocumentChunk` | 🟡 Interface/pipeline tham khảo; impl mới: `GeminiEmbeddingService` + `PgVectorStore` |
| Chat UI | `UI.Platform/Components/Realtime/RealtimeChatPanel.razor` — subject-agnostic, SignalR `/hubs/messaging`, `IRealtimeChatClient` → Gateway | 🟡 Reuse pattern UI shell; AI widget = component mới (HTTP REST, không SignalR) |
| Tenant resolution | `ShopInstance` + `ShopInstanceService` (multi-VPS routing), `KhachLinkInstance` (domain binding), `MeteringTenantAttributeResolver` — resolve **server-side** | ✅ REUSE — Gateway resolve → signed claim vào `vanan-ai` |
| Public anonymous endpoint | `PublicOrdersController`, `PublicBookingController` — anonymous + rate-limited + arch-test exemption | ✅ REUSE pattern |
| Products/Menu | **ShopERP per-tenant SQLite** — Gateway PG KHÔNG có Products (Option C) | ⚠️ **Key delta** — sync curated knowledge ShopERP → `vanan-ai` |
| Metering | `8_Metering/vanan-metering` — `X-Caller-Id`+`X-Metering-Key`, `Idempotency-Key`, wallet/quota/deny, `IMeteringClient`, caller client fail-open | ✅ REUSE — `ai.chat.message` serviceCode; **deviation: AI dùng chính sách chặt hơn fail-open mặc định** [G7] |
| Service riêng precedent | `8_Metering/vanan-metering`: sln riêng + `src/{Api,Contracts,Domain,Infra}` + tests + Dockerfile; VPS riêng; nginx path `/admin/metering/api/` → internal `:5050`; entities KHÔNG vào `1_Shared/Domain.cs` | ✅ Clone cấu trúc `9_TenantAI/vanan-ai` |
| Auth service-to-service | API key per caller + `CallerKey.IsAdmin`; `GatewayAdminApiClientBase` | ✅ REUSE + mở rộng: caller **scopes** + signed tenant ctx [G1] |
| DataProtection | `QRChannel.EncryptedToken` dùng ASP.NET DataProtection | ✅ REUSE cho signing secret/secrets at-rest |
| Secrets discipline | `DevToken__Secret` rules — inline env only | ✅ Áp dụng `TenantAi__*` secrets |
| Feature flag | `IShopFeatureSettingsService` — `SmartEntryLlmEnabled` precedent | ✅ REUSE — `TenantAiEnabled` |
| Storefront | KhachLink PWA (WASM chung) + `KhachLinkInstance` per domain; Directory SSR | ✅ Widget trong KhachLink |
| Order creation | Option C: KhachLink → Gateway order creator (snapshot ProductName/VatRate) → PG → NATS → ShopERP | ✅ AI gợi ý → cart hiện hữu; **AI không tạo order** |
| Knowledge base / Intent / Policy / Validator | **KHÔNG tồn tại** | ❌ BUILD — trọng tâm SRS |

---

## 3. PHẠM VI

### 3.1 Trong phạm vi — P1 chia 5 GATE (không qua gate sau khi gate trước chưa có test evidence)

| Gate | Thành phần | Evidence bắt buộc để qua gate |
|---|---|---|
| **P1-G1 Foundation** | Service `vanan-ai` skeleton (Api/Domain/Infra/Contracts/Ai) · PG + pgvector · caller auth (key + **scopes** + **signed tenant ctx**) · health · deploy pipeline | Build+test pass · deploy VPS riêng · auth tests: bad-key 401, wrong-scope 403, expired/invalid signed ctx 401 |
| **P1-G2 Isolation + Knowledge** | Data model · ingest API (`products:batch`, documents) · **approval gate** (pending_review→approved) · ShopERP sync client · operator tooling script · chunk/embed pipeline | Cross-tenant retrieval tests · approval gate tests (pending doc không lọt ra) · ingest idempotency |
| **P1-G3 Chat core** | Intent Router · Policy Engine · **Knowledge Usage Permission filter** · SQL-first retriever · pgvector RAG · LLM Orchestrator · Answer Validator · fallback paths | Intent×policy matrix tests · grounded answer eval · T1–T5 tests |
| **P1-G4 Storefront** | Gateway public endpoint + signed ctx · AiChatWidget (UI.Platform) · product cards → cart · session resume | E2E: hỏi→gợi ý→thêm giỏ→checkout flow Option C · spoof tenant test T6 |
| **P1-G5 Metering + Pilot** | Metering check/ingest + degrade policy · `TenantAiEnabled` flag · knowledge readiness pilot tenant F&B · RV production | T8 quota deny · T10 freshness · 5-layer RV · pilot sign-off |

**Pilot (D4 + review):** tenant F&B **đang chạy production**, đủ dữ liệu (menu + ít nhất FAQ/story sẽ chuẩn bị qua operator tooling); **ưu tiên nhà hàng chay nếu tồn tại** trong hệ thống; candidate fallback: Vạn An Cafe. 【PO】 chốt tenant cụ thể ở task card P7.

**P2 — Admin SaaS hoàn thiện:** Admin UI ShopERP `/admin/tenant-ai/*` (UI Platform) — Identity · Profile · Story · FAQ CRUD · Upload docs · Permission config · Preview · Publish/Rollback · Conversation review · Usage · Sync status. P1 chỉ có **operator tooling** (script + admin API) cho PO/technical operator chuẩn bị + duyệt knowledge pilot.

**P3 — Controlled External Knowledge:** External Knowledge Gateway · allow-list nguồn · tenant toggle phạm vi · citation · refuse vượt policy. External luôn là lớp ưu tiên THẤP NHẤT.

**P4 — Ecosystem AI:** booking assist · đa điểm chạm · insight aggregate (anonymized) · vận hành hỗ trợ.

### 3.2 Ngoài phạm vi (ghi nợ — KHÔNG làm)

- Fine-tune/train model riêng per tenant.
- AI tự tạo order/thanh toán/đổi giá-tồn kho (chỉ gợi ý; order qua Option C).
- Tài liệu `Internal`/`Confidential`/`Restricted` **nguyên bản** vào public chat context — không ngoại lệ [G5].
- Voice chat, multi-modal trong v1.
- External knowledge tự do (web search không kiểm soát) — vĩnh viễn cấm ngoài External Knowledge Gateway.
- AI copilot nội bộ (kế toán/kho) — module khác.
- Streaming SSE — P1 full response; đánh giá lại P2.

---

## 4. GIẢ ĐỊNH & QUYẾT ĐỊNH — [G#] giả định · [D#] đã chốt user · 【PO】 cần Product Owner duyệt

| # | Quyết định / Giả định | Mô tả |
|---|---|---|
| [D1] | **Service độc lập `9_TenantAI/vanan-ai`** — CHỐT | `VanAn.TenantAi.sln` + `src/{Api,.Domain,.Infra,.Contracts,.Ai}` + tests + Dockerfile + image `ghcr.io/anlebao/vanan-ai`. Entities AI **KHÔNG vào `1_Shared/Domain.cs`**. Không reference `1_Shared`/`3_CoreHub`. |
| [D2] | **DB = PostgreSQL + pgvector** — CHỐT | PG riêng trong compose. `Pgvector.EntityFrameworkCore`. **Index default = HNSW** (không cần training, recall tốt ở scale nhỏ); exact kNN chấp nhận <50k chunks; IVFFlat chỉ đánh giá lại >1M chunks. Embedding dims **config-driven** (default theo model verify — ví dụ `text-embedding-004`→768). |
| [D3] | **LLM + Embedding = Gemini** — CHỐT | Chat default `gemini-3.5-flash-lite` (production-verified); embedding `text-embedding-004` (cả hai **verify trực tiếp với API trước deploy P1** — task card có bước verify, model đổi qua config không đổi code). `TenantAiLlm` env section — ApiKey rỗng = AI tắt hoàn toàn. |
| [D4] | **Pilot = tenant F&B production** — CHỐT | §3.1 — ưu tiên nhà hàng chay nếu có. |
| [D5] | **P1 có gợi ý đặt món** — CHỐT | `products[]` structured refs → "Thêm vào giỏ" → Option C flow. AI không tạo order. |
| [D6] | **Deploy = VPS riêng + nginx proxy** — theo precedent metering | VPS `vanan-ai` đề xuất; expose `…/ai/api/` → internal port; không domain mới; external port đóng firewall. |
| [D7] | **Auth = API key + scopes + signed tenant context** — v1.1 MỚI | (a) `CallerKeys` có `scopes`: `gateway`→`public-chat` chỉ; `shoperp-{inst}`→`ingest`+`admin` **+ `caller_tenant_scope` allowlist** (shoperp-A chỉ ingest/admin cho tenant của nó). (b) `X-Tenant-Ctx`: base64(`{tenantId,callerId,iat,exp,requestId}`) + `X-Tenant-Ctx-Sig` = HMAC-SHA256(**signing secret riêng**, tách API key — DataProtection-managed). Reject khi: sig sai, hết hạn (±5ph skew — quy ước metering G5), `callerId` mismatch, tenant ngoài scope. **Gateway phục vụ mọi tenant by design** → không allowlist được; mitigation = signed ctx (key lộ một mình không forge claim) + scope + audit mọi request + rate limit. JWT asymmetric = upgrade path sau, không phải P1. |
| [G2] | **Tenant isolation ở retrieval, không chỉ prompt** | Mọi bảng knowledge có `tenant_id`; mọi query filter trước khi trả. Vector: `WHERE tenant_id = @t ORDER BY embedding <=> @v`. Session/conversation/cache partition per tenant. Không code path bypass — arch-test enforced nếu khả thi. |
| [G3] | **Structured-first, vector-second; structured thắng khi xung đột** | Intent structured → SQL → LLM chỉ diễn đạt. Intent semantic → vector → LLM grounded chunks. **Nếu vector doc mâu thuẫn structured data (giá/menu): structured data wins** — T13 test. LLM không tự query DB, không tự chọn tenant. |
| [G4] | **Grounded product refs** | LLM trả JSON `productRefs[]` → `AnswerValidator` đối chiếu knowledge tenant — ref không tồn tại = strip. Pattern = schema validation `LlmEnhanceVoucherTextParseService`. |
| [G5] | **Classification + Public-fact extraction — v1.1 VIẾT LẠI** | Classification: `Public | Internal | Confidential | Restricted`. **Public chat context CHỈ chứa: `knowledge_documents.classification=Public` + structured fields (`product_knowledge.*`).** Confidential/Internal **không bao giờ** vào retrieval public — kể cả "dùng để tư vấn". Trường hợp công thức bí mật → tư vấn dị ứng: tenant/operator trích public facts (allergen profile, ingredient list) → lưu `product_knowledge.allergens_json` / Public doc sau **approval** → AI dùng bản đã duyệt, không tiếp xúc nguyên bản. `Restricted` không bao giờ ingest cho chat. |
| [G6] | **Fail-closed có định nghĩa chính xác** | "Fail-closed" = **không phát sinh chi phí LLM khi không xác minh được quyền/điều kiện**. Cụ thể: thiếu knowledge → "chưa có dữ liệu xác nhận" · policy không match → `OutOfScope` refuse · LLM down/timeout → **structured-only answer hoặc fallback message** (không phải màn trắng) · metering deny → refuse lịch sự · metering check fail + cache hết hạn → **degrade structured-only** [G7]. |
| [G7] | **Metering: deny→refuse, check-fail→degrade — v1.1 VIẾT LẠI** | Check `wallets/{t}/check?serviceCode=ai.chat.message` trước phục vụ, cache 30s/tenant. Ma trận: ví đủ → allow · **ví hết → refuse + hướng dẫn** · check fail + cache còn hiệu lực → allow theo cached balance · **check fail + cache hết hạn → KHÔNG gọi LLM** — mặc định degrade structured-only (trả lời từ SQL/template, zero LLM cost) 【PO: chọn `degrade` (mặc định) hay `refuse` cho `ai.chat.message` — cấu hình `Metering:QuotaFailPolicy` per serviceCode】. Đây là **deviation có chủ đích** khỏi fail-open generic của metering — AI call tốn tiền ngoài mỗi request nên chính sách chặt hơn; ghi rõ trong task card. Ingest usage sau trả lời (quantity=1; token count ghi message row cho phân tích). |
| [G8] | **Idempotency + rate limit** | `sessionId` + `messageId` dedupe. Gateway: rate-limit per-IP + session cap (**20 msg/session/giờ** — 【PO】 duyệt ngưỡng). |
| [G9] | **Knowledge sync = HTTP push P1** | `POST /v1/knowledge/products:batch` idempotent, replace-by-source khi `fullSync=true`. Trigger: product CUD + manual "Đồng bộ AI" + nightly reconcile. NATS `vanan.ai.knowledge.*` đánh giá P2+. |
| [G10] | **Conversation retention 90 ngày** 【PO】 | `expires_at` + daily cleanup. `Restricted` — chỉ review/quality, không export/training. PDPD/NĐ13: quyền xóa trong admin P2. |
| [G11] | **Config versioned + Publish gate** | Draft → Preview → Publish (version mới active); rollback = activate cũ. Knowledge/conversation gắn `ConfigVersion` truy vết. |
| [G12] | **Prompt KHÔNG phải lớp bảo mật** | Persona trong prompt; isolation/classification/scope enforce bằng code (Retriever + PolicyEngine + UsagePermission + Validator). Test suite chứng minh injection không vượt code guard. |
| [G13] | **Single-identity** | PK `Id` duy nhất, không dual-identity. `Guid` (UUIDv7 nếu lib sẵn). |
| [G14] | **Knowledge approval gate — v1.1 MỚI** | Mọi `knowledge_documents` ingest → `approval_status=pending_review`; **public retrieval chỉ đọc `approved`**. Approve qua admin API `POST /v1/admin/knowledge/{id}/approve|reject` — P1 operator dùng script/curl; P2 UI tenant tự duyệt. Reject kèm lý do. Auto-approve KHÔNG tồn tại cho channel public. Product knowledge từ sync pipeline (structured, source = ShopERP) coi như đã approved vì nguồn là hệ thống — 【PO】 xác nhận ngoại lệ này. |
| [G15] | **Knowledge Usage Permission filter — v1.1 MỚI** | Lớp kiểm soát **sau retrieval, trước prompt**: mọi context item check `intent × classification × channel` permission matrix (bảng code/config `knowledge_usage_permissions`) — ví dụ: `(intent=NutritionQuery, classification=Confidential, channel=public)` → **drop item**. Đây là defense-in-depth độc lập với PolicyEngine (filter trước) — bug ở một lớp không đủ để leak. Log mọi item bị drop. |
| [G16] | **Freshness/staleness control — v1.1 MỚI** | `product_knowledge.source_version` + `synced_at`; TTL cấu hình `Knowledge:StaleHours` (default 24h). Khi stale: validator **strip volatile claims** (giá, availability) khỏi answer, giữ stable facts (tên, mô tả), đính kèm flag `staleData=true` trong response metadata + đếm vào log/alert admin. Nightly reconcile so count/hash với nguồn → drift report. |
| [G17] | **Provider abstraction — v1.1 MỚI** | `ILlmChatClient` + `IEmbeddingProvider` interfaces; impl đầu tiên = Gemini. Không phải over-engineering: đây là contract để (a) test bằng fake deterministic, (b) đổi provider/model bằng config, (c) verify model trước deploy. |

---

## 5. KIẾN TRÚC

```
Khách hàng (browser)
   │  storefront = KhachLink PWA (per-domain KhachLinkInstance)
   ▼
┌─────────────────────────────┐
│ AiChatWidget (UI.Platform)  │  POST /api/public/tenant-ai/chat
└──────────────┬──────────────┘   {message, sessionId, messageId}
               │ anonymous + rate-limited
               ▼
┌──────────────────────────────────────────────┐
│ Gateway (2_Gateway)                          │
│  PublicTenantAiController                    │
│  → resolve tenant: KhachLinkInstance/        │
│    ShopInstance (SERVER-SIDE)                │
│  → sign Tenant Context (HMAC secret)         │
│  → POST {ai}/v1/chat {ctx, ctxSig, sessionId,│
│    messageId, text} + X-Caller-Id + X-Ai-Key │
└──────────────┬───────────────────────────────┘
               │ internal HTTP
               ▼
┌──────────────────────────────────────────────────────────┐
│ vanan-ai (VPS riêng — internal port only)                │
│                                                          │
│  AuthMiddleware: verify key → verify scope → verify      │
│    signed ctx (sig + exp + callerId + tenant-scope)      │
│                                                          │
│  IntentRouter → PolicyEngine → KnowledgeRetriever        │
│       │              │              ├─ StructuredQuery   │
│       │              │              └─ VectorQuery       │
│       │              │                 (pgvector HNSW,   │
│       │              │                  tenant-filtered, │
│       │              │                  Public+approved) │
│       │              ▼                                   │
│       │     KnowledgeUsagePermission (context filter:    │
│       │       intent × classification × channel)         │
│       │              ▼                                   │
│       │        LlmOrchestrator ──→ ILlmChatClient        │
│       │              │              (Gemini impl)        │
│       │              ▼                                   │
│       └──      AnswerValidator → ChatResponse            │
│              {text, products[], sources[], confidence,   │
│               refused?, staleData?}                      │
│                                                          │
│  Metering client (check + degrade + ingest)              │
│  Admin API /v1/admin/* · Ingest API /v1/knowledge/*      │
│  DB: PostgreSQL + pgvector                               │
└──────────────────────────────────────────────────────────┘
               ▲
               │ knowledge ingest (HTTP push, idempotent)
               │ + approval qua admin API
┌──────────────┴───────────────────────────────┐
│ ShopERP (per-tenant SQLite = source of truth)│
│  TenantAiKnowledgeSyncService (push)         │
│  TenantAiApiClient (admin ops)               │
│  P1: operator script · P2: /admin/tenant-ai/*│
└──────────────────────────────────────────────┘
```

**Boundary (HARD):** `vanan-ai` không gọi ngược vào Vạn An core trừ metering + Gemini API. Không shared DB, không FK chéo, không reference `1_Shared`/`3_CoreHub`. Knowledge store = **bản sao curated** — ShopERP SQLite là source of truth.

**Luồng chat (9 bước):**
1. Gateway resolve tenant → sign ctx → forward.
2. `vanan-ai` auth: key → scope → signed ctx (reject khi sai bất kỳ lớp nào).
3. Metering check (deny→refuse; fail+expired cache→degrade flag).
4. Load active `TenantAiConfig` + persona.
5. IntentRouter (rule-based → LLM classify fallback).
6. PolicyEngine: intent→sources; OutOfScope/PolicyViolation → refuse không gọi LLM.
7. Retriever: SQL-first/vector, tenant-filtered, Public+approved only → **UsagePermission filter** drop items vi phạm matrix.
8. LlmOrchestrator: prompt = persona + guardrails + facts(có sourceId) + câu hỏi → JSON `{answer, productRefs[], sourceIds[], confidence}`.
9. AnswerValidator (schema + refs + số liệu + staleness) → persist → ingest metering → response.

---

## 6. API CONTRACT (v1)

Auth: `X-Caller-Id` + `X-Ai-Key` + scope check; tenant-bearing endpoints yêu cầu `X-Tenant-Ctx` + `X-Tenant-Ctx-Sig`. Mutation: `Idempotency-Key`.

### 6.1 Public-facing (caller `gateway`, scope `public-chat`)

Gateway: `POST /api/public/tenant-ai/chat` (anonymous, rate-limited) → forward:

| Method | Path | Body → Response |
|---|---|---|
| POST | `/v1/chat` | `{ctx,sig,sessionId,messageId,text,locale?}` → `{answer, products:[{productId,name,price,imageUrl?}], sourceIds[], confidence, refused, refuseReason?, staleData?, degraded?}` |
| GET | `/v1/chat/sessions/{sid}/messages` | Lịch sử session — ctx phải chứa tenant sở hữu session |
| GET | `/v1/config/widget` | Widget config public: tên AI, lời chào, gợi ý mở đầu, `aiEnabled` (false khi flag off/metering deny) |

### 6.2 Knowledge ingest (caller `shoperp-*`, scope `ingest`, tenant trong allowlist)

| Method | Path | Mô tả |
|---|---|---|
| POST | `/v1/knowledge/products:batch` | `{fullSync:bool, sourceVersion, items:[{productId,name,description?,price,calories?,ingredients?[],allergens?[],available,categoryName?}]}` — replace-by-source khi fullSync |
| DELETE | `/v1/knowledge/products/{productId}` | Gỡ món khỏi knowledge |
| POST | `/v1/knowledge/documents` | `{type:faq|story|policy|menu-note, title, content, classification}` → `approval_status=pending_review` + chunk+embed sau approve |
| DELETE | `/v1/knowledge/documents/{docId}` | Xóa doc + chunks |
| GET | `/v1/knowledge/status` | Doc/chunk count, last product sync + `source_version`, stale flag, pending-review count |

### 6.3 Admin (caller `shoperp-*`, scope `admin`, tenant trong allowlist)

| Method | Path | Mô tả |
|---|---|---|
| GET/PUT | `/v1/admin/tenants/{t}/profile` | `TenantAiProfile`: tên AI, xưng hô, phong cách, business profile |
| GET/PUT | `/v1/admin/tenants/{t}/policy` | `AiPolicy`: intent-scope matrix, refused topics, external allow (P3) |
| POST | `/v1/admin/knowledge/{id}/approve` `/reject` | **Approval gate — P1 operator tooling chính** |
| GET | `/v1/admin/knowledge/pending` | Danh sách chờ duyệt |
| POST | `/v1/admin/tenants/{t}/preview-chat` | Sandbox chat trên Draft — không metering, không persist public |
| POST | `/v1/admin/tenants/{t}/publish` `/rollback` | Config version lifecycle |
| GET | `/v1/admin/tenants/{t}/conversations` | Review (paged) |
| GET | `/v1/admin/tenants/{t}/usage` | Usage tổng hợp (proxy metering) |

### 6.4 Client mỏng

`ShopERP/Services/TenantAiApiClient.cs` — pattern `MeteringApiClient`: timeout 5s admin / 30s ingest, circuit breaker, self-disable khi thiếu config, `BaseAddress` trailing `/`. Gateway forward client riêng (ký ctx bằng signing secret — secret chỉ Gateway giữ, service giữ verify key; HMAC symmetric nên cả hai giữ, nhưng tách khỏi `X-Ai-Key`).

---

## 7. DATA MODEL (`vanan-ai` DB — PG + pgvector)

> Entities trong `VanAn.TenantAi.Domain` (pure). EF config `.Infra`. Mọi bảng tenant-scoped có `tenant_id` + index đầu tiên.

| Bảng | Cột chính | Ghi chú |
|---|---|---|
| `caller_keys` | id, caller_id (unique), key_hash, is_admin, **scopes_json**, **tenant_scope_json** (nullable = all — chỉ gateway), created_at | Scope enforcement |
| `tenant_ai_configs` | id, tenant_id, version, status, ai_name, persona_json, business_profile_json, published_at/by | Append-only per version |
| `ai_policies` | id, tenant_id, config_version, intent_scopes_json, external_allow_json(P3), refused_topics_json | |
| `knowledge_usage_permissions` | id, tenant_id (null=system default), intent, classification, channel, permission(deny/consult-facts-only→v1.1: chỉ Public pass) | Policy-as-data cho G15 filter |
| `product_knowledge` | id, tenant_id, product_id, name, description, price, calories, ingredients_json, allergens_json, available, category_name, **source_version, synced_at** | Curated mirror; staleness tính từ `synced_at` |
| `knowledge_documents` | id, tenant_id, type, title, classification, **approval_status(pending_review/approved/rejected), approved_by, approved_at**, status, source_ref, created_at, updated_at | G14 gate |
| `knowledge_chunks` | id, tenant_id, document_id, chunk_index, content, **embedding vector(N)** — N config theo model, token_count | Index `(tenant_id)` + **HNSW** trên embedding |
| `conversation_sessions` | id, tenant_id, session_id (unique/tenant), created_at, last_active_at, channel, meta_json | |
| `conversation_messages` | id, tenant_id, session_id, role, content, intent, product_refs_json, source_ids_json, confidence, refused, **degraded, prompt_tokens, completion_tokens**, created_at, expires_at | Token count cho cost analysis |
| `ingest_log` | id, tenant_id, kind, idempotency_key, item_count, status, error, created_at | |
| `intent_routes` | id, tenant_id (null=system), intent, allowed_source_flags, requires_facts, refuse_template | Seed defaults |

---

## 8. YÊU CẦU CHỨC NĂNG (FR)

**FR-1 Config & Identity.** Persona/business profile versioned Draft→Preview→Publish; public dùng `published` mới nhất.

**FR-2 Intent Router.** Intents: `MenuQuery, NutritionQuery, PriceQuery, AvailabilityQuery, RecipeQuery, BrandStory, Faq, ExternalKnowledge, OrderHelp, ChitChat, OutOfScope, PolicyViolation`. Rule-based → LLM classify (1 label) → vẫn không chắc = `OutOfScope`.

**FR-3 Policy Engine — intent → sources (bảng bắt buộc):**

| Intent | Sources được phép | Bắt buộc facts |
|---|---|---|
| MenuQuery/PriceQuery/AvailabilityQuery | `product_knowledge` tenant | Có — không facts → "chưa có dữ liệu" |
| NutritionQuery | `product_knowledge` (calories/ingredients/allergens đã xác nhận) | Có — cấm ước lượng |
| RecipeQuery | Public docs type=recipe đã approve **+ public fact fields** (allergen/ingredient profile) | Có — KHÔNG retrieve doc Confidential |
| BrandStory/Faq | Public docs type tương ứng đã approve | Có — không bịa story |
| ExternalKnowledge | Approved external (P3) + disclaimer | Tenant bật scope; tắt mặc định P1 |
| OrderHelp | Static template hướng dẫn | Không |
| ChitChat | Persona only, ngắn | Không — không suy ra facts |
| OutOfScope/PolicyViolation | Refuse template + redirect | — |

**FR-4 Grounded Answer.** Facts phải kèm `sourceIds` trong retrieval set. Validator loại: productRefs không tồn tại · sourceIds ngoài set · claims số (giá/calories) không khớp structured · **claims volatile khi data stale [G16]**. Parse fail → 1 retry → fallback.

**FR-5 Product suggestion → cart.** `products[]` chỉ `available=true` và **không stale** theo volatile-claim rule. Card → "Thêm vào giỏ" → Option C flow.

**FR-6 Knowledge ingest + approval.** Documents → `pending_review` → approve → chunk+embed. Product sync = structured pipeline, auto-trusted theo 【PO】G14. Reject doc kèm lý do.

**FR-7 Conversation.** Persist + session resume; context window N=10 message gần nhất + retrieval mới mỗi turn.

**FR-8 Admin P2 + operator tooling P1.** P1: admin API + script (approve/reject, profile put, publish, status). P2: full UI `/admin/tenant-ai/*`, Owner/StaffOrAbove.

**FR-9 Feature flag + kill switch.** `TenantAiEnabled` per-tenant; `TenantAiLlm__ApiKey` rỗng hoặc `TenantAi:Enabled=false` → AI off toàn hệ.

**FR-10 Refuse nhẹ nhàng.** Template lịch sự + redirect khả dụng; cấm "tôi không được phép" trần.

**FR-11 Knowledge Usage Permission [G15].** Mọi context item qua matrix `intent×classification×channel`; drop + log; test matrix bắt buộc.

**FR-12 Freshness [G16].** Stale → strip volatile claims + `staleData=true`; reconcile nightly + drift report admin.

**FR-13 Provider abstraction [G17].** `ILlmChatClient`/`IEmbeddingProvider`; fake impl cho test deterministic; model verify pre-deploy.

---

## 9. YÊU CẦU PHI CHỨC NĂNG (NFR)

| # | NFR | Target |
|---|---|---|
| NFR-1 | Latency P95 | ≤8s LLM path; ≤2s structured-only/degrade. LLM timeout 30s. |
| NFR-2 | Model config | Chat `gemini-3.5-flash-lite` default; embedding `text-embedding-004` default; **verify API trước deploy**; đổi = config, không code. |
| NFR-3 | Availability | AI down ≠ storefront/cart/checkout down. Widget fallback "AI tạm nghỉ". |
| NFR-4 | Scale P1 | ~50 tenant, ~5k msg/ngày. HNSW mặc định; exact <50k chunks; IVFFlat re-eval >1M. |
| NFR-5 | Security | §10 = acceptance. Không PII ngoài nội dung khách nhắn; secrets env-only; signing secret tách API key. |
| NFR-6 | Cost | Metering check+ingest mọi message; prompt/completion tokens persist per message. |
| NFR-7 | Retention | Messages 90d 【PO】; tenant xóa → purge knowledge+conversations. |
| NFR-8 | Observability | Log per request: tenantId, intent, sources, per-stage latency, refused, degraded, dropped-by-permission count, metering result. `/v1/health`. |
| NFR-9 | Tiếng Việt | Templates/prompt/persona vi-VN; embedding đa ngôn ngữ. |
| NFR-10 | **Quality metrics (v1.1)** | Eval harness trên eval-set per tenant: **Grounded Answer Rate ≥95%** (mọi factual claim có source trong retrieval set) · **Unsupported Claim Rate ≤2%** · Refuse-đúng rate trên OutOfScope set ≥95%. Chạy trong test suite + trước publish config (P2). |

---

## 10. THREAT MODEL & TEST BẮT BUỘC

| # | Threat | Kiểm soát | Test |
|---|---|---|---|
| T1 | Cross-tenant leak | tenant filter mọi query + usage permission | Tenant A hỏi món tenant B → không tìm thấy; vector trả 0 chunk tenant B |
| T2 | Prompt injection user message | Code guardrails + validator | 10 payload → refuse/in-scope, không lộ prompt/config |
| T3 | Injection trong knowledge upload | Ingest sanitize + approval gate + doc không được thi hành như instruction | Doc "IGNORE ALL RULES..." → vẫn tuân policy |
| T4 | Hallucinated product/price | FR-4 validator | Món không có → "chưa có"; fake ref → strip |
| T5 | Confidential leak | **Không retrieve Confidential vào public context** + extraction pipeline [G5] | Hỏi định lượng công thức → refuse phần đó; verify DB-level: query public channel không đọc doc non-Public |
| T6 | Tenant spoofing | **Signed tenant ctx [D7]** + scope + caller-tenant allowlist (shoperp) | Forge ctx với key đúng nhưng sig sai → 401; expired ctx → 401; shoperp caller gọi tenant ngoài scope → 403 |
| T7 | Key/secret leak | Hash at rest, env-only, tách signing secret | Log scan không chứa key |
| T8 | Abuse/chi phí | Rate limit + session cap + metering deny + **degrade khi check fail [G7]** | Ví hết → refuse; metering down + cache hết hạn → verify KHÔNG có LLM call nào phát sinh (assert mock counter = 0) |
| T9 | External abuse | Allow-list P3 | Chưa bật scope → refuse/redirect |
| T10 | Stale knowledge | [G16] + reconcile | Đổi giá → sau sync trả giá mới; giả lập stale → volatile claims bị strip + flag |
| T11 | Approval bypass | [G14] | Doc pending/rejected → không xuất hiện trong retrieval/answer |
| T12 | Caller scope escalation | D7 scopes | `gateway` key gọi `/v1/admin/*` hoặc `/v1/knowledge/*` → 403 |
| T13 | SQL/RAG conflict | [G3] precedence | Doc story nói "giá 50k" vs product_knowledge price=80k → answer dùng 80k |

---

## 11. TÍCH HỢP METERING

- ServiceCode: `ai.chat.message` (**Deny** khi hết ví) · `ai.embedding.batch` (MeterOnly đầu).
- Check: `wallets/{t}/check?serviceCode=…` cache 30s/tenant — theo ma trận [G7].
- Ingest: `POST /v1/usage/events`, `Idempotency-Key`=`chat:{tenantId}:{messageId}`; degraded answers (không LLM) ingest `ai.chat.message` với `quantity=1`? — 【PO】 quyết degraded có tính phí hay không (đề xuất: tính 0.2 định mức hoặc serviceCode riêng `ai.chat.degraded` MeterOnly để đo).
- `aiEnabled=false` trong widget config khi deny → client ẩn widget.

---

## 12. DEPLOY & VẬN HÀNH

- `9_TenantAI/vanan-ai/`: sln + src + tests + Dockerfile + `docker-compose.tenantai.yml`.
- VPS riêng `vanan-ai` (asia-southeast1); image `ghcr.io/anlebao/vanan-ai`; CD multi-VPS deploy set riêng + smoke `/v1/health`.
- PG: image `pgvector/pgvector:pg16`, volume riêng, backup theo chính sách VPS.
- Nginx: `app2.khachvip.online/ai/api/` → internal IP:port (pattern metering); external port đóng.
- Secrets env-only: `TenantAiLlm__ApiKey` · `TenantAi__CallerKey` (shoperp caller) · `TenantAi__GatewayCallerKey` + **`TenantAi__CtxSigningSecret`** · `Metering__*`.
- RV 5-layer sau deploy: health domain · external blocked · no-auth 401 · bad-key 401 · bad-scope 403 · forged-ctx 401 · chat e2e pilot · T1 cross-tenant production.

---

## 13. RỦI RO & VẤN ĐỀ MỞ

| # | Rủi ro/Mở | Hướng xử lý |
|---|---|---|
| R1 | Chi phí Gemini theo tenant | Metering + degrade policy + token metrics; cân nhắc self-host embedding sau |
| R2 | pgvector trên VPS nhỏ | HNSW nhẹ ở scale pilot; monitor; PG tuning theo workload |
| R3 | Knowledge drift | [G16] staleness + nightly reconcile + drift alert |
| R4 | Chất lượng tiếng Việt | Eval set vi-VN trong NFR-10; re-eval embedding nếu recall thấp |
| R5 | Gemini free-tier limit | Backoff + degrade; nâng billing khi cần 【PO】 |
| R6 | Pilot thiếu dữ liệu | Knowledge readiness gate trong P1-G5 + operator tooling |
| R7 | Signing secret compromise | Rotate quy trình trong sysadmin guide; secret tách API key giảm blast radius |
| O1 | SSE streaming | Đánh giá sau P1 |
| O2 | NATS knowledge sync | P2+ |
| O3 | Per-tenant LLM key/model | Deferred — v1 key nền tảng |
| O4 | Directory SSR tenants | P4 |
| O5 | JWT asymmetric thay HMAC | Upgrade path khi multi-service gọi phức tạp hơn |

---

## 14. TIÊU CHÍ NGHIỆM THU (per gate — không evidence không qua gate)

### P1-G1 Foundation
- [ ] Build+tests pass · deploy VPS riêng · health qua domain · external port blocked
- [ ] Auth: no-key 401 · bad-key 401 · `gateway`→admin path 403 · forged ctx 401 · expired ctx 401 · shoperp ngoài tenant-scope 403

### P1-G2 Isolation + Knowledge
- [ ] Ingest products → query đúng tenant · cross-tenant = 0 kết quả (SQL+vector)
- [ ] Approval gate: pending doc không retrievable · approve → xuất hiện · reject → không
- [ ] Ingest idempotent (replay key → không double)

### P1-G3 Chat core
- [ ] "Món nào ít calories?" → chỉ món có calories knowledge tenant · "chưa có dữ liệu" khi thiếu
- [ ] Brand story → theo doc approved, không bịa
- [ ] Usage permission: inject context item vi phạm matrix → drop + log
- [ ] Eval harness: Grounded ≥95% · Unsupported ≤2% trên eval set

### P1-G4 Storefront
- [ ] Widget hiển thị, chat e2e · product card → giỏ → checkout Option C (AI không tạo order)
- [ ] Session resume sau reload · spoof tenant → 401/403

### P1-G5 Metering + Pilot
- [ ] Ví hết → refuse; metering down + cache hết hạn → 0 LLM call (assert)
- [ ] Flag off → widget ẩn · pilot sign-off sau knowledge readiness
- [ ] RV 5-layer + T1–T13 trên production

### P2/P3/P4 — nghiệm thu khi tới phase (admin UI · external gateway · ecosystem).

---

## 15. TASK CARDS ↔ GATES (card→duyệt→implement tuần tự)

| Card | Gate | Nội dung | Phụ thuộc |
|---|---|---|---|
| `task_ai_p0_skeleton` | G1 | Solution + Dockerfile + compose PG/pgvector + health | — |
| `task_ai_p0b_auth` | G1 | CallerKeys + scopes + signed ctx (HMAC) + tenant-scope allowlist + tests | P0 |
| `task_ai_p1_schema` | G2 | Domain + EF config + migrations + pgvector HNSW | P0 |
| `task_ai_p2_ingest` | G2 | Ingest API + approval gate + ShopERP sync client + operator script + chunk/embed + `GeminiEmbeddingService` | P1 |
| `task_ai_p3_chat` | G3 | IntentRouter + PolicyEngine + UsagePermission + Retriever + `ILlmChatClient` + Validator + eval harness | P1, P2 |
| `task_ai_p4_storefront` | G4 | Gateway endpoint + sign ctx + AiChatWidget + product cards→cart + session | P3 |
| `task_ai_p5_metering` | G5 | Metering client + degrade policy + flag + widget enable | P3 |
| `task_ai_p6_tests` | G5 | T1–T13 suite + NFR-10 eval + RV scripts | P3–P5 |
| `task_ai_p7_pilot` | G5 | Model verify API + knowledge readiness + pilot enable + RV production 【PO duyệt mục 【PO】 trước】 | P2–P6 |

Nguồn tham chiếu: `docs/requirements/van_an_phi_nen_tang_metering_srs_v1.md` · `.devin/rules/metering.md` · `3_CoreHub/Services/LlmEnhanceVoucherTextParseService.cs` · `3_CoreHub/Infrastructure/SemanticSearch/` · tài liệu khả thi Tenant AI + external review v1.0→v1.1.
