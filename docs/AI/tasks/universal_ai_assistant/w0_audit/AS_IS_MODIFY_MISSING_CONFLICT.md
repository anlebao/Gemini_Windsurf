# W0-AUDIT: AS-IS / MODIFY / MISSING / CONFLICT — Universal AI Assistant

> Date: 2026-11-22 · Mode: ANALYZE · Evidence: direct code inspection
> Legend: ✅ production/implemented · 🔧 implemented cần modify · 🟡 partial · ❌ missing · ⚠️ conflict/cần quyết định

## 1. AI Core & Chat Pipeline

| Capability | Status | Evidence |
|---|---|---|
| TenantAI chat engine (intent/policy/retrieval/validation) | ✅ | `9_TenantAI/vanan-ai/src/VanAn.TenantAi.Ai/*` — IntentRouter, PolicyEngine, KnowledgeRetriever, AnswerValidator, PromptBuilder, UsagePermissionFilter |
| LLM provider | ✅ | `GeminiChatClient` + `GeminiEmbeddingService` (Google AI Studio endpoint, config `TenantAiLlm:*`) |
| Grounded chat API `/v1/chat` | ✅ | `VanAn.TenantAi.Api/Program.cs` — RequireScope(public-chat) + RequireTenantContext (signed HMAC ctx) |
| Widget config `/v1/config/widget` | 🟡 | Endpoint tồn tại; Gateway comment "501 stub lands in P5" — cần verify trạng thái thực tế trên prod |
| Conversation storage | ✅ | `ConversationSession` + `ConversationMessage` — SessionId **client-supplied**, keyed per tenant; dedupe `(tenant_id, message_id)`; `RetentionCleanupService` có sẵn |
| Anonymous session token | 🟡 | KHÔNG có server-issued session token — session chỉ là string client-generated (64 chars max). Đủ cho storefront, nhưng SRS muốn "short-lived scoped token" → MODIFY/NEW ở facade |
| ThreatSuite T1–T10 | ✅ | cross-tenant, injection, doc-injection, hallucination, confidential-leak, key-leak, freshness |
| AI-P11 chat→knowledge | ✅ | promote chat answers thành knowledge docs (provenance `chat:{convId}:{msgId}`) |

## 2. Public Facade (Gateway)

| Capability | Status | Evidence |
|---|---|---|
| Anonymous public chat | ✅ | `PublicTenantAiController` `POST /api/public/tenant-ai/chat` — AllowAnonymous + `tenant-ai-public` rate limit |
| Server-side tenant resolution | ✅ | `ResolveStoreTenantAsync`: KhachLinkInstance host→tenant wins; client tenantId validated against Active tenant, never signed raw |
| Session abuse cap | ✅ | `TenantAi:SessionCap` = 20 msg/session/hour (`TryAcquireSessionSlot`) |
| Idempotency | ✅ | messageId end-to-end → upstream dedupe |
| Semantic search | ✅ | `GET .../search` — deterministic session per (tenant,query), no double metering |
| Graceful degradation | ✅ | AI down → `aiEnabled:false` / 503; storefront không phụ thuộc AI (NFR-3) |
| Handoff chip UI | ✅ | `AiChatResult.Refused` → "Hỏi chủ quán trực tiếp" (AI-P8) |
| Resolution by slug/domain (cho link/widget ngoài) | ❌ | Hiện chỉ resolve theo storefront host hoặc tenantId validated — **chưa có** assistant-link slug → assistant resolution |
| Public widget config endpoint (publicWidgetKey) | ❌ | Chưa có — hiện widget-config đi qua tenantId + feature flag |
| Domain verification | ❌ | Chưa có gì |

## 3. Assistant Management

| Capability | Status | Evidence |
|---|---|---|
| Per-tenant AI enable flag | ✅ | `ShopFeatureSettingsEntity.AIChat_Enabled` (CoreHub PG), sync ShopERP↔Gateway qua outbox/NATS `vanan.shoperp.shop.feature.settings.changed` |
| Ops flag TenantAiEnabled | ✅ | SystemSetting row, gate knowledge sync (ShopFeatures.razor) |
| TenantAI admin console | ✅ | ShopERP `/admin/tenant-ai/*`: Hub, Knowledge, Policy, Profile, Conversations, Usage |
| Assistant entity (name/persona/greeting/publish states/config version) | ❌ | Chưa có — hiện chỉ có boolean flag + TenantAiProfile (cần check nội dung profile) |
| Publish states/versioning/rollback | ❌ | |
| AI Link/QR | ❌ | |
| Dashboard usage/quota cho owner | 🟡 | `TenantAiUsage.razor` + Metering usage events có; cần gộp view theo assistant |

## 4. Metering & Cost

| Capability | Status | Evidence |
|---|---|---|
| Rate card engine | ✅ | `RateCardVersion` append-only, scope Global/TenantType/TenantStatus/Tenant, enforcement MeterOnly/Deny |
| Service code `ai.chat.message` | ✅ code / 🟡 seed? | `MeteringServiceCodes.ChatMessage` — Deny-enforced; `ai.chat.degraded` MeterOnly; `ai.embedding.batch` MeterOnly. **Cần verify rate card đã seed trên prod DB** |
| QuotaGate | ✅ | `QuotaGate.cs` — CheckBalanceAsync per tenant, cache 30s; deny/fail → structured-only degradation policy |
| Ingest queue | ✅ | `MeteringIngestQueue` hosted service, idempotency key, retry+drop logging |
| Cost attribution chiều | 🟡 | Usage events có tenantId+serviceCode+qty+metadata; **chưa verify metadata có channel/session/assistant** — W1 cần bổ sung attribution fields |
| Per-assistant budget/hard-cap | ❌ | Chưa có — chỉ có wallet balance + rate card |
| Platform circuit breaker | ❌ | MeteringClient có 5-fail/60s circuit (call-level), chưa có cost-anomaly breaker |

## 5. Widget/UI

| Capability | Status | Evidence |
|---|---|---|
| Blazor chat widget | ✅ | `UI.Platform/Components/Ai/AiChatWidget.razor` — backdrop, scroll-lock, unread badge, voice hooks |
| `IAiChatClient` contract | ✅ | config/chat/history/search; null-on-error fail-safe |
| Product cards in answer | ✅ | `AiProductCardItem` |
| External JS SDK (embed site ngoài) | ❌ | |
| Hosted standalone assistant page | ❌ | Cần host app quyết định (xem ASSISTANT_HOST_DECISION) |

## 6. Knowledge

| Capability | Status | Evidence |
|---|---|---|
| Document ingest | ✅ | `DocumentIngestRequest(Type,Title,Content,Classification,SourceRef)` — **text-only content** |
| Product knowledge sync | ✅ | `ProductsBatchRequest` fullSync structured items |
| Approval workflow | ✅ | pending_review → approve/reject + classification; `KnowledgeStatusResponse` stats |
| Sanitizer | ✅ | `IngestSanitizer` |
| File/image/PDF ingest | ❌ | Ingest nhận text content — auto-builder phải extract text trước (W3 constraint) |
| Website crawl | ❌ | |
| Auto-builder (extract→profile→confirm) | ❌ | UX/orchestration layer mới trên pipeline có sẵn |

## 7. Voice

| Capability | Status | Evidence |
|---|---|---|
| Human-to-human voice/video call | ✅ | `10_Voice` + UI.Platform realtime — video MVP deployed 22/11 |
| AI voice (STT/LLM/TTS) | ❌ | Confirmed absent — xem VOICE_PROVIDER_AUDIT |

## 8. Distribution surfaces

| Capability | Status |
|---|---|
| Browser Extension | ❌ |
| Android app | ❌ (có `4_MobileApps` — cần check scope, nhưng không phải floating assistant) |
| Zalo OA / FB Messenger / TikTok API | ❌ |

## 9. CONFLICTS / cần quyết định

| # | Conflict | Hướng xử lý |
|---|---|---|
| C1 | `AIChat_Enabled` boolean flag vs Assistant entity mới | Assistant entity phải **bao gồm/thay thế** flag theo cách backward-compatible: flag = derived từ assistant.Status==Published (xem HOST_DECISION) |
| C2 | Session model: client string vs SRS "short-lived scoped token" | Facade cấp session token ký (như pattern signed ctx); TenantAI vẫn nhận sessionId string — token chứa sessionId+tenant+expiry |
| C3 | Widget-config upstream "501 stub" comment vs endpoint tồn tại | Verify prod; nếu stub → W1 implement config thật theo assistant entity |
| C4 | `TenantAiProfile` entity đã có — trùng Assistant? | Check nội dung; nếu profile = business info cho prompt thì Assistant = surface/lifecycle layer phía trên, không trùng |
