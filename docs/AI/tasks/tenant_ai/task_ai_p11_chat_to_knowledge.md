# Task AI-P11 — Promote human chat answer → knowledge draft ("Đề xuất vào tri thức AI")

> **Status:** 🔨 IMPLEMENTED 2026-11-13 — commit `926e4c85` · build 0 err · TenantAi.Tests 15/15 · ShopERP.Tests TenantAi 36/36 · Core.Tests TenantAi+pairing 54/54 · guard-check đang chạy · **chưa deploy/RV**
> **Gate:** main sln changes — additive · UI.Platform component extension · **Domain change nhỏ (cần confirm)**
> **User decisions (2026-11-13):** chỉ Owner được bấm đề xuất · owner tự chọn classification **khi approve** · nội dung chat giữ **nguyên văn**
> **Phụ thuộc:** AI-P8 (human handoff trong AiChatWidget) · AI-P9 (ShopInbox tab review) · AI-P2 (ingest pipeline + approval gate)

## Objective

Khi Owner trả lời khách trong ShopInbox, bấm 1 nút trên tin nhắn đó → tạo knowledge document `pending_review` chứa nguyên văn cặp hỏi/đáp. Owner duyệt + chọn classification sau ở trang TenantAi Knowledge. **Không thay đổi** approval gate [G14], sanitizer, hay permission filter.

## Hiện trạng (verified 2026-11-13)

| Thành phần | File | Ghi chú |
|---|---|---|
| Chat panel | `UI.Platform/Components/Realtime/RealtimeChatPanel.razor` | Shared component; `_messages: List<RealtimeChatMessage>` private; `isOwn = msg.SenderId == CurrentUserId`; history load oldest-first qua `IRealtimeChatClient.GetHistoryAsync(SubjectType, SubjectId, ..., PeerId)` |
| Shop inbox | `5_WebApps/ShopERP/Components/Pages/Community/ShopInbox.razor` | `[Authorize(Roles="Owner")]`; detail pane mount `RealtimeChatPanel SubjectType="Shop" PeerId=_selectedPeerId (=conv.CustomerId) StaffToken=_staffToken`; flag AI tab = `TenantAiEnabled` (`_aiTabVisible`) |
| Thread model | `2_Gateway/Controllers/RealtimeController.cs` | Shop threads **per-customer**: `GET /api/realtime/conversations/Shop/{tenantId}?peerId={customerId}`; msg DTO `{id, senderId, content, sentAt, isRead}` — **không có sender role** |
| Ingest client | `5_WebApps/ShopERP/Services/TenantAiApiClient.cs` | `IngestDocumentAsync(tenantId, type, title, content, classification)` ĐÃ CÓ (line ~157); `ApproveDocumentAsync(tenantId, docId)` chưa nhận classification |
| TenantAI ingest | `9_TenantAI/.../Services/KnowledgeIngestService.cs` | `AddDocumentAsync` → sanitize → `pending_review`; `ApproveDocumentAsync(tenantId, docId, callerId, ct)` → chunk+embed; `KnowledgeDocument.SourceRef` tồn tại nhưng `DocumentIngestRequest` chưa expose |
| Domain | `9_TenantAI/.../Domain/KnowledgeDocument.cs` | `Classification { get; private set; }` chỉ gán lúc `Create`; `Approve(approvedBy)` không đổi classification |
| Approve endpoint | `9_TenantAI/.../Api/Program.cs:354` | `POST /v1/admin/knowledge/{id}/approve` — hiện không nhận body |
| Admin UI | `5_WebApps/ShopERP/Components/Pages/Admin/TenantAi/TenantAiKnowledge.razor` | Trang review pending docs (approve/reject) |

## Deliverables

### 1. UI.Platform — `RealtimeChatPanel.razor` (additive, opt-in)

```csharp
/// <summary>AI-P11: khachAnswer→knowledge suggestion. Chỉ render khi host opt-in.</summary>
public sealed record KnowledgeSuggestion(
    Guid ConversationId, Guid MessageId, string Question, string Answer);

[Parameter] public bool KnowledgeSuggestEnabled { get; set; }   // default false
[Parameter] public EventCallback<KnowledgeSuggestion> OnKnowledgeSuggest { get; set; }
```

- Khi `KnowledgeSuggestEnabled && isOwn` (msg.SenderId == CurrentUserId): render nút nhỏ dưới bubble — icon `bi-robot` (hoặc `bi-lightbulb`), `title="Đề xuất vào tri thức AI"`, `aria-label` tương đương.
- **Pairing:** scan `_messages` ngược từ index tin owner → tin đầu tiên `SenderId != CurrentUserId` = customer question. Không tìm thấy → disable nút + tooltip "Không tìm thấy câu hỏi của khách phía trước".
- Click → `OnKnowledgeSuggest.InvokeAsync(new KnowledgeSuggestion(_conversationId, msg.Id, question, msg.Content))`.
- `_messages` phải chronological (oldest→newest) — verify khi implement; history endpoint trả oldest-first, message mới append cuối.
- **Giới hạn v1 (ghi comment):** "tin shop" = `SenderId == CurrentUserId`. Tin của staff khác (nếu shop có nhiều staff) bị coi như tin khách trong pairing. Chấp nhận vì ShopInbox là Owner-only; nâng cấp sau bằng sender-role trong DTO nếu cần.
- `KnowledgeSuggestEnabled=false` (default) → **zero behavior change** cho Order chat, KhachLink, mọi usage hiện có.

### 2. ShopERP — `ShopInbox.razor`

- Truyền vào panel: `KnowledgeSuggestEnabled="@_aiTabVisible"` + `OnKnowledgeSuggest="HandleKnowledgeSuggest"`.
- `HandleKnowledgeSuggest(KnowledgeSuggestion s)`:
  1. Mở confirm dialog (VanAn UI components — KHÔNG raw HTML): preview nguyên văn 2 chiều:
     - `Khách hỏi:` + `{s.Question}`
     - `Chủ quán đáp:` + `{s.Answer}`
  2. Title input auto = 80 ký tự đầu của Question, **owner edit được** (title không phải nội dung tri thức).
  3. Submit → `ITenantAiClient.IngestDocumentAsync(tenantId, "faq", title, content, "internal", sourceRef)` với:
     ```
     content = $"Khách hỏi:\n{s.Question}\n\nChủ quán đáp:\n{s.Answer}"
     sourceRef = $"chat:{s.ConversationId}:{s.MessageId}"
     ```
  4. Success → toast "Đã gửi vào hàng chờ duyệt tri thức AI" + link tới trang TenantAi Knowledge (verify route thật, dự kiến `/admin/tenant-ai/knowledge` hoặc tương đương). Fail → alert nhẹ; **không ảnh hưởng chat** (fail-safe pattern sẵn có của TenantAiApiClient).
  5. Disable submit sau khi gọi (chống double-submit); server-side dedupe bằng SourceRef là lớp thứ 2.
- Flag OFF / TenantAi client disabled → nút không render (đã gate ở panel param) → zero HTTP.

### 3. TenantAI — `sourceRef` + approve-time classification

**(a) Contracts (`KnowledgeDtos.cs`):**
- `DocumentIngestRequest` + `string? SourceRef` (optional, backward compatible).
- `ApproveDocumentRequest(string? Classification)` — record mới cho approve body.

**(b) Domain (`KnowledgeDocument.cs`) — ⚠️ cần user confirm (Domain modification):**
```csharp
/// <summary>AI-P11: owner chọn visibility lúc approve. Chỉ khi PendingReview;
/// Restricted fail-closed [G5]; sau approve classification immutable.</summary>
public void SetClassification(DocumentClassification classification)
{
    if (ApprovalStatus != ApprovalStatus.PendingReview)
        throw new InvalidOperationException("Classification chỉ đổi được khi pending_review.");
    if (classification == DocumentClassification.Restricted)
        throw new ArgumentException("Restricted không được phép.", nameof(classification));
    Classification = classification;
    UpdatedAtUtc = DateTime.UtcNow;
}
```
Additive — không đổi invariant/hành vi flow hiện có (approve không truyền classification → giữ nguyên).

**(c) `KnowledgeIngestService`:**
- `AddDocumentAsync`: truyền `req.SourceRef` vào `KnowledgeDocument.Create(sourceRef: ...)`. Nếu `SourceRef != null` và đã tồn tại doc cùng `(TenantId, SourceRef)` → trả doc hiện có (idempotent-ish, không tạo trùng; check-then-insert đủ cho tần suất owner-bấm-tay — KHÔNG thêm unique index/migration ở v1).
- `ApproveDocumentAsync(tenantId, docId, callerId, string? classification, ct)`:
  - `classification == null` → giữ nguyên (backward compat).
  - `Enum.TryParse<DocumentClassification>` fail → `("invalid_classification")`.
  - `Restricted` → service reject sớm `("restricted_not_approvable")` (defense-in-depth ngoài Domain throw).
  - Hợp lệ → `doc.SetClassification(parsed)` rồi `doc.Approve(callerId)` → chunk+embed như cũ.

**(d) `Program.cs`:**
- `POST /v1/knowledge/documents` — body đã là `DocumentIngestRequest` (auto-bind field mới).
- `POST /v1/admin/knowledge/{id}/approve` — nhận optional `ApproveDocumentRequest? body` (nullable body OK — minimal API bind `ApproveDocumentRequest?` hoặc đọc JsonElement; verify pattern hiện có của reject endpoint dùng `RejectDocumentRequest body` non-nullable — approve cần cho phép empty body, dùng `[FromBody] ApproveDocumentRequest? body = null` hoặc check `Request.ContentLength`).

### 4. ShopERP admin — `TenantAiApiClient` + `TenantAiKnowledge.razor`

- `ITenantAiClient`/`TenantAiApiClient`:
  - `IngestDocumentAsync(..., string? sourceRef = null, ...)` — thêm optional param cuối.
  - `ApproveDocumentAsync(Guid tenantId, Guid docId, string? classification = null, ...)` — gửi body `{ classification }` khi non-null.
- `TenantAiKnowledge.razor` approve flow: confirm dialog thêm `<select>` classification (Public / Internal / Confidential — **không Restricted**), default = classification hiện tại của doc; truyền vào `ApproveDocumentAsync`.
- Hint nhẹ trong UI: "Public = AI trả lời khách trên storefront; Internal/Confidential = chỉ lưu trữ".

## Data contract chi tiết

**Ingest payload (từ ShopInbox):**
```json
POST /v1/knowledge/documents
{
  "type": "faq",
  "title": "Hỏi: {≤80 chars đầu câu hỏi}",
  "content": "Khách hỏi:\n{question verbatim}\n\nChủ quán đáp:\n{answer verbatim}",
  "classification": "internal",
  "sourceRef": "chat:{conversationId}:{messageId}"
}
```

- `classification="internal"` lúc ingest là **cố ý**: doc pending không retrievable ở đâu (default permission matrix chỉ mở Public trên public/preview channel); owner reject → không gì lộ.
- `type="faq"` — gần nhất trong enum hiện có {faq, story, policy, menu-note, recipe}. **Không thêm type mới** (phải sửa DocTypes + `DocTypesByIntent` trong retriever + data migration).
- Verbatim: không rewrite/tóm tắt; xuống dòng `\n` chuẩn hóa duy nhất.

**Approve payload (mới, optional):**
```json
POST /v1/admin/knowledge/{id}/approve
{ "classification": "public" }   // hoặc internal | confidential; absent = giữ nguyên
```

## Edge cases & failure modes

| Case | Xử lý |
|---|---|
| Tin owner không có câu hỏi khách phía trước | Panel disable nút + tooltip |
| Nhiều tin khách liên tiếp trước câu trả lời | Lấy tin gần nhất phía trước owner message |
| Nội dung chứa injection pattern | `IngestSanitizer` strip như mọi doc; `Sanitized=true` hiển thị admin |
| PII (SĐT/địa chỉ/đơn cụ thể) trong chat | Default `internal` + owner đọc lại khi approve — lý do KHÔNG auto-public |
| Flag OFF / service down | Nút ẩn; client fail-safe → toast nhẹ, chat không ảnh hưởng |
| Approve mà giữ `internal` | Doc live nhưng AI public không trả lời — đúng thiết kế, UI có hint |
| `Restricted` | Không có trong dropdown; server + Domain đều reject (defense-in-depth) |
| Double-click / 2 owner cùng message | Nút disable sau submit + SourceRef dedupe server-side |
| Staff khác reply (không phải owner hiện tại) | v1: nút chỉ trên `isOwn`; tin staff khác bị coi như "khách" trong pairing — edge case chấp nhận |

## Security & privacy checklist

- [ ] Chỉ Owner role (`ShopInbox` đã `[Authorize(Roles="Owner")]` — verify không cần thêm)
- [ ] TenantId luôn từ signed ctx / `TenantProvider.TenantId` — không từ request body
- [ ] `Restricted` blocked ở 3 lớp: ingest validate (có sẵn), approve service check, Domain `SetClassification` throw
- [ ] Không log full chat content ở mức Info (pattern hiện có: LogWarning metadata only)
- [ ] SourceRef chứa conversationId+messageId — opaque GUIDs, không PII

## Tests bắt buộc

### TenantAi.Tests (`9_TenantAI/vanan-ai/tests/`)

- [ ] `ApproveDocumentAsync` + `classification:"public"` trên doc pending → Classification=Public, ApprovalStatus=Approved, chunks đã tạo
- [ ] Approve không body/null classification → giữ nguyên classification gốc (backward compat)
- [ ] `classification:"restricted"` → error `restricted_not_approvable`, doc vẫn PendingReview
- [ ] `classification:"bogus"` → `invalid_classification`
- [ ] `SetClassification` trên doc đã Approved/Rejected → `InvalidOperationException`
- [ ] Ingest 2 lần cùng `sourceRef` → chỉ 1 doc (cái thứ 2 trả doc cũ)
- [ ] Ingest `sourceRef=null` → hoạt động như cũ (regression)

### ShopERP.Tests (`6_Tests/VanAn.ShopERP.Tests/`)

- [ ] `TenantAiApiClient.ApproveDocumentAsync` non-null classification → body `{classification}` đúng; null → body rỗng/absent
- [ ] `IngestDocumentAsync` forward `sourceRef` trong body
- [ ] Pairing logic: nếu tách pure function (vd `FindPrecedingCustomerMessage(messages, index, currentUserId)`) → unit test: owner msg + preceding customer msg → context đúng; owner msg đầu → null; nhiều customer msgs → lấy gần nhất

### UI.Platform / bUnit

- [ ] `KnowledgeSuggestEnabled=false` → không render nút (regression: Order chat không đổi)
- [ ] `isOwn` message + enabled → nút render; non-own → không

## RV (runtime verification) — sau deploy

Script gợi ý `6_Testing/rv-scripts/tenant-ai-k1-rv.cjs` (pattern `rv-p9-inbox-ai.cjs`):

1. L1: image/deploy chứa `KnowledgeSuggestEnabled` + `SetClassification`.
2. L2-L3: impersonate Owner → mở `/community/messages` → chọn conversation có tin owner → nút "→ AI" hiện trên tin isOwn → bấm → dialog preview đúng cặp hỏi/đáp → submit → toast success.
3. Verify TenantAI: `GET /v1/admin/knowledge/documents?status=pending_review` chứa doc mới với `sourceRef=chat:...`, `sanitized` flag đúng.
4. Approve với `classification=public` qua UI TenantAiKnowledge → status approved → `GET /v1/knowledge/status` chunks+1.
5. Regression: flag `TenantAiEnabled` OFF → nút không hiện; Order chat page không có nút.

## Không được làm (scope guard)

- Không auto-suggest / background mining (Phương án B — phase sau nếu cần)
- Không mining AI `conversation_messages` thành knowledge (D — task riêng)
- Không đổi `UsagePermissionFilter`, `IntentRouter`, retrieval path, hay human chat pipe
- Không thêm `DocumentType` mới, không DB migration (SourceRef column đã tồn tại — **verify EF mapping trước khi code**)
- Không cho Staff role bấm đề xuất (chỉ Owner)

## Câu hỏi còn mở (cần trả lời trước IMPLEMENT)

1. **Domain change `SetClassification`** — governance yêu cầu confirm. Additive method, không đổi invariant; cần user duyệt.
2. Route chính xác trang TenantAi Knowledge cho link redirect (implementer verify trong `TenantAiHub.razor`/`Sitemap.razor`).
3. Nút hiện trên **mọi** tin isOwn (đề xuất) hay chỉ tin cuối lượt trả lời? → Đề xuất: mọi tin isOwn, owner tự chọn.

## Acceptance

- [ ] Build `VanAn.sln` 0 err · `guard-check.ps1` PASSED
- [ ] TenantAi.Tests + ShopERP.Tests mới xanh, không regression
- [ ] RV production pass theo checklist trên
- [ ] Flag OFF → zero behavior change toàn hệ thống
