# Task AI-P3 — Chat Pipeline (Intent → Policy → Retrieve → LLM → Validate)

> **Status:** ✅ IMPLEMENTED (code) `8dae1fce` — 2026-11-06 · tests 99/99 · eval gate pass · chờ CP2 deploy + API RV
> **Gate:** P1-G3 — SRS v1.1 [G2][G3][G4][G5][G6][G12][G15] · FR-2..FR-4, FR-11 · **→ checkpoint CP2 (deploy + API-level RV)**
> **Phụ thuộc:** P1, P2

## Objective

Trái tim của `vanan-ai`: pipeline trả lời grounded, fail-closed, không gọi LLM khi không cần/không được phép. Đây là card lớn nhất — mọi component test độc lập trước khi ghép.

## Components (`VanAn.TenantAi.Ai` + endpoints `VanAn.TenantAi.Api`)

| Component | Spec |
|---|---|
| `IntentRouter` | Rule-based trước (keyword/regex vi-VN: "giá", "bao nhiêu", "calo", "còn không", "câu chuyện", "công thức"…) → LLM classify fallback (prompt classify-only, output 1 label JSON) → vẫn không chắc = `OutOfScope`. Intents per FR-2 |
| `PolicyEngine` | Intent→sources map từ `intent_routes` (system defaults + tenant override qua `ai_policies`). `OutOfScope`/`PolicyViolation` → refuse NGAY, không gọi LLM [G6] |
| `KnowledgeRetriever` | Structured intents → SQL `product_knowledge` (tenant-filtered, `available`/`calories` filters theo intent) · Semantic intents → pgvector `WHERE tenant_id AND document.approval_status='approved' AND classification='Public'` [G2][G5][G14] |
| `KnowledgeUsagePermission` | **Post-retrieval filter [G15]:** mọi context item check `intent × classification × channel=public` matrix → drop + log `droppedByPermission` count vào response log |
| `ILlmChatClient` + `GeminiChatClient` | [G17] `GenerateAsync(prompt, schemaHint)` → `text-embedding`/`generateContent` endpoint, model config `TenantAiLlm:Model` (default `gemini-3.5-flash-lite`), timeout 30s, 1 retry on timeout/5xx |
| `PromptBuilder` | persona (active config) + guardrails + facts (có `sourceId`) + conversation window N=10 + câu hỏi → yêu cầu JSON `{answer, productRefs[], sourceIds[], confidence}` |
| `AnswerValidator` | Parse JSON (fail → 1 retry → fallback template) · strip productRefs ∉ `product_knowledge` tenant · strip sourceIds ∉ retrieval set · **claims số (giá/calories) phải khớp structured data** · stale → strip volatile claims + `staleData=true` [G16] · confidence < ngưỡng hoặc no-facts-required-facts → ép "chưa có dữ liệu xác nhận" |
| `POST /v1/chat` | Orchestrate 9 bước SRS §5 · persist `conversation_messages` (cả degraded flag) · response `{answer, products[], sourceIds[], confidence, refused, refuseReason?, staleData?, degraded?}` |
| `POST /v1/admin/tenants/{t}/preview-chat` | Cùng pipeline, config=draft, không metering, không persist public (scope `admin`) |
| Fallback paths | LLM down/timeout/schema-fail → structured-only template từ SQL result (có facts) hoặc polite fallback (không facts) — `degraded=true` |

## Precedence rule [G3] — bắt buộc

Structured data (product_knowledge) > approved Public docs > external (P3). Vector doc mâu thuẫn structured → **structured wins** (T13).

## Tests bắt buộc (gate evidence — fake `ILlmChatClient` deterministic)

- [x] Intent matrix: ≥15 câu vi-VN thật → đúng intent; câu lạ → OutOfScope, **0 LLM call** cho PolicyViolation — `ChatUnitTests.IntentMatrix_*` (23 cases)
- [x] "món nào ít calories?" → SQL trả món có calories, sorted; không có data → "chưa có dữ liệu xác nhận" — `ChatApiTests.Chat_NutritionQuery_*` / `Chat_PriceQuery_NoMatchingProduct_*`
- [x] Brand story → chunks doc approved; pending/rejected doc → không vào context (T11) — `Chat_BrandStory_*` ×3
- [x] Validator: LLM trả ref giả → strip · trả giá sai structured → strip/đính chính · JSON rác → retry → fallback — `ChatUnitTests.Validator_*` + `Chat_LlmGarbageJson_*`
- [x] UsagePermission: inject context item vi phạm matrix → drop + log count (G15) — `UsageFilter_*` ×3 + `Chat_G15_TenantDenyRule_*`
- [x] T13: doc story "giá 50k" vs price=80k → answer 80k — `Chat_T13_*` + `Validator_T13_*`
- [x] T1: 2 tenant seed → query A trả 0 data B (SQL + vector) — `Chat_T1_*`
- [x] Degrade: fake LLM throw → structured-only answer, `degraded=true`, không crash — `Chat_LlmDown_*`
- [x] Conversation persist + window N=10 · preview-chat dùng draft config — `Chat_PersistsTurn_*` / `Chat_ConversationWindow_*` / `PreviewChat_*`

## Eval harness (NFR-10 — nền tảng cho P6)

`tests/…/Eval/` — eval set JSON per intent (≥30 câu), chạy pipeline với fake+stub, đếm: Grounded Answer Rate, Unsupported Claim Rate, đúng-refuse rate. Gate: **Grounded ≥95% · Unsupported ≤2%**. ✅ `Eval/evalset.json` (33 câu) + `EvalHarnessTests` — metrics đo trên orchestrator thật (InMemory store).

## CP2 — Deploy + API RV (sau card này)

- Deploy `vanan-ai` update lên VPS · RV qua internal/admin API (chưa có storefront):
  - Ingest pilot-shaped data → approve → status
  - Chat ≥5 câu real qua caller `gateway` + ctx ký tay → grounded answers
  - T1 cross-tenant · T5 confidential · T11 approval · T13 conflict trên env thật
- LLM key thật (`TenantAiLlm__ApiKey`) — set lúc CP2, verify latency P95 ≤8s

## Acceptance

- [ ] Toàn bộ tests xanh · eval harness đạt ngưỡng · CP2 API RV pass · "Delivered" ghi vào card

> **CODE DONE 2026-11-06** — 99/99 tests (23 → 99). Components: `Ai/` IntentRouter (rule→LLM classify→OutOfScope) · PolicyEngine + `DefaultPolicySeed` (FR-3 matrix seeded runtime) · UsagePermissionFilter [G15] · `ILlmChatClient`/`GeminiChatClient` (generateContent, 1 retry) · PromptBuilder · AnswerValidator (strip refs, đính chính số, stale→volatile strip) · `Api/` KnowledgeRetriever (SQL structured + pgvector `<=>` / fallback doc-type+keyword) · ChatOrchestrator (9 bước, session+dedupe+window N=10+90d expiry) · `POST /v1/chat` + `preview-chat`. **Còn:** CP2 deploy + API RV (chờ VPS `vanan-ai` + secrets như CP1).
