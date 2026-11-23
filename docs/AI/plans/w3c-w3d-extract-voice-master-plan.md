# MASTER PLAN: W3c-fix → W3d Multi-Group Extract + Voice Wiring

> Ngày: 2026-11-23 · Trạng thái: APPROVED direction, chờ implement
> Liên quan: `universal-ai-assistant-master-plan.md` · `w0_audit_v2/REAUDIT_W2_W5.md`
> Task cards: `docs/AI/tasks/universal_ai_assistant/task_w3c_multi_group_extract.md` · `task_w3d_voice_wiring.md`

## Bối cảnh

W3c (URL → knowledge) đã deploy `e4565009`: `WebContentExtractorService` same-origin deep crawl (≤6 trang, keyword-priority, SSRF-guarded) + fix `GeminiChatClient` gửi `responseSchema` thật (root cause lỗi "Không đọc được kết quả trích xuất").

User yêu cầu 2 nâng cấp:

1. **Multi-group extraction**: 1 lần trích xuất ra nhiều nhóm — menu / about-us / chính sách bán hàng-đổi trả-bảo hành / khuyến mãi.
2. **Voice wiring**: khảo sát xác nhận STT (SpeechRecognition vi-VN) + TTS (speechSynthesis) đã có sẵn — chỉ thiếu nối vào trợ lý AI quán (`/a/{slug}` + embed).

## Quyết định đã chốt (mặc định, user có thể override)

| # | Quyết định | Lựa chọn |
|---|---|---|
| D1 | Kiến trúc multi-extract | **vanan-ai fan-out song song** — endpoint mới `POST /v1/admin/knowledge/extract-groups`, vanan-ai chạy N group-call `Task.WhenAll`, trả `{groups:[{group,summary,items}]}`. ShopERP giữ mỏng (1 call → preview theo nhóm → N `IngestDocumentAsync`). |
| D2 | Số nhóm & prompt | 4 nhóm: `menu` (món+dịch vụ+giá+giờ) · `about` (giới thiệu) · `policy` (bán hàng/đổi trả/bảo hành) · `promotion` (khuyến mãi). Mỗi nhóm 1 prompt chuyên biệt + schema riêng → output nhỏ, giảm truncate risk. |
| D3 | Doc type mapping | `menu→menu-note` · `about→story` · `policy→policy` · `promotion→policy` (title prefix "Khuyến mãi:"). **Không thêm ingest type mới** — zero contract change; nếu sau cần retrieval riêng cho promo thì thêm `promotion` vào `KnowledgeIngestService.AllowedTypes` + retriever intent map (increment nhỏ, ghi trong card). |
| D4 | sourceRef per group | `extract:{group}:{guid}` — dedupe per nhóm, mỗi nhóm 1 doc pending riêng. |
| D5 | Voice scope V1 | Chỉ client-side: (a) wire `VoiceEnabled` vào `Assistant.razor` (mic trên `/a/{slug}` + embed); (b) TTS read-aloud nút loa trên message AI trong `AiChatWidget`. Server STT fallback + AI voice = W6, out of scope. |
| D6 | Voice flag | Tái dùng `Voice_Note_Enabled` (đã là tenant feature flag, `TenantProfileService.GetFeatureSettingsAsync`). Không tạo flag mới. |

## Phases

### Phase 1 — W3c-fix verify (đang chờ manual RV)
User retest extract trên prod `e4565009`. Nếu pass → đóng W3c.

### Phase 2 — Task W3c-multi (multi-group extract)
- vanan-ai: `KnowledgeGroupExtractor` (4 prompt/schema, parallel, per-group never-throw) + endpoint `/v1/admin/knowledge/extract-groups`.
- ShopERP `TenantAiApiClient`: `ExtractKnowledgeGroupsAsync`.
- `TenantAiKnowledge.razor`: preview theo nhóm (badge group + items editable per group) → confirm tạo N docs pending.
- Tests trước: extractor unit tests per group + parse/never-throw + group mapping.

### Phase 3 — Task W3d-voice (voice wiring V1)
- `Assistant.razor`: resolve `Voice_Note_Enabled` → `VoiceEnabled` param.
- UI.Platform: `js/voice-tts.js` (port `tts-reader.js` pattern, vi-VN voice select) + `AiChatWidget` speaker button per AI message + `SpeakAsync`/`StopSpeak`.
- Gate cùng `VoiceEnabled`; auto-stop khi message mới/đóng widget.
- E2E: Playwright spec mới cho mic visibility + TTS button (browser-gated, skip khi không support).

## Sweep-first RV (sau mỗi deploy)

1. Health/container image tag khớp commit.
2. L1 API: vanan-ai extract-groups endpoint reachable (qua ShopERP signed ctx — verify gián tiếp qua UI flow hoặc log).
3. L2 static: JS assets load (`voice-input.js`, `voice-tts.js`).
4. L3 Playwright: storefront `/store/{slug}?chat=ai` mic + `/a/{slug}` mic + speaker button.
5. L4 manual: owner extract URL → preview 4 nhóm → confirm → N docs pending → approve → chat AI trả lời đúng menu/policy/promo.

## Risks

| Risk | Mitigate |
|---|---|
| 4× LLM cost/extraction | Admin-triggered, low freq; flash-lite rẻ; cap input 20k chars giữ nguyên |
| Group lẫn nội dung (menu trong policy) | Prompt per-group explicit + "không bịa" rule giữ nguyên; owner review-sửa trước ingest |
| TTS vi-VN quality kém (browser native) | Chấp nhận V1; Gemini TTS = W6 nếu pilot thấy cần |
| SpeechRecognition chỉ Chromium | Đã có sẵn pattern `isSupported()` → mic ẩn graceful trên Firefox/Safari; server STT = V2 |
| Assistant.razor chưa có feature-settings call | Pattern có sẵn ở Store.razor (`TenantProfileService.GetFeatureSettingsAsync`) — copy, không phát minh |
