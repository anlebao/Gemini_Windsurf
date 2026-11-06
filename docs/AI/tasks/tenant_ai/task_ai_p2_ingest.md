# Task AI-P2 — Knowledge Ingest + Approval Gate + Embedding Pipeline

> **Status:** ✅ APPROVED 2026-11-06
> **Gate:** P1-G2 — SRS v1.1 [G5][G9][G14][G16] · FR-6 · local build+test only
> **Phụ thuộc:** P1 schema · P0b auth (scope `ingest`)

## Objective

Đường ống đưa knowledge vào `vanan-ai`: product sync (auto-trusted), documents (approval gate), chunk+embed pipeline. ShopERP push client + operator tooling.

## Deliverables — vanan-ai side

| Thành phần | Spec |
|---|---|
| `POST /v1/knowledge/products:batch` | `{fullSync, sourceVersion, items[]}` — upsert per `product_id`; `fullSync=true` → xóa món không còn trong batch; set `synced_at=now`, `source_version` từ caller; `Idempotency-Key` → `ingest_log` dedupe |
| `DELETE /v1/knowledge/products/{id}` | Gỡ món (product xóa/ngừng bán) |
| `POST /v1/knowledge/documents` | `{type,title,content,classification}` → lưu `approval_status=pending_review` — **KHÔNG chunk/embed tới khi approve** [G14] |
| `POST /v1/admin/knowledge/{id}/approve` `/reject` | Approve → trigger chunk+embed; reject → lý do bắt buộc; `approved_by` = callerId |
| `GET /v1/admin/knowledge/pending` + `GET /v1/knowledge/status` | Pending list · status (doc/chunk count, last sync + source_version, stale flag [G16]) |
| `ChunkingService` | ≤~500 tokens/chunk, overlap ~50 — tokenizer đơn giản (word-count heuristic OK cho vi-VN MVP) |
| `IEmbeddingProvider` + `GeminiEmbeddingService` | [G17] interface + impl: `POST {endpoint}/models/{model}:embedContent`, model `text-embedding-004` config-driven, timeout 30s, retry backoff ×2, ApiKey rỗng = pipeline tắt (doc vẫn approve nhưng `embedding=null` + status flag `embedding_pending`) |
| `IngestSanitizer` | Strip instruction-like patterns khỏi doc content trước chunk (T3): remove lines khớp `ignore|instruction|system prompt|forget`… — đánh dấu `sanitized=true`, không xóa âm thầm |

## Deliverables — ShopERP side (main sln, additive)

| Thành phần | Spec |
|---|---|
| `Services/TenantAiApiClient.cs` | Pattern `MeteringApiClient`: timeout 5s admin / 30s ingest · circuit breaker 5-fail/60s · self-disable khi thiếu `TenantAi:BaseUrl/CallerId/CallerKey` · `BaseAddress` trailing `/` · ký `X-Tenant-Ctx` bằng `TenantAi__CtxSigningSecret` |
| `Services/TenantAiKnowledgeSyncService.cs` | Push product knowledge: map `Product` → curated DTO `{productId,name,description,price,calories,ingredients,allergens,available,categoryName}` · trigger: product CUD hook + manual `SyncNowAsync()` + hosted reconcile nightly (count/hash drift → re-push) — flag `TenantAiEnabled` OFF = no-op hoàn toàn |
| `scripts/` operator tooling | `tenant-ai-ops.ps1` hoặc curl docs trong card: approve/reject/pending/status/put-profile — P1 operator dùng script, KHÔNG cần UI |

## Tests bắt buộc (gate evidence)

- [ ] products:batch upsert + fullSync remove + idempotent replay (cùng key → không double, `ingest_log` 1 row)
- [ ] Document ingest → `pending_review` → **retrieval query trả 0** trước approve (T11) → approve → chunk+embed → retrievable
- [ ] Reject → không bao giờ retrievable + lý do lưu đủ
- [ ] Classification `confidential`/`internal` doc → ingest OK nhưng public-channel retrieval KHÔNG bao giờ đọc (T5 DB-level)
- [ ] Sanitizer: doc chứa "IGNORE ALL RULES" → chunk có `sanitized` flag, injection line bị strip
- [ ] Embedding: fake `IEmbeddingProvider` deterministic cho tests; Gemini impl verify thật ở P7 (model verify)
- [ ] ShopERP sync client: mapping đúng field · flag OFF → zero HTTP call · service down → fail-open log warn (sync không chặn bán hàng)
- [ ] `synced_at`/`source_version` ghi đúng; stale check helper [G16]

## Acceptance

- [ ] Tests xanh · ShopERP build không vỡ (additive) · operator script chạy được end-to-end trên local compose
