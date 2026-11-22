# SOFTWARE REQUIREMENTS SPECIFICATION
# VANAN SHOP AI ASSISTANT
## SRS v1.0

| Thuộc tính | Giá trị |
|---|---|
| Sản phẩm | Vạn An Shop AI Assistant |
| Tài liệu | Software Requirements Specification |
| Phiên bản | 1.0 |
| Ngày lập | 22/11/2026 |
| Trạng thái | Proposed – Baseline for review and implementation planning |
| Định vị | **Mỗi shop Vạn An có một AI assistant riêng** — không phải "universal chatbot platform" |
| Ưu tiên | P1 – Shop-facing feature của hệ sinh thái Vạn An |
| Đối tượng thực hiện | Devin / Engineering / QA / Product Owner |
| Tiền thân | Thay thế định vị của `SRS_VanAn_Universal_AI_Assistant_Zero_Code_Integration_Platform_v1.0.md`; tái sử dụng phần lớn yêu cầu kỹ thuật và security của tài liệu đó |
| Ngôn ngữ | Vietnamese requirements; technical identifiers in English |

> **Mục tiêu cốt lõi:** Mỗi shop/doanh nghiệp đang dùng hệ sinh thái Vạn An (ShopERP, Gateway, KhachLink) có thể bật một AI assistant riêng trong vài phút — trả lời khách bằng chính tri thức đã được duyệt của shop đó. Assistant được gọi qua **AI Link/QR** (không cần cài đặt) và **Website Widget** (khi shop có website hỗ trợ). Không bán "chatbot trên mọi website"; bán "mỗi shop có trợ lý riêng".

---

# 0. DOCUMENT CONTROL

## 0.1. Quan hệ với tài liệu khác

Tài liệu này **thu hẹp và tái định vị** SRS Universal AI Assistant v1.0. Các yêu cầu kỹ thuật vẫn áp dụng trừ khi bị ghi đè tại đây:

- **FR-110→FR-122 (Browser Extension): OUT OF SCOPE** trong v1.0 — đưa vào "Future Consideration" (§17).
- **FR-130→FR-140 (Android Floating Assistant): OUT OF SCOPE** trong v1.0 — cùng mục trên.
- **Mọi yêu cầu khác của SRS gốc vẫn hiệu lực** (domain model, security controls SEC-001→024, NFR, error codes, acceptance tests AT-001→AT-025), với các điều chỉnh tại §4 và §13 của tài liệu này.

Các service nền **không bị thay thế**:

- Vạn An Tenant AI (`9_TenantAI/vanan-ai`) — knowledge/chat engine.
- Vạn An Voice Service (`10_Voice/vanan-voice`) — human-to-human calling; **không** chứa AI voice.
- Vạn An Metering (`8_Metering/vanan-metering`) — quota/wallet/usage.
- Gateway, Account/Identity/Device Trust, CoreHub, UI.Platform, KhachLink.

## 0.2. Nguyên tắc đọc tài liệu

- **MUST**: bắt buộc để nghiệm thu.
- **SHOULD**: nên có; chỉ bỏ khi có lý do và được PO duyệt.
- **MAY**: tùy chọn.
- **P0**: khảo sát và xác minh trước khi code.
- **P1**: AI Link/QR + text chat (hero release).
- **P2**: Website Widget trên domain đã verify + domain verification.
- **P3**: Zero-Code Installation Manager + WordPress plugin + Assisted Installation vận hành đầy đủ.

## 0.3. Phân biệt mức độ xác thực

Mọi mô tả hệ thống hiện hữu phải được Devin xác minh từ codebase và deployment thực tế. SRS này ghi lại các quyết định kiến trúc đã có trong tài liệu nền, nhưng không mặc định rằng mọi endpoint, API hoặc chức năng đã triển khai thành công.

---

# 1. EXECUTIVE SUMMARY

## 1.1. Bài toán

Shop Vạn An hiện đã có dữ liệu vận hành (sản phẩm, đơn hàng, FAQ, bảng giá, chính sách) trong ShopERP/TenantAI nhưng **khách hàng vẫn phải gọi điện, nhắn Zalo hoặc tự lục website** để được tư vấn. Mỗi shop muốn có "trợ lý riêng" nhưng:

- Không biết code, không muốn thuê lập trình viên.
- Đa số không có website riêng đủ điều kiện nhúng widget (bán qua Facebook page, Zalo OA, gian hàng, hoặc website đơn giản).
- Không muốn khách phải tải app hay tạo tài khoản.

## 1.2. Định vị sản phẩm

**"Mỗi shop Vạn An có một AI assistant riêng — bật trong 5 phút, dùng được ngay bằng link/QR."**

- Không phải nền tảng chatbot cho website bất kỳ.
- Không phải widget universal nhúng vào mọi nơi.
- Là **tính năng của shop**: assistant thuộc về shop, trả lời bằng knowledge của shop, tính phí vào ví shop, tắt được bất cứ lúc nào.

## 1.3. Product principles

- **Shop-first**: mọi quyết định ưu tiên trải nghiệm chủ shop và khách của shop đó.
- **Link/QR là hero**: kênh không cần cài đặt, dùng được với mọi shop kể cả không có website.
- **Widget là bonus**: chỉ khi shop có website và xác minh được domain.
- **Tenant Knowledge First**: assistant chỉ trả lời từ knowledge đã được shop duyệt; không trộn giữa các shop.
- **AI tư vấn, không giao dịch**: không tạo đơn, không đổi giá/tồn kho, không xác nhận thanh toán.
- **Zero-code ≠ zero-labor**: tự cài được là chính, Vạn An hỗ trợ cài là dịch vụ có SKU riêng.
- **Tái sử dụng, không xây lại**: Tenant AI, Metering, Voice, Gateway, Account giữ nguyên vai trò.

---

# 2. SCOPE

## 2.1. In Scope

### P1 – Shop AI Assistant Core (hero release)

- TenantAssistant: một assistant gắn với một tenant/shop (1:1 mặc định, cho phép nhiều assistant/shop ở cấu hình nâng cao).
- Bật/tắt, đặt tên, persona, ngôn ngữ, lời chào, appearance cơ bản.
- **Hosted AI Link** (`/a/{slug}` trên domain Vạn An kiểm soát) + **QR code** PNG/SVG.
- Public anonymous text chat session → Tenant AI (đã có sẵn engine).
- Metering: mọi chat message trừ vào usage/wallet của shop theo rate card đã duyệt.
- Dashboard tối thiểu trong ShopERP/portal hiện có: trạng thái, usage, pause/resume, link/QR.
- Knowledge setup tối thiểu: chọn nguồn đã có trong TenantAI (FAQ, tài liệu đã ingest) + nhập FAQ thủ công.
- Kill switch: shop pause; platform admin revoke.
- Consent & privacy baseline theo SRS gốc §6.9/§9.

### P2 – Website Widget + Domain Verification

- Domain binding + verification (DNS TXT primary; meta-file secondary; **operator manual review fallback** — bắt buộc cho SME VN).
- Widget SDK (loader + floating chat) nhúng bằng snippet do shop copy, hoặc qua assisted install.
- Widget chỉ hoạt động trên domain đã verify; AI Link vẫn hoạt động độc lập.
- Installation health check cơ bản.

### P3 – Assisted Installation & Installation Manager (vận hành hóa)

- InstallationJob queue cho operator Vạn An.
- WordPress plugin (sau compatibility audit).
- Zero-code wizard hoàn chỉnh, plain-language Vietnamese.
- Assisted installation như một **dịch vụ có quy trình + có thể tính phí** (PO quyết định).

## 2.2. Out of Scope v1.0

- Browser Extension — future consideration (§17).
- Android Floating Assistant — future consideration (§17).
- AI voice conversation — Voice Service hiện là human-to-human; không claim cho đến khi có AI audio adapter được verify (P0 audit).
- Widget injection vào Facebook/TikTok native app; scraping MXH — chỉ dùng link/QR.
- Shopify/Wix app — sau P3, theo platform review riêng.
- AI tạo đơn/thanh toán/đổi giá/tồn kho — tuyệt đối không.
- Đa assistant marketplace cho website không thuộc hệ Vạn An — không phải mục tiêu.

---

# 3. USERS, ACTORS AND USE CASES

## 3.1. Actors

| Actor | Description | Main permissions |
|---|---|---|
| Shop Owner (Tenant Owner) | Chủ shop dùng Vạn An | Bật/tắt assistant, cấu hình, xem usage, lấy link/QR |
| Tenant Staff | Nhân viên shop được phân quyền | Xem conversation được phép, chỉnh sửa giới hạn |
| Khách của shop (Visitor) | Người chat với assistant | Dùng text chat công khai theo policy của shop |
| Vạn An Operator | Nhân viên hỗ trợ cài đặt | Assisted installation theo job được giao, scoped |
| Platform Admin | Quản trị Vạn An | Rate card, revoke, kill switch toàn cục, audit |
| Tenant AI / Metering / Voice | Service nội bộ | Engine phía sau, giữ nguyên contract hiện có |

## 3.2. Use cases chính

### UC-01 – Shop bật assistant bằng link/QR (P1, hero)

1. Shop Owner đăng nhập ShopERP/portal Vạn An.
2. Vào "Trợ lý AI" → "Bật trợ lý cho shop".
3. Wizard hỏi tối thiểu: tên trợ lý, lời chào, ngôn ngữ, chọn nguồn knowledge đã có.
4. Preview cuộc chat thử (test conversation bắt buộc trước khi publish).
5. Bấm "Kích hoạt" → hệ thống tạo **AI Link** + **QR**.
6. Owner copy link gửi Zalo/Messenger, in QR tại quầy, dán lên bio Facebook.
7. Khách mở link → chat text với assistant của đúng shop đó.

### UC-02 – Shop có website, muốn widget (P2)

1. Owner thêm website URL.
2. Verify domain: DNS TXT (hướng dẫn từng bước) hoặc upload file/meta token.
3. Nếu verify fail sau hướng dẫn → chọn "Vạn An hỗ trợ xác minh" → operator review thủ công (ảnh chụp DNS/hosting panel, hoặc gọi xác nhận).
4. Owner copy snippet hoặc yêu cầu "Vạn An cài giúp" (assisted job, P3).
5. Widget xuất hiện trên site; chat vẫn đi qua cùng backend, cùng metering.

### UC-03 – Khách chat qua widget/AI Link (P1)

1. Khách mở link hoặc thấy widget trên site của shop.
2. Thấy tên shop + lời chào → biết đang nói chuyện với ai (FR-078 gốc).
3. Gửi câu hỏi → hệ thống tạo anonymous session scoped theo shop.
4. Tenant AI trả lời từ knowledge đã duyệt của shop.
5. Hết quota hoặc shop pause → thông báo lịch sự, không fake câu trả lời.

### UC-04 – Shop theo dõi và kiểm soát

1. Owner xem dashboard: số cuộc chat, số message, chi phí/quota đã dùng, trạng thái.
2. Nâng/hạ quota; pause assistant khi nghỉ lễ/đổi menu.
3. Thu hồi link (ví dụ link cũ bị phát tán sai nơi) mà không xóa assistant.

### UC-05 – Vạn An hỗ trợ cài đặt (P3, dịch vụ)

1. Owner chọn "Vạn An cài giúp" → tạo InstallationJob.
2. Operator liên hệ, hướng dẫn owner tự thao tác theo least-privilege (collaborator account có hạn, hoặc owner tự paste snippet).
3. **Cấm** thu thập mật khẩu admin website qua chat/email/form.
4. Installation Checker verify runtime thật (không chỉ snippet tồn tại).
5. Owner duyệt preview → kích hoạt.

---

# 4. QUYẾT ĐỊNH KIẾN TRÚC (thay đổi so với SRS gốc)

| ID | Quyết định | Trạng thái |
|---|---|---|
| SA-001 | Assistant gắn 1:1 với Tenant (shop) ở P1; multi-assistant chỉ qua config nâng cao, không làm UI phức tạp | Chốt |
| SA-002 | **AI Link/QR là kênh chính (P1); Website Widget là kênh phụ (P2)** — đảo ưu tiên so với SRS gốc | Chốt |
| SA-003 | Assistant configuration thuộc về **shop tenant hiện có**, lưu trong domain/config store hiện hữu sau P0 audit — không tạo service mới trừ khi audit chứng minh bắt buộc | P0 xác minh |
| SA-004 | **Ai trả tiền:** mọi chat message của visitor trừ vào quota/wallet của shop (tenant trả). Anonymous abuse được chặn bằng per-assistant rate limit + per-IP limit + bot protection trước khi tốn LLM token | Chốt (thay PO-008) |
| SA-005 | Shop có hard-cap chi tiêu/tháng cho assistant (mặc định theo gói tenant); vượt cap → degrade sang structured-only answer hoặc thông báo hết lượt, **không bao giờ** gọi LLM unmetered | Chốt |
| SA-006 | Domain verification bắt buộc cho widget (P2); **không bắt buộc cho AI Link/QR** — link hoạt động ngay cả khi shop không có website | Chốt |
| SA-007 | Thêm phương thức verify thứ 3: **operator manual review** — vì DNS/meta-file fail phổ biến với SME VN | Chốt (mới) |
| SA-008 | Voice AI: chat-first. UI voice ẩn hoàn toàn cho đến khi P0 audit xác nhận AI audio adapter tồn tại và pass test | Kế thừa D-013 |
| SA-009 | Browser Extension & Android Floating: **out of scope v1.0**, đưa vào Future Consideration có điều kiện | Chốt (mới) |
| SA-010 | Widget chỉ nhúng vào website **thuộc sở hữu shop đã verify**. Không hỗ trợ "gắn widget lên site người khác" — đó là bài toán khác | Chốt (mới) |
| SA-011 | Knowledge source: tái dùng TenantAI hiện có (documents đã ingest, FAQ, structured data). Wizard P1 chỉ cho phép **chọn nguồn đã có + nhập FAQ ngắn** — chưa làm crawl tự động | Chốt (mới) |
| SA-012 | Không tạo bảng Tenant/User/Assistant identity trùng lặp; kiểm tra entity hiện có trước (kế thừa §5.1 gốc) | Kế thừa |

## 4.1. Tại sao đảo ưu tiên Link/QR lên trước Widget

1. **Đúng phân khúc**: đa số shop Vạn An VN bán qua FB/Zalo/điện thoại — không có website để nhúng widget.
2. **Ít moving parts**: không cần domain verification, không cần lo CSP/ad-blocker/host-site conflict.
3. **Ship nhanh hơn**: P1 chỉ cần hosted page + chat session + metering — tái dùng tối đa TenantAI.
4. **Vẫn giữ moat**: assistant trả lời bằng knowledge riêng của shop = giá trị cốt lõi, bất kể kênh nào.

---

# 5. DOMAIN MODEL

Kế thừa §5 SRS gốc với điều chỉnh:

## 5.1. Entities áp dụng cho v1.0

| Entity | Ghi chú cho Shop Assistant |
|---|---|
| TenantAssistant | 1:1 với Tenant ở P1; Status: Draft/Preview/Published/Paused/Suspended |
| WebsiteDomain | Chỉ cần từ P2 (widget); AI Link không cần domain binding |
| WidgetInstance | Chỉ từ P2 |
| AssistantLink | **Entity trung tâm của P1** — slug unguessable, revocable, per-assistant |
| QrAsset | P1 — trỏ về AssistantLink |
| InstallationJob | P3 — nhưng có thể tạo thủ công từ P2 |
| InstallationCheck | P3 |
| PublicVisitorSession | P1 — anonymous, scoped theo assistant+tenant, TTL ngắn |
| ConsentRecord | P1 — tối thiểu: chat consent + privacy notice version |
| AssistantUsageSummary | P1 — feed từ Metering |
| AuditEvent | P1 — mọi publish/pause/revoke |

**Không dùng ở v1.0:** SurfaceRegistration (extension/app), AssistantSurface đa loại (chỉ có `link`, `widget`), voice session entities.

## 5.2. Identifier rules

Kế thừa §5.2 gốc. Bổ sung:

- `AssistantLink.slug`: high-entropy, unguessable, revocable, **không chứa** tenant name hay PII (tránh enumeration/enumeration leak).
- Link public page phải hiển thị **tên shop + logo** rõ ràng để khách biết đang chat với ai — nhưng không lộ TenantId, quota, hay thông tin nội bộ.

## 5.3. Relationship constraints

- Một tenant = một active assistant ở P1 (đơn giản hóa onboarding; multi-assistant là future).
- AssistantLink → đúng 1 assistant → đúng 1 tenant. Không share link giữa tenant.
- PublicVisitorSession scoped theo assistant; không merge anonymous sessions xuyên kênh.
- Domain binding (P2) không share giữa các tenant — giữ SEC-022.

---

# 6. FUNCTIONAL REQUIREMENTS

## 6.1. Assistant onboarding (P1)

| ID | Requirement | Priority |
|---|---|---|
| FR-001 | Shop Owner MUST bật assistant từ portal/ShopERP hiện có, không cần code | P1 |
| FR-002 | Wizard MUST hỏi tối thiểu: tên trợ lý, lời chào, ngôn ngữ, persona cơ bản | P1 |
| FR-003 | Owner MUST chọn knowledge source từ danh sách TenantAI đã ingest sẵn (FAQ/docs/structured); nhập FAQ thủ công ≤ N mục | P1 |
| FR-004 | TenantId MUST resolve từ authenticated shop context, không từ client input | P1 |
| FR-005 | Assistant MUST có states Draft/Preview/Published/Paused/Suspended | P1 |
| FR-006 | Publish MUST tạo config version audit-able; rollback về version trước (SHOULD P1) | P1 |
| FR-007 | Test conversation bắt buộc trước publish | P1 |
| FR-008 | Readiness checklist plain-language: "Shop chưa có FAQ — trợ lý sẽ trả lời hạn chế" | P1 |
| FR-009 | Owner MUST pause/resume bất cứ lúc nào, hiệu lực trong cache TTL giới hạn | P1 |

## 6.2. AI Link và QR (P1 — hero)

| ID | Requirement | Priority |
|---|---|---|
| FR-070 | Mỗi published assistant MUST có hosted AI Link HTTPS với slug opaque | P1 |
| FR-071 | Link MUST tạo QR PNG + SVG/SVG-equivalent | P1 |
| FR-072 | Owner MUST copy link, tải/in QR | P1 |
| FR-073 | Link MUST hoạt động không cần website/widget | P1 |
| FR-074 | Link MUST giữ tenant policy + knowledge boundary của shop | P1 |
| FR-075 | Owner MUST pause/revoke link độc lập với assistant | P1 |
| FR-076 | Link MUST có rate limit + abuse protection (kế thừa FR-077 gốc) | P1 |
| FR-077 | Link page MUST hiển thị tên shop/logo rõ ràng | P1 |
| FR-078 | QR MUST NOT chứa API key/credential/PII | P1 |
| FR-079 | Hỗ trợ regenerate link khi slug bị lộ (revoke cũ → cấp mới) | P1 |

## 6.3. Public Chat Session (P1)

| ID | Requirement | Priority |
|---|---|---|
| FR-090 | Guest mở link → tạo anonymous session scoped assistant+tenant | P1 |
| FR-091 | Session token short-lived, scoped, không dùng được cho admin API | P1 |
| FR-092 | Guest chat text KHÔNG cần login | P1 |
| FR-093 | Rate limit per-IP + per-assistant + per-session; bot mitigation tối thiểu (CAPTCHA/Turnstile-class) trước khi gọi LLM | P1 |
| FR-094 | Mọi message MUST qua Tenant AI với tenant context ký server-side; client không tự chọn TenantId | P1 |
| FR-095 | Idempotency theo messageId scoped session | P1 |
| FR-096 | Hết quota/cap → thông báo rõ, KHÔNG gọi LLM unmetered, KHÔNG fake answer | P1 |
| FR-097 | Khi shop pause → link trả trạng thái "shop tạm ngưng", giữ lịch sự | P1 |
| FR-098 | Conversation history guest: trong session hiện tại; cross-session chỉ khi guest đăng nhập (SHOULD P2) | P1/P2 |
| FR-099 | Guest MAY để lại tên/SĐT liên hệ — chỉ khi có notice rõ và hành động chủ động (SHOULD P2) | P2 |

## 6.4. Metering & Billing (P1)

| ID | Requirement | Priority |
|---|---|---|
| FR-110 | Mọi chat message MUST ghi nhận usage qua Metering hiện có | P1 |
| FR-111 | Service code đề xuất: `ai.chat.message` — MUST đăng ký rate card trước production | P1 |
| FR-112 | Shop MUST có hard-cap chi tiêu/tháng cho assistant; default theo gói tenant | P1 |
| FR-113 | Vượt cap → degrade structured-only hoặc "hết lượt", không silent unmetered | P1 |
| FR-114 | Không double-charge: định nghĩa idempotency key + billing unit rõ (kế thừa §4.D gốc) | P1 |
| FR-115 | Dashboard MUST hiển thị usage + quota state | P1 |
| FR-116 | Owner MAY mua/nâng gói quota assistant như một add-on Metering (PO quyết định giá) | P2 |

## 6.5. Domain & Widget (P2)

Kế thừa §6.2 + §6.3 SRS gốc (FR-020→FR-060, FR-040→FR-060) với bổ sung:

| ID | Requirement | Priority |
|---|---|---|
| FR-120 | Domain verification DNS TXT primary; meta-file secondary | P2 |
| FR-121 | **Operator manual review** là phương thức verify thứ 3 cho trường hợp kỹ thuật fail | P2 |
| FR-122 | Widget MUST NOT activate trên domain chưa verify (trừ preview/test mode) | P2 |
| FR-123 | Widget dùng cùng AssistantLink identity — khách chat trên widget hay link đều là cùng assistant+tenant | P2 |
| FR-124 | Nếu shop xóa domain binding → widget ngừng, AI Link vẫn hoạt động | P2 |

## 6.6. Installation Manager & Assisted Install (P3)

Kế thừa §6.5 SRS gốc (FR-090→FR-106) + §6.5.1, rút gọn ưu tiên:

- WordPress plugin: P3 sau compatibility audit.
- Assisted installation: quy trình vận hành + SLA; **có thể tính phí như dịch vụ** — PO quyết định (thay PO-010: giờ đã rõ đây là revenue stream, không chỉ cost).
- Mọi nguyên tắc an toàn giữ nguyên: không thu password, least-privilege collaborator, audit đầy đủ.

## 6.7. Dashboard & Admin (P1 tối thiểu, P2 đầy đủ)

| ID | Requirement | Priority |
|---|---|---|
| FR-140 | Owner xem: trạng thái assistant, link/QR, usage, quota | P1 |
| FR-141 | Owner pause/resume/revoke link | P1 |
| FR-142 | Owner xem readiness checklist + gợi ý cải thiện ("thêm FAQ để trả lời tốt hơn") | P1 |
| FR-143 | Operator queue + scoped access (P3) | P3 |
| FR-144 | Platform admin: revoke keys/links/domains, kill switch, audit | P1 |
| FR-145 | Console không lộ LLM/provider secrets | P1 |

## 6.8. Voice

- **Không có requirement voice nào trong v1.0.** UI voice ẩn hoàn toàn.
- Future: `IAiVoiceSessionAdapter` chỉ khi P0 audit + provider selection + legal review hoàn tất (kế thừa §6.8.1, §7.7 gốc).

---

# 7. API AND INTEGRATION CONTRACTS

Kế thừa §7 SRS gốc, điều chỉnh:

## 7.1. Public (P1)

- `GET /v1/public/assistants/{slug-or-key}/config` — public display config, capability flags, **không secrets**.
- `POST /v1/public/assistants/{assistantRef}/sessions` — tạo anonymous session; resolve assistant từ published link/domain mapping; **client không gửi TenantId**.
- `POST /v1/public/assistant-sessions/{sessionId}/messages` — chat; idempotency messageId; pageUrl chỉ là metadata.

> Tên route chính xác do P0 audit map vào API conventions hiện có (Gateway facade pattern). Đây là logical contract, không phải route đã tồn tại.

## 7.2. Authenticated shop (P1)

- `POST /v1/assistants` / `GET/PATCH /v1/assistants/{id}` — tạo/cấu hình (1 assistant/tenant ở P1).
- `POST /v1/assistants/{id}/preview` / `/publish` / `/pause` / `/resume`.
- `POST /v1/assistants/{id}/links` / `POST /v1/links/{linkId}/revoke` / `POST /v1/links/{linkId}/regenerate`.
- `GET /v1/assistants/{id}/qr` / `POST /v1/assistants/{id}/qr/regenerate`.
- `GET /v1/assistants/{id}/usage` — usage summary từ Metering.
- `PATCH /v1/assistants/{id}/quota` — cap chi tiêu (theo quyền owner/admin tenant).

## 7.3. Domain & Installation (P2/P3)

Kế thừa §7.5/§7.6 gốc + thêm:

- `POST /v1/domains/{domainId}/verification/manual-review` — tạo request operator review kèm evidence.
- `GET /v1/operator/installation-jobs` — queue cho operator (P3).

## 7.4. Service-to-service

Kế thừa §7.8 gốc nguyên vẹn:

- Public client → Gateway facade → Tenant AI (signed tenant context, caller key + scopes + HMAC).
- → Metering với caller credentials + idempotency.
- Không browser-held service keys, không DB access trực tiếp.

---

# 8. USER INTERFACE REQUIREMENTS

## 8.1. Shop Owner screens (trong portal/ShopERP hiện có)

| Screen | Nội dung |
|---|---|
| Assistant setup wizard | 3-4 bước: bật → tên/lời chào/ngôn ngữ → chọn knowledge → preview → kích hoạt |
| Assistant dashboard | Trạng thái, link/QR (copy/download), usage tháng này, quota bar, pause/resume |
| Knowledge picker | Checkbox nguồn TenantAI đã ingest + form FAQ thủ công ngắn |
| Domain management (P2) | Thêm site, hướng dẫn verify từng bước, nút "Vạn An hỗ trợ xác minh" |
| Installation (P3) | Job status, hướng dẫn plain-language, liên hệ operator |

## 8.2. Guest chat page (hosted AI Link, P1)

- Mobile-first (đa số traffic từ Zalo/Messenger → mobile).
- Header: logo + tên shop + "Trợ lý AI của {ShopName}".
- States: welcome / typing / answer / error-retry / quota-exceeded / shop-paused.
- Privacy notice link + nút "Xóa cuộc trò chuyện".
- Không quảng cáo, không cross-sell UI — giữ sạch, đây là kênh của shop không phải của Vạn An.

## 8.3. UX language

Kế thừa §8.3 gốc: tránh "paste JavaScript", "configure API key", "DNS record" không giải thích. Ưu tiên:

- "Bật trợ lý AI cho shop"
- "Sao chép đường link này gửi cho khách"
- "In mã QR này để dán tại quầy"
- "Trợ lý đang dùng hết lượt tháng này — liên hệ Vạn An để nâng cấp"
- "Shop tạm ngưng trợ lý — vui lòng quay lại sau"

---

# 9. SECURITY, PRIVACY AND COMPLIANCE

Kế thừa toàn bộ §9 SRS gốc (threat model, SEC-001→024, privacy Vietnam/LPDP theo Law 91/2025). Điểm nhấn riêng cho v1.0:

- **SEC bổ sung SA**: link slug phải unguessable; page public không lộ TenantId/quota/usage nội bộ.
- Anonymous chat là attack surface chính → bot mitigation + rate limit là MUST của P1, không phải nice-to-have.
- Conversation content của guest = personal data → retention theo TenantAI policy hiện có, không tạo policy mâu thuẫn.
- Legal review bắt buộc trước production (kế thừa §9.3).

---

# 10. NON-FUNCTIONAL REQUIREMENTS

Kế thừa §10 SRS gốc với điều chỉnh:

- **Performance**: hosted link page + chat phải load nhanh trên mobile 3G/4G phổ thông VN — target FCP < 2.5s trên mid-tier Android (thêm metric, vì phần lớn traffic là mobile từ MXH).
- Widget performance budgets (NFR-001→008 gốc) chỉ áp dụng từ P2.
- **Availability**: link page degrade gracefully khi TenantAI/Metering down — "trợ lý tạm bận, thử lại sau", không fake answer.
- **Scalability**: pilot 10-50 shop → 100 → 1.000 theo bảng gốc; không provision cho 10k trước dữ liệu thật.
- **Compatibility P1**: mọi modern mobile browser (Chrome Android, Safari iOS) — vì link mở từ MXH in-app browser cũng phải chạy.
- **Accessibility**: kế thừa §10.5.

---

# 11. DATA, RETENTION AND OBSERVABILITY

Kế thừa §11 SRS gốc:

- Data classification table giữ nguyên (public config / tenant settings / session token / conversation / usage).
- Retention: anonymous session expire; conversation theo TenantAI policy; không raw audio; không train model trên visitor chat mặc định.
- Metrics: giữ danh sách §11.3 gốc, thêm:
  - `assistant_link_open_total`, `assistant_link_chat_started_total`, `assistant_quota_exceeded_total`, `assistant_shop_paused_view_total`.
- Không PII/raw text trong metric labels.

---

# 12. ERROR HANDLING

Kế thừa bảng §12 SRS gốc, điều chỉnh message cho shop-facing:

| Code | User-facing (guest) | User-facing (owner) |
|---|---|---|
| `ASSISTANT_NOT_PUBLISHED` | "Trợ lý của shop chưa sẵn sàng." | "Trợ lý chưa được kích hoạt — bấm Kích hoạt để mở." |
| `ASSISTANT_PAUSED` | "Shop tạm ngưng trợ lý — vui lòng quay lại sau." | "Trợ lý đang tạm dừng." |
| `QUOTA_EXCEEDED` | "Trợ lý đã hết lượt phục vụ tháng này." | "Đã đạt giới hạn chi tiêu — nâng cấp để tiếp tục." |
| `SESSION_EXPIRED` | "Phiên trò chuyện hết hạn — bắt đầu lại." | — |
| `RATE_LIMITED` | "Bạn hỏi hơi nhanh — thử lại sau ít phút." | — |
| `SHOP_UNAVAILABLE` (AI/Metering down) | "Trợ lý đang bận — vui lòng thử lại." | — |

Các code còn lại (domain, mic, install...) kế thừa nguyên gốc.

---

# 13. IMPLEMENTATION ROADMAP AND GATES

## 13.1. P0 – Audit (NO FEATURE CODING)

Bắt buộc trả lời trước khi code:

1. TenantAI chat endpoint hiện tại: auth model, có public-ready chưa, cách signed tenant context hoạt động.
2. Gateway có facade pattern cho public session chưa; Account/Identity có anonymous session capability chưa.
3. **AI voice capability trong vanan-voice: xác nhận absent** → ghi thẳng vào decision register.
4. Metering: service code `ai.chat.message` đã tồn tại hay cần đăng ký; rate card mechanism.
5. **Assistant configuration sẽ nằm ở đâu** (SA-003): candidate = TenantAI admin surface, CoreHub config, hoặc Gateway — phải quyết định bằng evidence.
6. Knowledge source: TenantAI hiện ingest những gì; shop owner access path nào.
7. UI.Platform/Blazor conventions cho guest-facing page (link page không cần login — xác định host app phù hợp: KhachLink? page riêng?).
8. Domain/QR/link generation utilities hiện có.

Deliverables (kế thừa + điều chỉnh):

- `AS_IS_MODIFY_MISSING_CONFLICT.md`
- `API_CONTRACT_GAP.md`
- `ASSISTANT_HOST_DECISION.md` (mới — quyết định SA-003)
- `PRICING_AND_QUOTA_MODEL.md` (mới — quyết định SA-004/005/116)
- `SECURITY_THREAT_MODEL.md`
- `IMPLEMENTATION_TASK_CARDS.md`
- `P0_DECISION_REGISTER.md`

**Gate P0:** PO duyệt host decision + pricing model + scope trước khi P1 code.

## 13.2. P1 – Shop AI Assistant Core (Link/QR + text chat)

Scope:

- Assistant config + publish states.
- Hosted AI Link page (mobile-first) + QR.
- Public anonymous session + chat → Tenant AI.
- Metering integration + per-shop hard cap.
- Owner dashboard tối thiểu trong portal hiện có.
- Knowledge picker (chọn nguồn có sẵn + FAQ thủ công).
- Kill switch + audit.

Gate P1:

- Một shop pilot thật bật assistant → khách chat qua link trên mobile → nhận câu trả lời grounded.
- QR in được, scan mở đúng shop.
- Cross-tenant isolation tests pass (AT-008, T1 suite).
- Quota cap enforced; vượt cap không gọi LLM.
- Metering deny/failure đúng policy TenantAI.
- Không secret trong client artifacts.
- Link page chịu được bot/rate-limit test.
- Voice UI hoàn toàn ẩn.

## 13.3. P2 – Widget + Domain Verification

Scope: domain binding, DNS/meta/manual-review verify, widget SDK, widget dùng chung identity với link, health check.

Gate P2:

- Widget chạy trên ≥1 site test do shop sở hữu, đã verify.
- Domain spoofing tests pass.
- Unverified domain → reject an toàn; AI Link vẫn hoạt động.
- Widget failure không làm hỏng host page.

## 13.4. P3 – Installation Manager + Assisted Install + WordPress

Gate P3:

- Wizard non-technical hoàn chỉnh.
- Installation checker verify runtime thật.
- Operator scoped + audited; không password collection.
- WordPress plugin (nếu audit P0/P3 pass) cho non-technical owner.

## 13.5. Dependency order

```text
P0 Audit (host decision + pricing)
  |
  v
P1 Link/QR + text chat + metering cap   <-- HERO, ship trước
  |
  v
P2 Widget + domain verification         <-- chỉ khi có shop có website
  |
  v
P3 Installation Manager + WP plugin + Assisted ops
  |
  v
Future: voice AI adapter / extension / android (riêng SRS)
```

**P1 không bị block bởi widget. P2 không block bởi P3. Mỗi phase ship độc lập được.**

---

# 14. TESTING AND ACCEPTANCE

Kế thừa §14 SRS gốc. Acceptance scenarios điều chỉnh/thêm:

| Test ID | Scenario | Expected |
|---|---|---|
| AT-001→AT-005 | Như gốc (onboarding, link, QR, widget states) | Như gốc |
| AT-010 | Metering không verify được | Structured-only degradation; không unmetered LLM (giữ nguyên) |
| AT-011 | Link page/LLM down | Guest thấy trạng thái lịch sự, host page không ảnh hưởng |
| AT-026 (mới) | Shop đạt hard-cap quota | Guest thấy "hết lượt", KHÔNG có LLM call unmetered trong logs |
| AT-027 (mới) | Shop pause assistant | Link trả "shop tạm ngưng", session mới bị chặn |
| AT-028 (mới) | Guest spam messages | Rate limit kích hoạt trước khi tốn LLM token đáng kể |
| AT-029 (mới) | Owner revoke link | Link cũ 404/vô hiệu; assistant vẫn sống; link mới hoạt động |
| AT-030 (mới) | Domain xóa (P2) | Widget reject; AI Link vẫn chat bình thường |
| AT-031 (mới) | Manual review verify (P2) | Operator approve → domain verified; evidence lưu audit |
| AT-025 | Regression KhachLink/Gateway/ShopERP hiện có | Pass |

Definition of Done: kế thừa §14.3 nguyên vẹn.

---

# 15. DEVIN IMPLEMENTATION INSTRUCTIONS

Kế thừa §15 SRS gốc với điều chỉnh:

1. Đọc SRS này + SRS gốc (phần không bị override) + TenantAI SRS.
2. Inspect `9_TenantAI/vanan-ai`, `10_Voice/vanan-voice`, `8_Metering/vanan-metering`, Gateway, Account, UI.Platform, KhachLink.
3. **P0 trước, không feature code** — đặc biệt 2 deliverable mới: `ASSISTANT_HOST_DECISION.md` và `PRICING_AND_QUOTA_MODEL.md`.
4. Map mọi FR → EXISTING / MODIFY / MISSING / CONFLICT.
5. Không tạo service/entity trùng lặp; mọi thay đổi Domain theo governance.
6. Implement P1 chỉ sau PO approve P0; dừng ở mỗi gate.

Non-negotiable constraints: kế thừa §15.2 gốc nguyên vẹn (không rewrite services, không secrets client-side, không unmetered LLM, không fake stub...).

---

# 16. RISKS AND MITIGATIONS

Kế thừa bảng §16 SRS gốc + bổ sung:

| Risk | Impact | Mitigation |
|---|---|---|
| Shop kỳ vọng "chatbot biết bán hàng" nhưng knowledge nghèo | Trả lời kém → shop churn | Readiness checklist + gợi ý FAQ; pilot chọn shop có knowledge sạch (PO-001) |
| Visitor spam đốt quota của shop | Shop bất mãn, chi phí | Rate limit trước LLM, hard-cap, bot mitigation, cảnh báo usage dashboard |
| Link bị phát tán ngoài ý muốn (group/chat) | Abuse + lo lắng owner | Revoke/regenerate link; rate limit; per-link analytics |
| "Zero-code" bị hiểu là free | Kỳ vọng sai về giá | Định vị rõ: assistant là tính năng có giá (metering add-on) + assisted install là dịch vụ |
| Domain verify fail với SME | Widget adoption thấp | Manual review fallback; không block P1 vì link/QR vẫn đủ dùng |
| Guest chat = public attack surface | Cost/risk | Bot mitigation + rate limit là P1 MUST |
| Voice expectation gap | Khách hỏi "sao không nói được" | Không UI voice; FAQ sẵn trong assistant config nếu cần |

---

# 17. FUTURE CONSIDERATION (không cam kết)

Ghi nhận để không mất hướng, **không nằm trong v1.0 scope**:

- **AI Voice assistant** — khi có provider + adapter được verify và legal review.
- **Browser Extension** — nếu data chứng minh user cần "hỏi AI về trang đang xem".
- **Android Floating Assistant** — cùng lý do.
- **Multi-assistant per shop** (vd: 1 cho CSKH, 1 cho bán hàng) — khi có demand.
- **Cross-channel memory** (chat link ↔ widget ↔ sau này Zalo OA) — khi có authenticated identity.
- **Shopify/Wix apps** — theo platform review riêng.
- **Widget trên website không thuộc Vạn An** ("universal" đúng nghĩa) — chỉ xem xét nếu business case riêng được chứng minh; bản chất là bài toán khác.

---

# 18. OPEN DECISIONS FOR PRODUCT OWNER

| ID | Decision | Default recommendation |
|---|---|---|
| PO-001 | Pilot shop đầu tiên | Shop Vạn An hiện có, knowledge TenantAI sạch, owner nhiệt tình |
| PO-002 | AI Link domain | Subdomain Vạn An kiểm soát, HTTPS, sau DNS review |
| PO-008 | **Pricing model** — đã thu hẹp: tenant trả theo `ai.chat.message`; cần duyệt giá + hard-cap default theo gói | Rate card add-on "AI Assistant" trong Metering |
| PO-010 | Assisted install có thu phí? | Có — định giá như dịch vụ onboarding, sau khi đo workload pilot |
| PO-013 (mới) | Assistant có phải tính năng trả phí ngay P1 hay free trong pilot? | Free quota nhỏ trong pilot để đo adoption, thu phí từ P2 |
| PO-014 (mới) | Guest chat retention | Theo TenantAI policy hiện có |
| PO-015 (mới) | Zalo OA integration (share link trong OA auto-reply?) | Future — không block v1.0 |

Các PO còn lại của SRS gốc (PO-003→PO-007, PO-009, PO-011, PO-012) giữ nguyên, một số tự động resolve bởi các quyết định SA-xxx ở §4.

---

# 19. FINAL PRODUCT BASELINE

## 19.1. What P1 must achieve

Một chủ shop Vạn An không chuyên kỹ thuật phải có thể:

1. Đăng nhập portal hiện có.
2. Bật "Trợ lý AI" trong < 5 phút qua wizard.
3. Chọn knowledge đã có sẵn của shop.
4. Preview + kích hoạt.
5. Nhận AI Link + QR để gửi khách qua Zalo/Messenger/in tại quầy.
6. Thấy usage và chi phí trong dashboard.
7. Pause bất cứ lúc nào.

Một khách của shop phải có thể:

1. Mở link từ Zalo/Messenger/QR trên điện thoại.
2. Biết đang nói chuyện với shop nào.
3. Hỏi đáp text ngay, không cần app/tài khoản.
4. Nhận câu trả lời từ knowledge đã duyệt của shop.

## 19.2. Final architecture decision

**Một shop = một AI assistant = một TenantAI tenant context = một ví Metering. Kênh phân phối theo độ khó cài đặt: Link/QR trước, Widget sau, mọi thứ khác là future.**

- AI Link/QR là sản phẩm hero của P1.
- Website Widget là mở rộng tự nhiên cho shop có website (P2).
- Assisted installation là dịch vụ vận hành có thể monetize (P3).
- Voice/Extension/Android không cam kết trong v1.0.
- Tenant AI, Metering, Voice Service, Gateway, Account giữ nguyên vai trò — tái sử dụng qua contract, không rewrite.

**P1 success = một shop thật có khách thật chat với trợ lý riêng của shop đó qua link/QR, với chi phí đo được và kiểm soát được.**

---

**END OF SRS – VANAN SHOP AI ASSISTANT v1.0**
