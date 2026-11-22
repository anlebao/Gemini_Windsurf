# W0 AUDIT: KNOWLEDGE PIPELINE (Q4)

> Date: 2026-11-22 · Câu hỏi: Auto-Builder (W3) build trên pipeline nào, hỗ trợ input gì?

## Evidence

### Ingest inputs hiện có (verified)

| Nguồn | Contract | Trạng thái |
|---|---|---|
| Structured products | `ProductsBatchRequest(FullSync, SourceVersion, Items[])` — ProductId/Name/Desc/Price/Calories/Ingredients/Allergens/Available/Category | ✅ ShopERP sync sang |
| Text documents | `DocumentIngestRequest(Type, Title, Content, Classification, SourceRef?)` | ✅ Content là **text thuần** |
| Approval gate | pending_review → approve/reject + classification override (AI-P11) | ✅ |
| Sanitizer | `IngestSanitizer` trước chunk/embedding | ✅ |
| Usage permissions | `KnowledgeUsagePermissions` per intent/classification/channel | ✅ |
| Chat→knowledge | AI-P11 promote answer → doc với provenance | ✅ |
| Status/stats | `KnowledgeStatusResponse` (pending/approved/chunks/products/stale) | ✅ |

### KHÔNG có

- File upload (PDF/Excel/ảnh) trực tiếp vào ingest — `Content` là string.
- Website crawl/fetch.
- OCR/image parsing.
- Auto-extraction (LLM parse menu/giá từ text thô → structured) — chưa có service này.

## Hàm ý cho W3 Auto-Builder

1. **Ingest engine không cần viết lại** — đúng như SRS. Nhưng Auto-Builder cần lớp **pre-ingest extraction**: nhận input thô (text paste, ảnh, file, URL) → extract thành text/structured → feed `DocumentIngestRequest`/`ProductsBatchRequest`.
2. Input khả thi W3 (theo độ khó tăng):
   - **Text/FAQ paste**: trivial — wrap vào DocumentIngestRequest.
   - **Menu/bảng giá text** (copy từ Zalo/web): LLM extract → products/docs.
   - **Ảnh menu**: cần OCR/vision — Gemini vision API có sẵn cùng vendor; effort trung bình.
   - **PDF/Excel**: cần parser (PdfPig/EPPlus…) → text → ingest; effort trung bình.
   - **Website URL crawl**: fetch+extract text (verify SSRF rules); effort trung bình-cao.
3. **Confidence check** tận dụng được: `KnowledgeStatusResponse` (chunks embedded, stale hours) + test-question generation là layer mới gọi chat API.
4. **ProductsBatch** đã là structured path tốt nhất cho menu/giá — auto-builder nên ưu tiên map sang products khi nhận diện được item+giá.

## Đề xuất phạm vi W3 (cập nhật task card)

- W3a (core): text paste + manual structured input + LLM-assisted extraction từ text → documents/products → confirm UI.
- W3b: ảnh menu qua Gemini vision → extract.
- W3c: PDF/Excel parser.
- W3d: website crawl opt-in.

Mỗi mức là increment riêng — không block W3a bởi W3b-d.

## Cần PO quyết

- [ ] W3 scope: chỉ text/manual trước, hay gồm ảnh/PDF ngay?
- [ ] Extraction dùng Gemini (cùng vendor TenantAI) — OK?
