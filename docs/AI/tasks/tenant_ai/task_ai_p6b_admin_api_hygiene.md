# Task AI-P6b — Admin API đủ SRS §6.3 + Hygiene Jobs (unblock P7)

> **Status:** ✅ APPROVED 2026-11-07 — implementing
> **Gate:** P1-G5 — SRS v1.1 §6.3 · FR-1 · FR-8 · [G9][G10][G11][G16] · NFR-7
> **Phụ thuộc:** P3 (chat pipeline + entities) · **BLOCKER cho P7** (bước 3 cần PUT profile + publish)
> **Nguồn gap:** `docs/AI/plans/tenant-ai-srs-compliance-audit.md` §3.1, §3.4–3.7

## Objective

Đóng các gap P1 còn lại trước CP3/P7:
1. Admin API đủ §6.3 — hiện `CreateDraft`/`MarkPublished` chỉ chạy trong test, production không có cách nào tạo/publish `TenantAiConfig` (profile GET đang là stub `NotImplemented` tại `Program.cs:377`).
2. Hygiene jobs: retention cleanup [G10], tenant purge [NFR-7], manual sync [G9], drift check [G16].

## Phạm vi

- **Làm:** 7 endpoint admin mới/thay stub trong `vanan-ai` + 1 BackgroundService cleanup + 1 endpoint purge + manual sync endpoint ShopERP + drift check trong reconcile + ops-script functions + tests.
- **Không làm:** UI admin (P2/`task_ai_p8`) · eval-gate trước publish (SRS ghi P2 — ghi TODO hook) · external knowledge (P3) · NATS sync (O2).

## Deliverables A — Admin API (`vanan-ai`, `Program.cs` + service mới `TenantAdminService`)

Tất cả endpoint: `.RequireScope(ScopeNames.Admin).RequireTenantContext()` + route `{tenantId}` phải `== TenantOf(ctx)` (pattern `preview-chat` tại `Program.cs:180`) → mismatch = 403.

| Endpoint | Spec |
|---|---|
| `PUT /v1/admin/tenants/{t}/profile` | Body `{aiName, personaJson?, businessProfileJson?}` (validate JSON parse được, `aiName` ≤128 ký tự non-empty) → **tạo Draft version mới** (`version = max(version)+1`, append-only — KHÔNG update in-place) → trả `{version, status:"draft"}` |
| `GET /v1/admin/tenants/{t}/profile` | Thay stub `NotImplemented` (`Program.cs:377`) → trả `{draft:{version,aiName,persona,businessProfile}?, published:{version,aiName,publishedAtUtc,publishedBy}?}` — null khi chưa có |
| `POST /v1/admin/tenants/{t}/publish` | Lấy Draft version mới nhất → `MarkPublished(CallerOf(ctx))` + `MarkArchived()` mọi Published cũ → trả `{version, status:"published", publishedAtUtc}` · không có Draft → 409 `no_draft` |
| `POST /v1/admin/tenants/{t}/rollback` | Body `{version}` → tạo Draft mới copy nội dung version đó rồi publish (giữ append-only [G11] — rollback = version mới, không resurrect row cũ) · version không tồn tại → 404 |
| `GET/PUT /v1/admin/tenants/{t}/policy` | `AiPolicy` per (tenant, configVersion mới nhất): GET trả `intentScopesJson/externalAllowJson/refusedTopicsJson` (default rỗng khi chưa có row); PUT upsert row gắn configVersion hiện hành · externalAllow chấp nhận nhưng **luôn rỗng v1** (P3 chưa build — reject non-empty với 400 `external_p3`) |
| `GET /v1/admin/tenants/{t}/conversations` | Paged `{page=1,size=20≤100}` → sessions kèm message preview: `{sessionId, channel, lastActiveAtUtc, messageCount, refusedCount, degradedCount}` + `?sessionId=` drill → messages `{role,content,intent,confidence,refused,createdAtUtc}` · order `last_active_at desc` · chỉ đọc, tenant-filtered |
| `GET /v1/admin/tenants/{t}/usage` | Aggregate từ `conversation_messages` `{days=30}`: `{messages, refused, degraded, promptTokens, completionTokens}` per ngày + tổng · (metering summary proxy defer — ghi TODO P2) |

**Contracts mới** (`VanAn.TenantAi.Contracts`): `PutProfileRequest/ProfileResponse` · `PublishResponse/RollbackRequest` · `PutPolicyRequest/PolicyResponse` · `ConversationListResponse/ConversationDetailResponse` · `UsageResponse`.

**Logic service:** `src/VanAn.TenantAi.Api/Services/TenantAdminService.cs` — giữ Program.cs mỏng (pattern `KnowledgeIngestService`).

## Deliverables B — Hygiene (`vanan-ai`)

| Thành phần | Spec |
|---|---|
| `RetentionCleanupService` | `BackgroundService` — chạy daily (config `Retention:CleanupIntervalHours` default 24, startup delay 5ph): `DELETE conversation_messages WHERE expires_at_utc < now` → sau đó xóa `conversation_sessions` không còn message nào chưa hết hạn · log `{tenant, deletedMessages, deletedSessions}` per pass · lỗi swallow + log warn (pattern `MeteringIngestQueue`) |
| `DELETE /v1/admin/tenants/{t}` | **Purge [NFR-7]** — xóa toàn bộ data tenant: `tenant_ai_configs` · `ai_policies` · `knowledge_usage_permissions` (tenant rows) · `product_knowledge` · `knowledge_documents` + `knowledge_chunks` (cascade) · `conversation_sessions`/`messages` · `ingest_log` · `intent_routes` tenant-scoped · trong 1 transaction · trả `{deleted:{...counts}}` · **yêu cầu header `X-Confirm-Purge: <tenantId>`** chống gọi nhầm |

## Deliverables C — ShopERP + drift

| Thành phần | Spec |
|---|---|
| Manual sync `POST /api/tenant-ai/sync` | Controller mới `TenantAiController` ShopERP: `[Authorize]` + resolve tenant caller (pattern ProductsController) → `ITenantAiKnowledgeSync.SyncNowAsync(tenantId)` → `{synced:N}` · flag off → `{synced:0, disabled:true}` · **không** SystemAdmin — owner/staff tenant đó được bấm (SRS G9 "manual Đồng bộ AI") |
| (Optional) nút trong `ShopFeatures.razor` | Nút "Đồng bộ AI ngay" cạnh toggle `Đồng bộ dữ liệu AI` → gọi endpoint trên + toast kết quả — chỉ làm nếu ≤30 dòng, không thì defer P8 UI |
| Drift check [G16] | Trong `TenantAiKnowledgeReconcileService.ReconcileAllAsync`: trước re-push, `client.GetKnowledgeStatusAsync` → so `productCount` vs `db.Products.Count(tenantId)` → lệch → `logger.LogWarning("TenantAi drift {Tenant}: local={L} remote={R}")` + đếm vào metric log · vẫn re-push (self-heal giữ nguyên) · status call fail → skip check, vẫn push |

## Ops tooling (`scripts/tenant-ai-ops.ps1` — bổ sung functions)

`Put-AiProfile` · `Publish-AiConfig` · `Rollback-AiConfig -Version n` · `Get-AiProfile` · `Get-AiPolicy`/`Put-AiPolicy` · `Get-AiConversations [-SessionId]` · `Get-AiUsage [-Days]` · `Remove-AiTenant -Confirm` — tất cả ký ctx như functions hiện có, caller scope `admin`.

## Tests bắt buộc (`VanAn.TenantAi.Tests` + ShopERP.Tests)

- [ ] Profile: PUT → draft v1 · PUT lần 2 → draft v2 (append-only, v1 không đổi) · publish → draft→published + old published→archived · rollback v1 → published mới copy nội dung v1 · publish khi không draft → 409
- [ ] GET profile: trả draft+published đúng · tenant chưa config → nulls
- [ ] Policy: GET default rỗng · PUT upsert · `externalAllow` non-empty → 400
- [ ] Conversations: paged + session drill · **cross-tenant route t≠ctx → 403** (T1 admin path)
- [ ] Usage: aggregate đúng prompt/completion tokens, refused, degraded
- [ ] Cleanup: insert expired + valid messages → run pass → expired xóa, valid giữ · orphan session xóa · session còn message sống → giữ
- [ ] Purge: seed đủ bảng tenant A + ít data tenant B → purge A (kèm `X-Confirm-Purge`) → A trống, B nguyên · thiếu confirm header → 400
- [ ] Widget-config sau publish: greeting/suggestions/aiName từ persona published mới (regression `Program.cs:250-260`)
- [ ] ShopERP: `POST /api/tenant-ai/sync` flag on → SyncNowAsync gọi 1 lần · flag off → `{disabled:true}` zero HTTP · unauthorized → 401/403
- [ ] Drift: fake status count lệch → warning log + re-push vẫn chạy
- [ ] `dotnet test VanAn.TenantAi.Tests` xanh + ShopERP.Tests không regression + `dotnet build VanAn.sln` 0 err (ShopERP đụng main sln)

## Acceptance

- [ ] Tests xanh · P7 bước 3 chạy được end-to-end bằng ops script: `Put-AiProfile` → `preview-chat` → `Publish-AiConfig` → widget-config trả persona mới
- [ ] Không còn `NotImplemented` stub trong `Program.cs`
- [ ] Deviation ghi vào card: rollback-as-new-version · usage chưa proxy metering (P2) · externalAllow reject non-empty (P3)
- [ ] Push theo CP3 policy (đụng ShopERP → guard-check + build VanAn.sln + ShopERP.Tests trước push)

## Delivered

_(điền sau implement — pattern các card trước)_
