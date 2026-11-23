# TASK W3c-multi: MULTI-GROUP KNOWLEDGE EXTRACTION

> Wave: W3 (Auto-Builder) · Mode: IMPLEMENT · TDD bắt buộc
> Master plan: `docs/AI/plans/w3c-w3d-extract-voice-master-plan.md` (D1–D4)
> Depends on: W3a extractor (deployed `628eb0e3`), W3c URL extract (deployed `e4565009`)
> Task card gốc: `task_w3_knowledge_onboarding.md`

## Mục tiêu

Owner bấm 1 lần "Trích xuất" trên text/URL → nhận preview **4 nhóm riêng** (menu · about · policy · promotion) → confirm → hệ thống tạo **N knowledge docs pending-review** (mỗi nhóm 1 doc, `sourceRef` riêng). Approval gate giữ nguyên — KHÔNG auto-publish.

## Hiện trạng (đã verify)

- `KnowledgeExtractor.ExtractAsync` (vanan-ai): 1 call → `KnowledgeExtractResponse{Summary, Items[{kind,title,content}]}`, kinds = product|service|faq|policy|hours|other, cap 50.
- `POST /v1/admin/knowledge/extract` — admin scope, ≤20k chars.
- `GeminiChatClient.GenerateAsync(prompt, schemaHint)` — schemaHint giờ gửi `responseSchema` thật (fix `586e9247`).
- Ingest types: `faq | story | policy | menu-note | recipe` (`KnowledgeIngestService.AllowedTypes`).
- ShopERP: `TenantAiApiClient.ExtractKnowledgeAsync` + card "Trích xuất tự động" trên `TenantAiKnowledge.razor` (paste text + ảnh OCR + URL deep-crawl).

## Quyết định (theo plan D1–D4)

- **D1**: vanan-ai fan-out — endpoint mới `POST /v1/admin/knowledge/extract-groups`, vanan-ai chạy song song 1 call/nhóm.
- **D2**: 4 nhóm `menu|about|policy|promotion`, mỗi nhóm prompt + schema riêng.
- **D3**: doc mapping `menu→menu-note`, `about→story`, `policy→policy`, `promotion→policy` (title prefix "Khuyến mãi:"). Không thêm ingest type.
- **D4**: `sourceRef = "extract:{group}:{guid:N}"`.

## Scope IN

### vanan-ai
1. `KnowledgeGroupExtractor` (hoặc extend `KnowledgeExtractor`): method `ExtractGroupsAsync(request, groups, ct)` — `Task.WhenAll` 4 call, mỗi call dùng prompt chuyên biệt + schema `{summary, items[]}`; **per-group never-throw** (group fail → `{group, "", []}` + warning, không làm hỏng nhóm khác).
2. Contract: `KnowledgeExtractGroupResponse { Groups: List<{Key, Label, Summary, Items[], Warning?}> }` — file mới trong `VanAn.TenantAi.Contracts` (không sửa DTO cũ, backward compat).
3. Endpoint `POST /v1/admin/knowledge/extract-groups` — cùng auth/admin scope/20k cap như `/extract`.
4. Prompt rules giữ nguyên: chỉ JSON, kind giới hạn, không bịa, giữ nguyên tiếng Việt + giá.

### ShopERP
5. `TenantAiApiClient.ExtractKnowledgeGroupsAsync(tenantId, text, hint)` — timeout 60s (4 call song song, LLM ~2-8s/call).
6. `TenantAiKnowledge.razor`: nút "Trích xuất theo nhóm" cạnh extract đơn → preview **theo nhóm** (section per group: badge màu + items editable) → "Đưa vào hàng chờ duyệt" → loop `IngestDocumentAsync` per group non-empty với mapping D3 + `sourceRef` D4.
7. Giữ nguyên flow extract đơn (paste nhanh không cần nhóm) — 2 nút song song.

## Scope OUT

- Thêm ingest type `promotion` (chỉ làm nếu retrieval cần — increment riêng).
- Product auto-create từ menu extract (catalog path riêng, không trộn).
- Extract từ ảnh/PDF trực tiếp (OCR ảnh đã có; PDF = W3d sau).
- Headless browser / JS-rendered SPA (đã có warning hướng dẫn copy tay).

## TDD — tests viết TRƯỚC

vanan-ai `VanAn.TenantAi.Tests`:
- [ ] `ExtractGroupsAsync` trả 4 group keys đúng thứ tự kể cả khi 1 group LLM fail (fail-safe per group).
- [ ] Group `menu` parse items có kind product/service; group `policy` items kind policy/faq.
- [ ] Input blank → tất cả groups rỗng, không gọi LLM.
- [ ] `llm.Enabled=false` → mọi group trả warning "chưa sẵn sàng".
- [ ] Items cap 50/group; content cap 500 chars.
- [ ] Endpoint test: auth required, text >20k → 400/empty an toàn.

ShopERP `VanAn.Core.Tests`:
- [ ] Group→docType mapping đúng (menu→menu-note, about→story, policy→policy, promotion→policy).
- [ ] `sourceRef` format `extract:{group}:{guid}` unique per group.
- [ ] Confirm với 1 group rỗng → chỉ tạo docs cho group non-empty.

## RV sweep-first (sau deploy)

1. Container `vanan-ai-1` + `vanan-shoperp-1` image = commit mới, healthy.
2. vanan-ai log: `POST /v1/admin/knowledge/extract-groups` → 200, 4 group calls song song trong log.
3. Manual UI: Knowledge → card Trích xuất → paste menu+about+policy+promo mẫu → "Trích xuất theo nhóm" → preview 4 section → confirm → **4 docs pending** (đúng type từng nhóm).
4. Approve 1 doc → chat AI trên storefront hỏi về khuyến mãi/chính sách → trả lời grounded.
5. Regression: extract đơn (non-group) vẫn hoạt động.

## Acceptance / Gate

- [ ] 1 click → preview ≥2 nhóm non-empty trên fixture text hỗn hợp
- [ ] Mỗi group → doc pending riêng, đúng type, sourceRef dedupe (bấm lại không nhân đôi)
- [ ] LLM fail 1 group không chặn nhóm khác
- [ ] Không doc nào auto-publish
- [ ] Extract đơn cũ không regression
- [ ] Guard ALL PASSED + build 0 err trước push

## Risks

| Risk | Mitigate |
|---|---|
| 4× LLM cost/extract | Admin-triggered tần suất thấp; flash-lite; giữ cap 20k input |
| Group overlap (giá trong policy) | Prompt "chỉ lấy nội dung thuộc nhóm X, bỏ qua phần khác"; owner review-sửa |
| Timeout 4 call song song | `Task.WhenAll` + per-call timeout sẵn có (HttpClient 30s); endpoint timeout 60s |
