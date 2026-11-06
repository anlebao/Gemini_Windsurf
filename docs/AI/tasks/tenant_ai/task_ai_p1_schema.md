# Task AI-P1 — Domain + EF Schema + pgvector

> **Status:** ✅ APPROVED 2026-11-06
> **Gate:** P1-G2 — SRS v1.1 §7 · [G2][G13] · local build+test only
> **Phụ thuộc:** P0

## Objective

Đầy đủ data model `vanan-ai` trên PostgreSQL + pgvector. Không API logic — chỉ entities, EF config, migrations.

## Entities (SRS §7 — `VanAn.TenantAi.Domain`, pure)

| Entity | Điểm bắt buộc |
|---|---|
| `TenantAiConfig` | versioned: `status(draft/published/archived)`, `published_at`, append-only per version |
| `AiPolicy` | `config_version` FK, `intent_scopes_json`, `external_allow_json` (P3), `refused_topics_json` |
| `KnowledgeUsagePermission` | `tenant_id` nullable (=system default), `intent`, `classification`, `channel`, `permission` |
| `ProductKnowledge` | `product_id`, name/desc/price/calories/ingredients_json/**allergens_json**/available/category, **`source_version`, `synced_at`** [G16] |
| `KnowledgeDocument` | `type`, `classification` (Public/Internal/Confidential/Restricted), **`approval_status(pending_review/approved/rejected)` + `approved_by/at`** [G14], `source_ref` |
| `KnowledgeChunk` | `document_id`, `chunk_index`, `content`, `embedding` — **Pgvector `Vector`** type, `token_count` |
| `ConversationSession` | `session_id` unique per tenant, `channel`, `meta_json` |
| `ConversationMessage` | role/content/intent/refs/sources/confidence/refused/**degraded/prompt_tokens/completion_tokens**, `expires_at` [G10] |
| `IngestLog` | `(tenant_id, kind, idempotency_key)` unique — dedupe ingest |
| `IntentRoute` | `tenant_id` null=system default, intent, allowed_source_flags, requires_facts, refuse_template |

## EF / DB requirements

- `Npgsql.EntityFrameworkCore.PostgreSQL` + `Pgvector.EntityFrameworkCore`
- `HasPostgresExtension("vector")` trong model
- Migrations EF thật (khác metering EnsureCreated — PG có migrations ngay từ đầu)
- Index: mọi bảng tenant-scoped `tenant_id` đầu tiên · `knowledge_chunks`: HNSW `vector_cosine_ops` (migration raw SQL `CREATE INDEX … USING hnsw`) · `product_knowledge (tenant_id, product_id)` unique · `conversation_messages (tenant_id, session_id, created_at)` · `expires_at` index cho cleanup
- `embedding vector(N)` — N từ config `TenantAiLlm:EmbeddingDimensions` (default 768 theo `text-embedding-004`); migration đọc config hoặc fix 768 + lưu ý đổi = migration mới
- Single-identity [G13]: PK `Id` Guid duy nhất, không business-key song song

## Tests bắt buộc

- [ ] Migration apply sạch trên PG16+pgvector (docker compose test hoặc Testcontainers)
- [ ] `CREATE EXTENSION vector` + HNSW index tồn tại sau migrate
- [ ] Insert chunk + query `ORDER BY embedding <=> @v LIMIT k` trả đúng thứ tự cosine
- [ ] Query vector có `WHERE tenant_id` → verify plan/result không cross-tenant (seed 2 tenant, assert 0 leak)
- [ ] Unique indexes: ingest_log dedupe, product_knowledge (tenant,product) — replay = update không dup

## Acceptance

- [ ] `dotnet build` + migration test xanh · schema review khớp SRS §7 từng bảng
