# RE-AUDIT W2–W5 — sửa sai sót của W0 audit (2026-11-22)

> User feedback: W0 audit bỏ lọt nhiều vấn đề, kết quả không chính xác.
> Doc này verify lại từng claim W0 bằng code thật + bổ sung những gì W0 bỏ sót.
> Trạng thái: ANALYZE — chưa implement gì từ doc này.

## 0. Corrections cho claim W0 (đã verify)

| # | W0 nói | Thực tế (verified) | Ảnh hưởng |
|---|---|---|---|
| C1 | `/v1/config/widget` "501 stub — cần verify" | **ĐÃ IMPLEMENT** (`Program.cs:239`, task_ai_p5 — aiEnabled=false khi kill-switch/quota deny, greeting/suggestions từ profile) | C3 conflict W0 giải quyết — không còn stub |
| C2 | "Chưa verify rate card `ai.chat.message` seed prod" | Code path đầy đủ (`MeteringServiceCodes` + QuotaGate + MeteringClient circuit breaker); **seed prod vẫn chưa verify được từ repo** — cần check metering DB/admin API | Vẫn open — verify lúc W2 bắt đầu |
| C3 | "chưa verify metadata có channel/session/assistant" | **XÁC NHẬN THIẾU end-to-end**: `ChatRequest` contract (ChatDtos.cs) KHÔNG có field metadata/channel; `TenantAiForwarder` không gửi; `ChatOrchestrator.EnqueueUsage` gọi `MeteringUsageRecord` **không truyền MetadataJson** (dù `MeteringClient` đã hỗ trợ field này). Attribution phải sửa ở 3 lớp: contract vanan-ai → forwarder Gateway → orchestrator | W2 phải làm — không phải "optional" |
| C4 | "TenantAiProfile trùng Assistant? cần check" | `TenantAiProfile`/`TenantAiConfig` trong vanan-ai = persona/greeting/policy/publish version **của engine**; `AssistantLink` (W1) = distribution link. KHÔNG trùng — nhưng **chưa có entity "Assistant" tổng hợp** (tên/persona/lifecycle) — hiện persona sống rải rác: TenantAiProfile (vanan-ai) + AIChat_Enabled flag (ShopFeatureSettings) + AssistantLink (Gateway). W2+ cần quyết định Assistant aggregate ở đâu | Quyết định mới cần PO |
| C5 | Knowledge ingest "text-only, không file/ảnh" | ĐÚNG nhưng **thiếu sót quan trọng**: ShopERP đã có `ITenantAiClient.IngestDocumentAsync` + `TenantAiKnowledge.razor` (paste text→doc) + `TenantAiKnowledgeSyncService`/`ReconcileService` (auto-sync products) + `ShopInbox` promote chat→knowledge (AI-P11). W3 "Auto-Builder" thực chất chỉ còn lớp **unstructured→structured extraction + wizard UX**, không phải pipeline mới | W3 scope nhỏ hơn W0 nghĩ nhiều |
| C6 | Widget "external JS SDK là port mới" | ĐÚNG một nửa — **iframe `/a/{slug}` đã embed được ngay hôm nay** (trang standalone, widget tự mở). W2 thực chất = (a) domain verification + (b) polish hosted page cho embed (ẩn header, theme, sizing) + (c) optional JS SDK wrapper mỏng gọi cùng API. Không cần Blazor→JS port | W2 effort giảm đáng kể |

## 1. W2 — Website Widget (re-audit)

### Đã có sẵn (W0 nói thiếu/sai)

| Capability | Evidence |
|---|---|
| Hosted chat page có thể iframe | `/a/{slug}` (W1) + `/store/{slug}?chat=ai` — standalone, mobile-first |
| Public chat API đầy đủ | `/api/public/tenant-ai/*` + `/api/public/assistant-link/{slug}/resolve` |
| CORS infrastructure | `DynamicCorsService` + `DynamicCorsCacheHostedService` — snapshot origins từ `Cors:StaticOrigins` + **`KhachLinkInstance.CustomDomain` active** |
| Widget config endpoint | `/v1/config/widget` real (P5) |

### W0 bỏ sót — vấn đề thật của W2

| # | Vấn đề | Chi tiết |
|---|---|---|
| W2-1 | **CORS chặn domain shop bên ngoài** | Snapshot chỉ lấy `KhachLinkInstance.CustomDomain` — domain website riêng của tenant KHÔNG được phép → widget call API sẽ bị CORS reject. Domain verification (W2) phải **feed vào nguồn CORS này** (thêm verified widget domains vào snapshot), không phải chỉ là UI flow |
| W2-2 | **iframe vs JS SDK — quyết định kiến trúc chưa chốt** | Iframe `/a/{slug}` = zero-code thật nhưng UX kém (scroll-lock, mobile viewport, không đồng bộ theme shop). JS SDK = UX tốt nhưng phải build + maintain. Đề xuất: **iframe trước (W2a), JS SDK sau (W2b optional)** |
| W2-3 | Attribution contract chưa có | `ChatRequest` không mang channel/widgetKey → metering không phân biệt storefront vs widget vs link. Phải extend contract (xem C3) |
| W2-4 | PublicWidgetKey chưa tồn tại ở đâu | Grep toàn repo = 0 hit. Cần entity + resolution path mới (pattern tham khảo AssistantLink) |
| W2-5 | `/a/{slug}` page hiển thị header "Trợ lý AI" + link Shop AI — không phù hợp embed | Cần `?embed=1` mode: ẩn header/footer, full-height, theme theo tenant |
| W2-6 | Rate limit `tenant-ai-public` đếm theo IP — nhiều site embed cùng IP shop owner test sẽ share quota | Chấp nhận được; ghi nhận |

## 2. W3 — Knowledge Auto-Builder (re-audit)

### Đã có sẵn — W0 đánh giá thấp

| Capability | Evidence |
|---|---|
| Text document ingest UI | `TenantAiKnowledge.razor` — paste text + classification → `IngestDocumentAsync` → `/v1/knowledge/documents` |
| Product auto-sync | `TenantAiKnowledgeSyncService` (incremental per-product + fullSync batch) + `TenantAiKnowledgeReconcileService` |
| Chat→knowledge promote | `ShopInbox.razor:391` — IngestDocumentAsync type "faq" (AI-P11) |
| Approval workflow | pending → approve/reject + classification override (TenantAi admin pages) |
| Status/health | `KnowledgeStatusResponse` + `TenantAiKnowledge` UI |

### Delta thật của W3 (nhỏ hơn W0 rất nhiều)

| # | Việc còn lại | Độ khó |
|---|---|---|
| W3-1 | LLM extraction: text thô (menu copy từ Zalo/web, bảng giá) → structured products/docs → **confirm UI** | Trung bình — gọi Gemini (cùng vendor) parse → map `TenantAiProductItem`/document → đẩy qua `PushProductsBatchAsync`/`IngestDocumentAsync` có sẵn |
| W3-2 | Ảnh menu → extract (Gemini vision) | Trung bình — cần upload endpoint + vision call |
| W3-3 | PDF/Excel → text | Thấp-trung — parser lib + reuse ingest |
| W3-4 | Website URL → crawl text | Trung bình-cao — SSRF guard bắt buộc |
| W3-5 | Onboarding wizard gộp: profile + knowledge + link/QR một luồng | UX layer |

**Khuyến nghị scope W3:** W3a = text-paste extraction + confirm (đã có 70% nền). W3b-d là increments riêng, không block.

### W3 status update (2026-11-22)

- **W3a DONE** — `KnowledgeExtractor` + `POST /v1/admin/knowledge/extract` + card "Trích xuất tự động" trên `TenantAiKnowledge` (commit `628eb0e3`).
- **W3b DONE (A+B, sysadmin-configurable, default B)** — OCR infra đã production sẵn (`/api/v1/ocr-assist` kind=menu, Tesseract client-side, wizard ShopERP + KhachLink Onboard); chỉ wire vào TenantAI:
  - Mode B (default): `MenuOcrService` + upload ảnh menu trong card Trích xuất → OCR → text → extract → pending-review.
  - Mode A: `OcrMenuKnowledgeService` — sau import sản phẩm menu OCR thành công trong `OcrOnboardingWizard`/`TenantManagement` → `IngestDocumentAsync("menu-note", sourceRef="ocr-menu:{tenantId}")` pending-review. Flag `ShopFeature:{tenantId}:OcrMenuKnowledgeAuto` + toggle ở `/settings/shop-features`.
  - Tests `OcrMenuKnowledgeServiceTests` 4/4 PASS.
- **Còn lại:** W3c PDF/Excel/URL crawl — increments riêng.

## 3. W4 — Business Actions (re-audit)

### Đã có sẵn — W0 nói thiếu nhưng thực tế có

| Capability | Evidence |
|---|---|
| Human handoff end-to-end | `RealtimeChatPanel` guest device id → messaging hub/HTTP → **`ShopInbox.razor`** owner inbox — đã chạy trên storefront VÀ `/a/{slug}` (W1 wired) |
| Public booking API | `PublicBookingController`: `/entry`, `/qr/{token}`, `/tenants/{id}/services`, `/availability`, `POST /bookings`, `/bookings/{token}`, `/cancel` |
| Lead entity + FB webhook | `Lead` + `LeadManagementService` + `LeadConversionService` + `FacebookLeadService` + `WebhookController /api/webhooks/{provider}` + `ProcessedWebhookKey` idempotency |
| Order public surface | `PublicOrdersController` tồn tại |

### Delta thật của W4

| # | Việc | Ghi chú |
|---|---|---|
| W4-1 | **AI → booking deep action**: chat trả lời kèm booking link/CTA (`/booking/entry?tenant=` hoặc QR flow có sẵn) — có thể chỉ cần product-card-style action trong `ChatResponse` + widget render | Contract `ChatResponse` cần thêm action/CTA field hoặc dùng product cards hiện có |
| W4-2 | **AI → lead capture**: guest để lại SĐT trong chat → tạo `Lead` (SourceReference=assistant session) → `LeadConversionService` flow có sẵn | Cần endpoint mới + wire vào orchestrator policy (intent detect "để lại SĐT") |
| W4-3 | Handoff từ AI refuse → human ĐÃ CÓ (chip AI-P8) — W4 chỉ cần verify trên `/a/` + polish (context handoff: tóm tắt cuộc chat cho owner) | Nhỏ |
| W4-4 | AI execute order (L6) — phức tạp: order creation cần customer identity/address/payment — **defer**, chỉ làm booking + lead trước | Đúng roadmap |

## 4. W5 — Distribution (re-audit)

| Surface | Thực tế |
|---|---|
| Browser Extension | ❌ Không có gì — build mới hoàn toàn (MV3, content script inject widget.js hoặc gọi API trực tiếp) |
| Android floating | `4_MobileApps` chỉ có `HRApp` + `StationApp` (staff nội bộ) — KHÔNG có customer app. Floating assistant = app mới hoặc PWA approach (đơn giản hơn nhiều: PWA + widget đã mobile-first) |
| Zalo OA / FB Messenger / TikTok | FB **Lead Ads webhook** có sẵn (inbound leads, không phải chat). Không có Messenger chat webhook, không Zalo OA. Mỗi kênh cần: webhook receiver + mapping page-scoped ID → assistant + session continuity |
| Continuity `SessionTransferToken` | ❌ Không tồn tại ở đâu (grep = 0). Cần signed token chứa sessionId+tenant+channel+expiry — pattern tham khảo `TenantAiContextSigner` (HMAC) đã có trong Contracts |

**Khuyến nghị W5 thứ tự lại:** PWA floating (rẻ, tái dùng 100% web stack) → Extension → kênh social (mỗi kênh 1 spike vì API platform khác nhau) → continuity token (làm sớm nếu muốn cross-channel, không block surface đơn lẻ).

## 5. Cross-cutting gaps W0 bỏ sót hoàn toàn

| # | Gap | Wave ảnh hưởng |
|---|---|---|
| G1 | **Attribution metadata end-to-end**: contract `ChatRequest` + forwarder + `EnqueueUsage` đều thiếu channel/assistantRef/session — metering hiện không phân biệt surface nào | W2 (bắt buộc trước khi có >1 surface thu phí) |
| G2 | **Assistant aggregate entity chưa có**: persona nằm ở TenantAiProfile (vanan-ai), enable flag ở ShopFeatureSettings, link ở AssistantLink — 3 nơi. SRS muốn "1 assistant/shop" với lifecycle — cần quyết định source of truth | W2 trở đi |
| G3 | **`/a/{slug}` hoạt động trên MỌI KhachLink domain** (multi-profile: shopai, diemthuong, hoptacxa...) — slug resolution bỏ qua instance context. Có thể OK (link universal) nhưng link share qua domain "sai" vẫn chạy — ghi nhận, không block | W1 note |
| G4 | `TryAcquireSessionSlot` copy-paste giữa 2 controller — nên extract shared helper khi thêm surface thứ 3 | W2 |
| G5 | AssistantLink không có publish/pause lifecycle (chỉ IsActive) — SRS promised states; hiện revoke = pause. Đủ dùng W1-W2, note cho W4 | W4 |
| G6 | Voice: W0 audit chủ yếu web research, **chưa spike thật** — giá/latency claims chưa verify. Nên có spike Gemini Live 1-2 ngày ở W4 song song, không chờ W6 | W4/W6 |

## 6. Kết luận re-audit

- **W2 nhỏ hơn W0 tưởng** (iframe có sẵn) nhưng **phức tạp hơn ở CORS + attribution contract** — 2 việc W0 đánh giá sai hướng.
- **W3 nhỏ hơn nhiều** — pipeline + UI ingest đã có, chỉ thiếu extraction layer.
- **W4 khả thi sớm hơn** — handoff/booking/lead infra đều production; delta là action/CTA contract + lead endpoint.
- **W5 đúng là build mới** nhưng PWA approach rẻ hơn native nhiều.
- **Blocker chung trước W2:** attribution metadata (G1) + Assistant aggregate decision (G2) + rate-card seed verify.
