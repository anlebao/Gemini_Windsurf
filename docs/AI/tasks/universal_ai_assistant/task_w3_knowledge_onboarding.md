# TASK W3: ZERO-CODE KNOWLEDGE ONBOARDING (AUTO-BUILDER)

> Wave: W3 · Mode: IMPLEMENT (sau W1; song song được với W2)
> Master plan §W3 · SRS v2.0 §6 (KB-001→007)
> Depends on: W1 (assistant entity) + `KNOWLEDGE_PIPELINE_AUDIT.md` (W0)

## Sản phẩm giao được

> Owner không cần biết "RAG" hay "knowledge base": upload menu/ảnh/bảng giá/FAQ hoặc nhập website URL → hệ thống tự dựng knowledge profile → sinh test questions chỉ ra chỗ thiếu/mâu thuẫn → owner review-sửa-xác nhận → publish. **Onboarding <15 phút, self-serve.**

## Scope IN

### Auto-Builder pipeline
1. **Collect**: shop info cơ bản (tên, ngành, địa chỉ, SĐT) + ≥1 nguồn: text FAQ, ảnh menu, file (PDF/Excel nếu pipeline hỗ trợ), hoặc website URL.
2. **Extract**: parse → candidates (sản phẩm/dịch vụ/giá/giờ HC/chính sách/FAQ). Tái dùng `KnowledgeIngestService`/`IngestSanitizer`/chunking hiện có — KHÔNG tạo ingest engine mới.
3. **Profile**: structured knowledge profile per tenant.
4. **Confidence check**: sinh test questions; detect missing/conflict/stale (giá không rõ, chính sách mâu thuẫn, data cũ).
5. **Merchant confirm**: review UI plain-language — xem/sửa/xóa từng mục, confirm trước publish. Không auto-publish extracted content.
6. **Publish**: knowledge qua ingest pipeline chuẩn → usage-permission → assistant dùng được.

### Knowledge management UI
- Danh sách knowledge items plain-language; edit/delete từng mục.
- Readiness checklist: "Trợ lý chưa biết giá X — thêm để trả lời tốt hơn".
- Freshness: stale warning + gợi ý cập nhật khi menu/giá đổi.

### Safety
- Mọi extracted content qua `IngestSanitizer` + usage-permission trước public (KB-002).
- Không ingest private/confidential content vào public knowledge (SRS §9.3).
- Website crawl (nếu làm): opt-in, robots.txt respect, SSRF defenses (FR-029/030 pattern).

## Scope OUT

- Real-time sync với ShopERP catalog (future — catalog đã có product cards path riêng)
- Đa ngôn ngữ nâng cao, voice knowledge
- Auto-publish không confirm — tuyệt đối không

## Tasks

1. Audit ingest pipeline input types → quyết định nguồn hỗ trợ W3 (theo W0-F).
2. Extraction service: input adapters (text/image/file/URL) → normalized candidates.
3. Confidence/test-question generator.
4. Knowledge profile storage (theo Q1 host decision — có thể tái dùng TenantAI knowledge docs).
5. Review/confirm UI trong portal (UI.Platform).
6. Publish flow → ingest pipeline.
7. Freshness/stale detection cơ bản.
8. Tests: extraction quality trên fixture set, sanitizer pass, confirm bắt buộc, permission flags, E2E (upload→profile→confirm→chat trả lời đúng).

## Acceptance / Gate

- [ ] Owner hoàn thành knowledge setup <15 phút không cần support
- [ ] Assistant trả lời đúng trên test set do owner duyệt
- [ ] Không item nào public trước merchant confirm
- [ ] Extracted content qua sanitizer + permission đúng
- [ ] Missing/conflict được chỉ ra rõ trong checklist
- [ ] Stale data có warning
- [ ] Regression TenantAI ingest hiện có pass

## Risks

- Extraction quality kém (ảnh menu, PDF giá) → giới hạn nguồn W3 ở loại parse tốt nhất; còn lại fallback nhập tay.
- Owner confirm ẩu → vẫn tốt hơn không có knowledge; checklist giảm rủi ro.
- Ingest pipeline không nhận loại file cần → W0-F quyết; nếu thiếu, thêm adapter tối thiểu chứ không viết lại.
