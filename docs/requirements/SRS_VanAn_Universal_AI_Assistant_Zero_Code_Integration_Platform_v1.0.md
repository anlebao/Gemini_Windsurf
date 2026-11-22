# SOFTWARE REQUIREMENTS SPECIFICATION
# VANAN UNIVERSAL AI ASSISTANT & ZERO-CODE INTEGRATION PLATFORM
## SRS v1.0

| Thuộc tính | Giá trị |
|---|---|
| Sản phẩm | VanAn Universal AI Assistant |
| Tài liệu | Software Requirements Specification |
| Phiên bản | 1.0 |
| Ngày lập | 20/11/2026 |
| Trạng thái | Proposed – Baseline for review and implementation planning |
| Ưu tiên | P1 – Shared Ecosystem Platform |
| Định hướng | One AI Core, Multiple Surfaces, Zero-Code Onboarding |
| Đối tượng thực hiện | Devin / Engineering / QA / Product Owner |
| Ngôn ngữ | Vietnamese requirements; technical identifiers in English |

> **Mục tiêu cốt lõi:** Người dùng phổ thông và chủ website không cần biết lập trình vẫn có thể tạo, cài đặt và sử dụng một VanAn AI Assistant. Vạn An cung cấp trải nghiệm đa điểm chạm thông qua Website Widget, AI Link/QR, Browser Extension và Android Floating Assistant theo lộ trình có kiểm soát.

---

# 0. DOCUMENT CONTROL

## 0.1. Mục đích tài liệu

Tài liệu xác định yêu cầu nghiệp vụ, chức năng, kiến trúc logic, bảo mật, tích hợp, UX, vận hành, kiểm thử và lộ trình triển khai cho nền tảng VanAn Universal AI Assistant & Zero-Code Integration Platform.

Tài liệu này là lớp nền tảng đa giao diện (multi-surface platform), **không thay thế và không viết lại** các SRS/service hiện hữu:

- Vạn An Tenant AI (`9_TenantAI/vanan-ai`).
- Vạn An Voice Service (`10_Voice/vanan-voice`).
- Vạn An Metering (`8_Metering/vanan-metering`).
- Gateway, Account/Identity/Device Trust, CoreHub và các ứng dụng Vạn An hiện hữu.

## 0.2. Nguyên tắc đọc tài liệu

- **MUST**: bắt buộc để nghiệm thu.
- **SHOULD**: nên có; chỉ bỏ khi có lý do và được PO duyệt.
- **MAY**: tùy chọn.
- **P0**: khảo sát và xác minh trước khi code.
- **P1**: nền tảng và Website Widget + AI Link/QR.
- **P2**: Zero-Code Installation Manager và nền tảng plugin ưu tiên.
- **P3**: Browser Extension.
- **P4**: Android Floating Assistant và các tích hợp nền tảng mở rộng.

## 0.3. Phân biệt mức độ xác thực

Mọi nội dung mô tả hệ thống hiện hữu phải được Devin xác minh từ codebase và deployment thực tế. SRS này ghi lại các quyết định kiến trúc đã có trong tài liệu nền, nhưng không mặc định rằng mọi endpoint, API hoặc chức năng đã triển khai thành công.

---

# 1. EXECUTIVE SUMMARY

## 1.1. Bài toán

Hiện nay, việc tích hợp chatbot/voice assistant thường yêu cầu một hoặc nhiều thao tác kỹ thuật: copy JavaScript snippet, chỉnh HTML, cài plugin, cấu hình API hoặc thuê lập trình viên. Điều này là rào cản với hộ kinh doanh, SME và chủ website không chuyên công nghệ.

Người truy cập website cũng không muốn phải đăng ký nhiều tài khoản hoặc cài nhiều ứng dụng chỉ để hỏi AI.

## 1.2. Mục tiêu sản phẩm

Xây dựng một nền tảng có thể:

1. Cho phép doanh nghiệp tạo một AI Assistant từ quy trình hướng dẫn đơn giản.
2. Cho phép chủ website tích hợp widget mà không phải tự viết code.
3. Cho phép website không hỗ trợ nhúng vẫn dùng được thông qua AI Link/QR.
4. Cung cấp Browser Extension để người dùng chủ động gọi AI trên những trang được cấp quyền.
5. Cung cấp Android Floating Assistant ở giai đoạn sau, với quyền overlay rõ ràng.
6. Dùng chung một AI Core, cùng chính sách tenant, knowledge, metering và lịch sử hội thoại theo quyền.
7. Cho phép Vạn An hỗ trợ cài đặt thay khách hàng (Assisted Installation).
8. Quản lý nhiều website, nhiều assistant, nhiều widget trong cùng một tài khoản doanh nghiệp.

## 1.3. Định vị

**VanAn AI – Universal AI Assistant Platform**

Một nền tảng trợ lý AI có thể được gọi từ nhiều điểm chạm số, không giới hạn trong website hay ứng dụng do Vạn An sở hữu.

## 1.4. Product principles

- Zero-code for merchant; không đồng nghĩa không có công việc kỹ thuật phía Vạn An.
- One AI Core, Multiple Surfaces.
- Tenant Knowledge First; không trộn tri thức giữa các doanh nghiệp.
- Consent before access: người dùng chủ động cho phép trước khi đọc nội dung trang, bật microphone hoặc thực hiện hành động.
- Secure by default; quyền hạn tối thiểu.
- AI hỗ trợ tư vấn, không tự ý xác nhận giao dịch, thay đổi giá/tồn kho hoặc tạo đơn.
- Không tuyên bố khả năng tích hợp mà nền tảng bên thứ ba chưa cho phép.
- Tái sử dụng service hiện có; không tạo microservice trùng chức năng.

---

# 2. SCOPE

## 2.1. In Scope

### P1 – Core platform

- Universal Assistant identity and configuration.
- Tenant / Assistant / Surface / Widget identity.
- Website Widget SDK (JavaScript loader + isolated UI).
- Hosted AI Assistant Link.
- QR code for AI Link and website widget entry.
- Basic zero-code onboarding wizard.
- Basic operator-assisted installation workflow.
- Integration adapter to existing Tenant AI.
- Voice capability discovery and adapter contract.
- Metering, quotas, feature flags and usage reporting.
- Basic admin console for managing assistant and website binding.
- Consent, privacy and security controls.
- Public anonymous visitor sessions with abuse protection.

### P2 – Zero-Code Installation Manager

- Website platform detection (best-effort).
- Domain ownership verification.
- WordPress plugin (subject to P0 compatibility audit).
- Shopify app/integration (subject to platform API and app review requirements).
- Guided/manual installation for custom websites.
- Installation health checker.
- Knowledge setup wizard.
- Preview, publish, pause, uninstall and rollback.
- Assisted installation work queue for Vạn An operators.

### P3 – Browser Extension

- Browser extension for supported desktop browsers.
- User-triggered floating panel on permitted websites.
- Optional selected-text/page-context handoff with explicit user action.
- Per-site permission and disable controls.
- No silent page scraping or hidden microphone use.

### P4 – Android Floating Assistant

- Android application with user-granted display-over-other-apps permission.
- Floating bubble and compact panel.
- Explicit user gesture to open microphone or share screen/page context.
- Foreground service and OS-compliant lifecycle.
- Device-specific compatibility and battery tests.

## 2.2. Out of Scope in v1.0

- Automatic injection into every website without owner approval or user-installed extension.
- Automatic modification of third-party websites without permission.
- Direct embedding into Facebook or TikTok native mobile applications.
- Unauthorized scraping of Facebook/TikTok live comments, video streams, private messages or account data.
- Bypassing CSP, CORS, browser permission models, platform restrictions or app review.
- Background microphone recording or always-listening mode.
- Automatic screen recording or continuous page capture.
- AI autonomous checkout, payment, order confirmation, price changes or inventory updates.
- AI agent actions on third-party accounts without explicit authorization and platform-supported APIs.
- PSTN/SIP/group calling and call-center features.
- Replacing the existing Tenant AI knowledge/policy engine.
- Replacing the existing Voice Service.
- Building a new LLM or training a separate model for each tenant.
- Guaranteeing compatibility with all CMS, page builders, browsers, apps and social platforms.

---

# 3. USERS, ACTORS AND USE CASES

## 3.1. Actors

| Actor | Description | Main permissions |
|---|---|---|
| Platform Admin | Vạn An platform administrator | Global configuration, platform policy, suspend/revoke |
| Vạn An Operator | Nhân viên hỗ trợ onboarding | Assist setup, verify installation, manage assigned tenants |
| Tenant Owner | Chủ doanh nghiệp/website | Create assistant, manage domain, knowledge, widget, usage |
| Tenant Staff | Nhân viên được phân quyền | Review conversations and limited assistant settings |
| Website Visitor | Khách truy cập website | Use public assistant under tenant policy |
| Universal AI User | Người dùng cá nhân | Use own assistant session via Link/Extension/Android |
| Developer/Integrator | Technical partner | Install SDK/API under scoped credentials |
| Third-party Platform | WordPress, Shopify, browser, social platform | Host integration surface under its own rules |

## 3.2. Primary user journeys

### UC-01 – Chủ website tự tạo trợ lý

1. Chủ website mở VanAn AI onboarding.
2. Đăng nhập/đăng ký Vạn An Account.
3. Chọn “Tạo trợ lý AI cho website”.
4. Nhập tên doanh nghiệp, website và thông tin liên hệ.
5. Xác minh quyền sở hữu domain hoặc chọn AI Link trước.
6. Chọn mẫu giao diện, ngôn ngữ, persona và tính năng.
7. Cung cấp tri thức: website URL được phép, FAQ, PDF, bảng giá, danh mục.
8. Xem preview.
9. Chọn “Kích hoạt”.
10. Hệ thống kiểm tra và hiển thị trạng thái đã sẵn sàng hoặc hướng dẫn bước tiếp theo.

### UC-02 – Chủ website không biết kỹ thuật cần Vạn An hỗ trợ

1. Chọn “Vạn An cài đặt giúp tôi”.
2. Xác nhận domain và thông tin liên hệ.
3. Chọn phương thức hỗ trợ (hẹn lịch/nhân viên liên hệ).
4. Hệ thống tạo Installation Job.
5. Operator xác minh nền tảng, quyền truy cập và phương thức triển khai.
6. Chủ website cấp quyền cần thiết theo phương thức an toàn.
7. Operator cài plugin/snippet hoặc hướng dẫn nhà cung cấp website.
8. Installation Checker kiểm tra.
9. Chủ website duyệt preview và kích hoạt.

**Security rule:** Không yêu cầu khách gửi mật khẩu quản trị website qua chat, email hoặc form. Ưu tiên tài khoản collaborator có thời hạn/quyền tối thiểu hoặc khách tự thao tác theo hướng dẫn. Nếu phải dùng quyền quản trị, quy trình phải được bảo vệ và phê duyệt riêng.

### UC-03 – Website không thể cài widget

1. Chủ website tạo AI Assistant.
2. Hệ thống tạo AI Link duy nhất.
3. Hệ thống tạo QR tương ứng.
4. Chủ website chia sẻ link/QR qua Zalo, Facebook, TikTok bio, email hoặc in tại cửa hàng.
5. Khách mở link và trò chuyện với AI.
6. Assistant vẫn giữ tenant identity và policy của doanh nghiệp.

### UC-04 – Khách truy cập website có widget

1. Khách mở website đã tích hợp.
2. Widget tải cấu hình public đã được ký/kiểm tra.
3. Widget hiển thị nút nổi.
4. Khách mở widget, gửi câu hỏi.
5. Backend tạo/tiếp tục anonymous session.
6. Tenant AI xử lý theo tenant context và knowledge permission.
7. Widget hiển thị câu trả lời.
8. Nếu khách chủ động bật voice, hệ thống yêu cầu quyền microphone.

### UC-05 – Người dùng gọi AI trên website bất kỳ bằng Extension

1. Người dùng cài Extension từ cửa hàng chính thức.
2. Đăng nhập Vạn An hoặc dùng chế độ guest giới hạn.
3. Người dùng mở website.
4. Nhấn icon Extension hoặc floating bubble.
5. Extension chỉ đọc URL/title hoặc nội dung được người dùng chọn/chia sẻ theo permission.
6. Nội dung được gửi tới AI theo một lần consent cụ thể.
7. AI trả lời trong side panel/overlay.
8. Người dùng có thể xóa ngữ cảnh đã chia sẻ và thu hồi quyền site.

### UC-06 – Android Floating Assistant

1. Người dùng cài VanAn AI Android.
2. Đăng nhập.
3. Hệ thống giải thích quyền “Display over other apps”.
4. Người dùng tự bật quyền trong Android Settings.
5. Bubble nổi xuất hiện.
6. Người dùng chạm bubble để mở panel.
7. Người dùng nhập câu hỏi hoặc chủ động nhấn microphone.
8. Khi app bị đóng hoặc quyền bị thu hồi, bubble phải dừng theo quy định OS.

---

# 4. PRODUCT AND ARCHITECTURE DECISIONS

## 4.1. Decision register

| ID | Quyết định | Trạng thái |
|---|---|---|
| D-001 | One AI Core, Multiple Surfaces | Chốt định hướng |
| D-002 | Tenant AI (`9_TenantAI/vanan-ai`) là AI knowledge/chat engine độc lập | Kế thừa SRS Tenant AI |
| D-003 | Voice Service (`10_Voice/vanan-voice`) là module/service độc lập, attachable by contract | Kế thừa Voice Master Plan |
| D-004 | Metering dùng `8_Metering/vanan-metering`; không tự tạo wallet/quota engine mới | Chốt theo kiến trúc hiện hữu |
| D-005 | Gateway là lớp xác thực/điều phối public session và tenant resolution theo contract đã xác minh | P0 xác minh chi tiết |
| D-006 | CoreHub là nguồn dữ liệu nghiệp vụ; không để Widget/Extension truy cập database trực tiếp | Bắt buộc |
| D-007 | AI Link/QR là fallback phổ quát khi website không thể nhúng | Chốt |
| D-008 | P1 ưu tiên Website SDK + AI Link/QR; không chờ Extension/Android | Chốt đề xuất |
| D-009 | WordPress ưu tiên plugin; Shopify và CMS khác theo adapter, API và quy trình review | Đề xuất |
| D-010 | Browser Extension chỉ hoạt động theo quyền người dùng và site permission | Bắt buộc |
| D-011 | Android overlay là ứng dụng riêng, không phải widget web; user phải tự cấp quyền OS | Bắt buộc |
| D-012 | Facebook/TikTok native app không được coi là nơi có thể nhúng widget trực tiếp | Bắt buộc |
| D-013 | AI voice conversation không được mặc định là chức năng đã có trong Voice Service gọi người-với-người | P0 xác minh và adapter riêng |
| D-014 | Không rewrite Gateway, Tenant AI, Voice Service, Metering hoặc Account | Bắt buộc |

## 4.2. Logical architecture

```text
                 USER / MERCHANT / VISITOR
                           |
       +-------------------+-------------------+
       |                   |                   |
 Website Widget        AI Link / QR       Browser Extension
       |                   |                   |
       +-------------------+-------------------+
                           |
                 Android Floating App
                    (P4 - later)
                           |
                 VANAN CLIENT SDK LAYER
       - UI / Session / Consent / Context Adapter
       - Public Config / Feature Capability
       - Chat Transport / Voice Adapter
                           |
                    API GATEWAY
       - Authentication / Anonymous Session
       - Tenant Resolver / Domain Binding
       - Rate Limit / Abuse Protection
       - Signed Context / Authorization
       - Public API Facade
                           |
        +------------------+------------------+
        |                  |                  |
   Tenant AI Service  Voice Service       Metering
   9_TenantAI         10_Voice            8_Metering
   Chat/RAG/Policy    Voice capability    Quota/Usage
        |                  |                  |
        +------------------+------------------+
                           |
                     COREHUB / DATA
       - Tenant / Account / Domain binding
       - Assistant configuration reference
       - Audit / Usage references
       - Business data through approved APIs
```

## 4.3. Service boundaries

### A. Universal Assistant Platform (new logical capability)

Owns:

- Assistant configuration and publication workflow.
- Website/domain bindings.
- Widget configuration and public manifests.
- Installation jobs and installation status.
- AI Link and QR mapping.
- Surface registration and capabilities.
- Consent records/consent references.
- Public session bootstrap.
- Usage dashboard aggregation.
- Operator onboarding workflow.

Must not own:

- LLM prompts/policy engine/RAG implementation.
- Voice media/signaling implementation.
- Metering wallet/quota ledger.
- Account identity source of truth.
- ShopERP product/order source of truth.

### B. Tenant AI

Reuse `9_TenantAI/vanan-ai` per its current SRS:

- PostgreSQL + pgvector.
- Tenant-isolated knowledge.
- Intent Router, Policy Engine, retrieval, Answer Validator.
- Gemini provider abstraction as currently specified.
- Structured-first, vector-second.
- Public chat only retrieves approved Public knowledge and allowed structured fields.
- Knowledge freshness and usage permission filters.
- AI must not create orders or modify price/inventory.
- Existing metering policy, including structured-only degradation when metering check cannot be verified after cache expiry, must be preserved.

### C. Voice Service

Reuse `10_Voice/vanan-voice` and its current Master Plan/SRS:

- Standalone attachable module.
- WebRTC P2P first, Coturn fallback.
- SignalR signaling inside Voice Service; audio media does not traverse Gateway/CoreHub.
- Existing authentication and verified-context rules.
- Same-tenant and default-deny call rules for human-to-human calls.
- Do not introduce SFU/SIP/LiveKit into this platform release.

**Important distinction:** Existing Voice Service v1.1 describes human-to-human audio calling and explicitly excludes AI voice agents/transcription. Universal Assistant must define a separate `IAiVoiceSessionAdapter` contract only after P0 audit confirms available AI audio capabilities. Do not route AI speech through a human-to-human call workflow or claim AI real-time voice is available until an actual supported implementation passes tests.

### D. Metering

Reuse `8_Metering/vanan-metering`.

Candidate service codes must be approved and registered in rate card before production:

- `ai.chat.message`
- `ai.voice.session`
- `ai.voice.minute`
- `ai.widget.active` (only if commercial model requires it)
- `ai.extension.request` (only if separately metered)

Do not meter both a parent event and child events in a way that double-charges the same usage. Define idempotency keys and billing unit per event.

---

# 5. DOMAIN MODEL

## 5.1. Core entities

| Entity | Purpose | Key fields |
|---|---|---|
| TenantAssistant | AI assistant owned/managed by tenant | Id, TenantId, Name, Status, ConfigVersion |
| AssistantSurface | Where assistant is exposed | Id, AssistantId, SurfaceType, Enabled |
| WebsiteDomain | Website/domain binding | Id, TenantId, AssistantId, Hostname, VerificationStatus |
| WidgetInstance | A widget deployment on a domain | Id, AssistantId, DomainId, PublicWidgetKey, ConfigVersion |
| AssistantLink | Hosted AI URL | Id, AssistantId, Slug, Status |
| QrAsset | QR mapping | Id, AssistantLinkId, Format, Version |
| InstallationJob | Guided or assisted installation | Id, TenantId, DomainId, Method, Status, AssignedOperatorId |
| InstallationCheck | Installation verification history | Id, InstallationJobId, CheckType, Result, CheckedAt |
| PublicVisitorSession | Anonymous visitor session reference | Id, AssistantId, SurfaceId, CreatedAt, ExpiresAt |
| ConsentRecord | Consent and purpose record/reference | Id, SessionId, Purpose, PolicyVersion, GrantedAt, RevokedAt |
| SurfaceRegistration | Extension/app/device installation | Id, UserId, SurfaceType, DeviceRef, Status |
| AssistantUsageSummary | Aggregated usage | TenantId, AssistantId, Period, Metric, Quantity |
| AuditEvent | Security and administrative audit | Actor, Action, Resource, Timestamp, MetadataAllowlist |

Implementation must inspect existing identity, tenant, audit and configuration entities first. Do not create duplicate Tenant/User tables.

## 5.2. Identifier rules

- `TenantId`: resolved by trusted server-side tenant/domain mapping, never accepted as authoritative from public client input.
- `AssistantId`: stable identifier for one assistant.
- `DomainId`: one verified website hostname binding.
- `WidgetId`: stable widget deployment identity.
- `PublicWidgetKey`: public identifier only; not a secret and cannot authorize admin operations.
- `AssistantLink`: unguessable or sufficiently high-entropy slug; revocable.
- `SessionId`: opaque random identifier; no PII embedded.
- `InstallationJobId`: UUIDv7 if supported by existing conventions.
- Secrets/API keys must never be placed in public HTML, JavaScript, QR code or Extension package.

## 5.3. Relationship constraints

- One tenant may own multiple assistants.
- One assistant may be exposed on multiple surfaces.
- One assistant may bind to multiple verified domains only if tenant policy permits.
- A widget must map to exactly one active assistant configuration at a time.
- A public domain binding must not be shared across tenants without explicit platform-level approval and conflict resolution.
- Public sessions are scoped to assistant + surface + tenant context.
- Conversation continuity across surfaces is opt-in and requires authenticated identity or a secure transfer mechanism; anonymous sessions must not be silently merged.

---

# 6. FUNCTIONAL REQUIREMENTS

## 6.1. Assistant onboarding and management

| ID | Requirement | Priority |
|---|---|---|
| FR-001 | System MUST provide a guided wizard to create an assistant without coding | P1 |
| FR-002 | Wizard MUST ask only business-essential information first; advanced settings remain optional | P1 |
| FR-003 | Tenant Owner MUST be able to set assistant name, greeting, language, tone/persona and brand appearance | P1 |
| FR-004 | System MUST assign TenantId from authenticated owner context, not client-supplied value | P1 |
| FR-005 | Assistant MUST support Draft, Preview, Published, Paused and Suspended states | P1 |
| FR-006 | Publishing MUST create an immutable configuration version or equivalent audit-able version reference | P1 |
| FR-007 | Owner MUST be able to roll back to a previously published configuration | P2 |
| FR-008 | System MUST provide a test conversation before publishing | P1 |
| FR-009 | System MUST show a simple readiness checklist with actionable language | P1 |
| FR-010 | Operator MUST be able to assist a tenant without receiving unrestricted tenant credentials | P1 |

## 6.2. Website domain onboarding

| ID | Requirement | Priority |
|---|---|---|
| FR-020 | Owner MUST be able to enter a website URL | P1 |
| FR-021 | System MUST normalize hostname and reject malformed URLs | P1 |
| FR-022 | System MUST support domain verification using at least one secure method: DNS TXT or a well-known verification file/meta token | P2 |
| FR-023 | Verification token MUST be random, scoped, expiring and revocable | P2 |
| FR-024 | System MUST distinguish unverified, pending, verified, blocked and removed domains | P2 |
| FR-025 | Widget MUST NOT activate on an unverified domain unless an explicit safe preview/test mode is used | P2 |
| FR-026 | Domain verification MUST prevent one tenant from claiming another tenant's domain | P2 |
| FR-027 | System MUST support subdomain policy (exact host by default; wildcard only with explicit approval) | P2 |
| FR-028 | System MUST detect common website platform/CMS on a best-effort basis and clearly show confidence/unknown | P2 |
| FR-029 | Scanner MUST NOT access localhost, private IP ranges, cloud metadata endpoints or internal network resources | P2 |
| FR-030 | Scanner MUST defend against SSRF, redirects to private networks, DNS rebinding and oversized responses | P2 |

## 6.3. Website Widget SDK

| ID | Requirement | Priority |
|---|---|---|
| FR-040 | System MUST provide a lightweight JavaScript loader for supported websites | P1 |
| FR-041 | Widget MUST render a floating launcher and responsive chat panel | P1 |
| FR-042 | Widget MUST support configurable position, color, logo/avatar, greeting, language and open/closed default state | P1 |
| FR-043 | Widget MUST support mobile and desktop layouts | P1 |
| FR-044 | Widget MUST isolate its styles and DOM from host website as far as practical, preferably Shadow DOM or isolated iframe architecture after compatibility review | P1 |
| FR-045 | Widget MUST not break host page layout, navigation, forms or checkout | P1 |
| FR-046 | Widget MUST load asynchronously and not block host page rendering | P1 |
| FR-047 | Widget MUST have bounded bundle size, documented performance budget and lazy-load AI/voice modules | P1 |
| FR-048 | Widget MUST handle network failures and show retry state without losing unsent user input | P1 |
| FR-049 | Widget MUST provide a visible close/minimize control | P1 |
| FR-050 | Widget MUST not request microphone permission on initial page load | P1 |
| FR-051 | Widget MUST request microphone permission only after explicit user action | P1 |
| FR-052 | Widget MUST provide keyboard navigation, focus management and accessible labels | P1 |
| FR-053 | Widget MUST respect reduced-motion preferences and avoid obstructing essential page controls | P1 |
| FR-054 | Widget MUST support a kill switch per assistant/domain and a platform-wide emergency disable | P1 |
| FR-055 | Widget public configuration MUST contain no API secrets, service keys or privileged credentials | P1 |
| FR-056 | Widget MUST identify its assistant/domain binding using public identifiers validated server-side | P1 |
| FR-057 | Widget MUST not trust a client-supplied TenantId or arbitrary AssistantId without server-side domain mapping | P1 |
| FR-058 | Widget MUST support configuration version caching with bounded TTL and invalidation/revocation | P1 |
| FR-059 | Widget MUST support uninstall/disable instructions and remove its own event listeners/resources | P2 |
| FR-060 | Widget MUST not silently collect host page content; context sharing requires explicit user action and disclosure | P1 |

### 6.3.1. Widget integration modes

1. **JavaScript snippet** – default universal method for custom websites when owner or authorized developer can paste a snippet.
2. **CMS plugin/app** – recommended no-code method where platform supports approved installation.
3. **Assisted installation** – Vạn An operator installs using least-privilege access or guides owner.
4. **AI Link/QR** – fallback when website modification is impossible.

The phrase “one-click install” may only be used for a platform integration where a real authorized app/plugin installation flow exists. For arbitrary custom websites, do not promise one-click installation.

### 6.3.2. Example public embed contract

The exact production URL and attribute names must be finalized after repository audit. The conceptual contract may look like:

```html
<script
  src="https://static.vanan.example/assistant-widget/v1/loader.js"
  data-widget-id="PUBLIC_WIDGET_ID"
  async>
</script>
```

This snippet is illustrative, not a production endpoint. No secret is allowed in the snippet.

## 6.4. AI Link and QR

| ID | Requirement | Priority |
|---|---|---|
| FR-070 | System MUST create a hosted assistant URL for every published assistant | P1 |
| FR-071 | Link MUST use HTTPS and an opaque, revocable identifier | P1 |
| FR-072 | System MUST generate downloadable QR in PNG and SVG or an equivalent vector format | P1 |
| FR-073 | Owner MUST be able to copy/share the link and download/print the QR | P1 |
| FR-074 | Link MUST work without website integration | P1 |
| FR-075 | Link MUST preserve assistant tenant policy and knowledge boundaries | P1 |
| FR-076 | Owner MUST be able to pause/revoke link access | P1 |
| FR-077 | Link MUST have abuse protection and rate limits | P1 |
| FR-078 | Link MUST display clear business identity so users know which assistant they are speaking with | P1 |
| FR-079 | Link MUST support optional lead/contact capture only with clear notice and user action | P2 |
| FR-080 | QR MUST NOT encode API keys, user credentials or personal data | P1 |

## 6.5. Zero-Code Installation Manager

| ID | Requirement | Priority |
|---|---|---|
| FR-090 | System MUST provide a step-by-step installation wizard | P2 |
| FR-091 | Wizard MUST explain technical steps in plain Vietnamese and avoid requiring the merchant to understand code | P2 |
| FR-092 | System SHOULD detect WordPress and provide a compatible plugin installation path | P2 |
| FR-093 | Shopify integration MUST use approved Shopify app/API flow and comply with platform requirements | Later P2/P3 |
| FR-094 | Wix and other hosted builders MUST use only officially supported embed/app mechanisms | Later |
| FR-095 | Custom websites MUST receive either a snippet or assisted installation route | P2 |
| FR-096 | Installation Manager MUST create an InstallationJob with status, owner, assigned operator and audit history | P2 |
| FR-097 | Installation Manager MUST show installation steps, not merely “failed” | P2 |
| FR-098 | Installation Checker MUST validate widget loader availability, configuration fetch, domain binding, runtime initialization and test request | P2 |
| FR-099 | Checker MUST distinguish a network/CSP/ad-blocker issue from invalid widget credentials or server failure | P2 |
| FR-100 | Checker MUST NOT claim “installed successfully” based only on snippet presence | P2 |
| FR-101 | Owner MUST preview widget before public activation | P2 |
| FR-102 | Owner MUST be able to pause, disable or uninstall integration | P2 |
| FR-103 | Operator actions MUST be scoped to assigned tenants and logged | P2 |
| FR-104 | Operator MUST NOT view tenant passwords or secrets | P2 |
| FR-105 | System MUST provide rollback/uninstall guidance for each supported platform | P2 |
| FR-106 | If platform detection is uncertain, system MUST say “Chưa xác định được nền tảng” and provide manual/assisted options | P2 |

### 6.5.1. Installation methods

| Platform | Preferred method | Fallback | Notes |
|---|---|---|---|
| WordPress | Official VanAn AI plugin | Snippet / assisted | Plugin must be signed/versioned and compatible with supported WP/PHP versions |
| Shopify | Official Shopify app or approved app embed | Hosted AI Link | Subject to app review, permissions and theme/app-extension rules |
| Wix | Official app/embed mechanism if available | AI Link / assisted | Do not promise access to unsupported site internals |
| Custom HTML/PHP/ASP.NET | JS snippet | Assisted installation | Customer or authorized operator must have site edit rights |
| Facebook Fanpage | AI Link in allowed profile/button/message paths; approved Messenger integration where authorized | QR/link | No direct widget injection into Facebook native app |
| TikTok | AI Link in bio/allowed destinations; approved platform APIs where available | QR/link | No direct widget injection into TikTok native app or unauthorized Live scraping |
| No website | AI Link + QR | VanAn hosted page | First-class supported scenario |

## 6.6. Browser Extension

| ID | Requirement | Priority |
|---|---|---|
| FR-110 | Extension MUST be distributed through official browser extension stores where applicable | P3 |
| FR-111 | Extension MUST request minimum permissions needed | P3 |
| FR-112 | Extension MUST provide a user-triggered action to open assistant | P3 |
| FR-113 | Extension MUST support per-site allow/deny settings | P3 |
| FR-114 | Extension MUST not read page body by default | P3 |
| FR-115 | Page context MUST be user-selected or explicitly approved per action/session | P3 |
| FR-116 | Extension MUST clearly show which URL/title/content is about to be shared | P3 |
| FR-117 | Extension MUST not inject into browser internal pages, extension stores, payment/password pages or restricted origins | P3 |
| FR-118 | Extension MUST not capture password fields, payment data, cookies, localStorage or authentication tokens | P3 |
| FR-119 | Extension MUST not execute arbitrary code returned by AI/backend | P3 |
| FR-120 | Extension MUST support sign-out, revoke session and uninstall cleanup | P3 |
| FR-121 | Extension MUST use a restrictive Content Security Policy and avoid remote executable scripts | P3 |
| FR-122 | Extension MUST disclose permissions and data practices before install | P3 |

## 6.7. Android Floating Assistant

| ID | Requirement | Priority |
|---|---|---|
| FR-130 | Android app MUST explain why overlay permission is needed before directing user to OS Settings | P4 |
| FR-131 | App MUST function without overlay permission using its normal app screen or notification/deep link alternative | P4 |
| FR-132 | Floating bubble MUST be movable, dismissible and hideable | P4 |
| FR-133 | Overlay MUST not impersonate system UI or obscure security prompts | P4 |
| FR-134 | Microphone MUST be activated only after explicit user action and OS permission | P4 |
| FR-135 | App MUST show visible microphone/voice active state | P4 |
| FR-136 | App MUST stop capture when user ends session, app is stopped or permission is revoked | P4 |
| FR-137 | App MUST follow Android foreground-service and background execution rules applicable to target SDK | P4 |
| FR-138 | App MUST provide an accessible route to revoke overlay/microphone permissions | P4 |
| FR-139 | App MUST not promise overlay support on iOS | P4 |
| FR-140 | Battery, memory and OS compatibility tests MUST be documented | P4 |

## 6.8. Chat and Voice Experience

| ID | Requirement | Priority |
|---|---|---|
| FR-150 | All surfaces MUST use the same Assistant identity/configuration and Tenant AI policy | P1 |
| FR-151 | Chat requests MUST be routed to the existing Tenant AI service through an authenticated server-side integration | P1 |
| FR-152 | Public client MUST never call Tenant AI using privileged service keys | P1 |
| FR-153 | Assistant MUST show a clear loading, success, unavailable and retry state | P1 |
| FR-154 | Conversation history MUST be scoped by tenant, assistant and authorized user/session | P1 |
| FR-155 | Cross-surface history sync MUST require authenticated identity or explicit secure session transfer | P2 |
| FR-156 | AI responses MUST preserve source/grounding metadata supported by Tenant AI | P1 |
| FR-157 | Stale structured data MUST trigger existing Tenant AI freshness behavior | P1 |
| FR-158 | Assistant MUST not create orders, charge payments or change inventory/prices | P1 |
| FR-159 | Where product suggestions exist, user MUST continue through the existing approved cart/order flow | P1 |
| FR-160 | Voice UI MUST remain hidden/disabled if no verified AI voice adapter is available | P1 |
| FR-161 | Voice activation MUST require explicit user gesture and microphone permission | P1 |
| FR-162 | Voice session MUST show connection state and a visible end/mute control | P1 when enabled |
| FR-163 | If voice fails, text chat MUST remain available | P1 |
| FR-164 | No raw audio recording/storage by default | P1 |
| FR-165 | Any future transcription/recording feature requires separate policy, consent, retention and legal review | Future |

### 6.8.1. Voice integration boundary

There are two distinct use cases:

1. **Human-to-human calling:** use existing `vanan-voice` contracts and permission model.
2. **User speaking to AI:** requires an AI audio session capability (speech input, speech output, turn-taking, interruption handling and session lifecycle).

Do not assume that a WebRTC P2P human call service automatically provides AI voice conversation. During P0, Devin MUST inspect current Voice Service code and any actual AI speech provider integration. If no complete AI audio capability exists, implement chat-first and keep voice as a documented future adapter.

## 6.9. Context and consent

| ID | Requirement | Priority |
|---|---|---|
| FR-170 | System MUST separate basic session metadata from optional page content | P1 |
| FR-171 | Website Widget MUST only pass tenant/domain/assistant context verified by backend | P1 |
| FR-172 | Browser Extension MUST request explicit action before sharing page text or selected content | P3 |
| FR-173 | Android app MUST not read other apps' screen contents by default | P4 |
| FR-174 | Consent MUST identify purpose: chat, page context, microphone, contact capture or action execution | P1 |
| FR-175 | Consent MUST be revocable where technically applicable | P1 |
| FR-176 | User MUST be able to use basic text chat without granting microphone or page-context permission | P1 |
| FR-177 | Consent record MUST include policy version and timestamp or a secure reference to the platform consent ledger | P1 |
| FR-178 | Consent MUST not be bundled into unrelated marketing consent | P1 |
| FR-179 | Widget MUST provide a privacy notice link configured for the tenant/platform | P1 |

## 6.10. Admin and Operator Console

| ID | Requirement | Priority |
|---|---|---|
| FR-180 | Tenant Owner MUST see assistant status and active surfaces | P1 |
| FR-181 | Tenant Owner MUST see AI Link and QR | P1 |
| FR-182 | Tenant Owner MUST see setup readiness and installation status | P1 |
| FR-183 | Tenant Owner MUST be able to pause assistant/widget/link | P1 |
| FR-184 | Tenant Owner MUST see usage summary and quota state | P1 |
| FR-185 | Operator MUST see installation queue and assigned jobs | P2 |
| FR-186 | Operator MUST be able to run installation checks without reading private conversations unless separately authorized | P2 |
| FR-187 | Platform Admin MUST be able to revoke widget keys, domains, links and surface registrations | P1 |
| FR-188 | Admin actions MUST be audited | P1 |
| FR-189 | Console MUST not expose LLM/provider secrets to tenant users or operators | P1 |
| FR-190 | Tenant Owner MUST be able to export basic usage information subject to privacy policy | P2 |

---

# 7. API AND INTEGRATION CONTRACTS

> Endpoint names below are logical contracts, not assertions that these routes already exist. Devin MUST map them to current API conventions and produce an EXISTING / MODIFY / MISSING / CONFLICT matrix before implementation.

## 7.1. Public configuration

### GET `/v1/public/widgets/{publicWidgetKey}/config`

Purpose:
- Return public display configuration and capability flags.
- Validate origin/domain binding server-side.
- Return no secrets.

Example response:

```json
{
  "widgetId": "uuid",
  "assistantId": "uuid",
  "display": {
    "title": "Trợ lý Vạn An",
    "greeting": "Xin chào, tôi có thể giúp gì?",
    "position": "bottom-right",
    "theme": "light"
  },
  "capabilities": {
    "chat": true,
    "voice": false,
    "pageContext": false,
    "actions": false
  },
  "configVersion": 3,
  "privacyNoticeUrl": "https://example.invalid/privacy"
}
```

## 7.2. Public session bootstrap

### POST `/v1/public/assistants/{assistantId}/sessions`

Purpose:
- Create/continue an anonymous visitor session.
- Resolve tenant from published assistant/link/domain mapping.
- Apply rate limits and abuse controls.

Requirements:
- Client cannot choose authoritative TenantId.
- Session token must be short-lived and scoped.
- Session token must not be accepted by admin APIs.
- Response must disclose allowed capabilities.

## 7.3. Chat

### POST `/v1/public/assistant-sessions/{sessionId}/messages`

Logical request:

```json
{
  "messageId": "uuidv7",
  "text": "Tôi muốn tìm hiểu sản phẩm này",
  "context": {
    "surface": "website_widget",
    "pageUrl": "https://merchant.example/product/123"
  }
}
```

Rules:
- `pageUrl` is metadata only unless page-context consent and supported context sharing are enabled.
- Do not accept arbitrary tenant identity from request.
- Idempotency uses `messageId` scoped to session.
- Gateway/facade forwards a signed tenant context to Tenant AI according to its existing SRS.
- Tenant AI enforces all knowledge/policy checks; client prompt instructions are untrusted.

## 7.4. Assistant configuration

Authenticated routes (logical):

- `POST /v1/assistants`
- `GET /v1/assistants/{assistantId}`
- `PATCH /v1/assistants/{assistantId}`
- `POST /v1/assistants/{assistantId}/preview`
- `POST /v1/assistants/{assistantId}/publish`
- `POST /v1/assistants/{assistantId}/pause`
- `POST /v1/assistants/{assistantId}/rollback`

Authorization:
- Owner/authorized staff only.
- TenantId resolved from account/session and membership.
- Admin override requires privileged scope and audit.

## 7.5. Domain and installation

Logical routes:

- `POST /v1/assistants/{assistantId}/domains`
- `POST /v1/domains/{domainId}/verification/start`
- `POST /v1/domains/{domainId}/verification/check`
- `DELETE /v1/domains/{domainId}`
- `POST /v1/installations`
- `GET /v1/installations/{installationJobId}`
- `POST /v1/installations/{installationJobId}/checks`
- `POST /v1/installations/{installationJobId}/complete`

## 7.6. AI Link and QR

Logical routes:

- `POST /v1/assistants/{assistantId}/links`
- `POST /v1/links/{linkId}/revoke`
- `GET /v1/assistants/{assistantId}/qr`
- `POST /v1/assistants/{assistantId}/qr/regenerate`

QR points to a hosted HTTPS URL. QR itself carries no credential.

## 7.7. Voice adapter

Logical interface (not necessarily a public HTTP endpoint):

```text
IAiVoiceSessionAdapter
  GetCapabilities(assistantId, surface)
  StartSession(userSession, consentReference)
  SendAudioFrameOrStream(sessionId, audioPayload)
  ReceiveAssistantAudio(sessionId)
  StopSession(sessionId)
  GetSessionStatus(sessionId)
```

Before implementation, determine whether the selected speech provider supports streaming, interruption/barge-in, latency and Vietnamese speech quality. Never pass long-lived provider secrets to client apps. Do not store raw audio unless a separately approved feature requires it.

## 7.8. Service-to-service security

- Public client → Gateway/public facade using short-lived scoped session token.
- Gateway/facade → Tenant AI using existing caller key + scopes + HMAC-signed tenant context, per Tenant AI SRS.
- Voice client/service → Voice Service using its existing auth/validation contract.
- Service → Metering using existing caller credentials and idempotency conventions.
- No Widget/Extension/Android direct database access.
- No browser-held service-to-service keys.
- No new auth provider or duplicate user identity store.

---

# 8. USER INTERFACE REQUIREMENTS

## 8.1. Merchant onboarding screens

| Screen ID | Screen | Required controls |
|---|---|---|
| UI-01 | Welcome / choose use case | Create website assistant, create AI Link, request setup help |
| UI-02 | Business profile | Business name, website URL, business type, contact |
| UI-03 | Assistant appearance | Name, avatar/logo, color, greeting, language, position |
| UI-04 | Knowledge setup | Website URL, FAQ, PDF/doc upload, product/catalog source |
| UI-05 | Capabilities | Chat on/off, voice availability, page context, contact capture |
| UI-06 | Preview | Desktop/mobile preview, sample conversation |
| UI-07 | Installation choice | Plugin, snippet, assisted setup, AI Link/QR |
| UI-08 | Installation progress | Plain-language steps, status, help action |
| UI-09 | Success / share | AI Link, QR download, embed instructions |
| UI-10 | Assistant dashboard | Status, usage, surfaces, quota, pause/resume |
| UI-11 | Domain management | Add, verify, remove domain |
| UI-12 | Installation job detail | Job status, operator, checks, timeline |

## 8.2. Visitor widget states

- Launcher closed.
- Panel open / welcome.
- Session initializing.
- Waiting for response.
- Answer displayed.
- Network unavailable.
- Quota/assistant unavailable.
- Voice permission explanation.
- Voice connecting / active / failed.
- Privacy notice.
- User minimized or closed.

## 8.3. UX language

Avoid technical language such as:
- “Paste JavaScript”.
- “Configure API key”.
- “Set CORS”.
- “Configure DNS record” without explanation.

Prefer:
- “Thêm trợ lý vào website”.
- “Xác minh website thuộc quyền quản lý của bạn”.
- “Vạn An có thể hỗ trợ cài đặt”.
- “Dùng đường link này nếu website chưa hỗ trợ cài trợ lý”.
- “Cho phép microphone để trò chuyện bằng giọng nói”.

Advanced technical details may be shown in an expandable section for developers.

---

# 9. SECURITY, PRIVACY AND COMPLIANCE

## 9.1. Threat model

Threats include:
- Domain spoofing and widget-key theft.
- Cross-tenant data leakage.
- Public endpoint abuse, spam and cost exhaustion.
- Prompt injection and malicious page content.
- SSRF through website scanner.
- XSS and DOM/style interference.
- Extension permission abuse.
- Secret leakage in JS, QR, logs or source maps.
- Unauthorized microphone/page access.
- Operator privilege abuse.
- Malicious third-party script supply chain.
- Replay and session fixation.
- Unauthorized social-platform data collection.

## 9.2. Mandatory controls

| ID | Control |
|---|---|
| SEC-001 | All public and service endpoints use HTTPS |
| SEC-002 | Tenant identity is resolved and validated server-side |
| SEC-003 | Strict tenant isolation across sessions, knowledge, logs, cache and usage |
| SEC-004 | Public widget key is an identifier, never an authorization secret |
| SEC-005 | Short-lived scoped session tokens; rotate/revoke as appropriate |
| SEC-006 | Rate limits by IP, domain, assistant, session and account |
| SEC-007 | Abuse detection and emergency kill switch |
| SEC-008 | No API/provider/service secrets in client artifacts |
| SEC-009 | Content Security Policy and output encoding; sanitize rendered markdown/HTML |
| SEC-010 | Page content is untrusted input; prompt injection cannot override backend policy |
| SEC-011 | No arbitrary execution of AI-generated code/actions |
| SEC-012 | Domain ownership verification before production widget activation |
| SEC-013 | Website scanner blocks private/reserved IP ranges and validates every redirect |
| SEC-014 | Extension permission minimization; never collect passwords, cookies or auth tokens |
| SEC-015 | Microphone requires explicit user gesture and OS/browser permission |
| SEC-016 | No background recording or always-on microphone |
| SEC-017 | Consent is purpose-specific, versioned and revocable |
| SEC-018 | Audit privileged configuration and operator actions |
| SEC-019 | Data retention/deletion rules documented and enforced |
| SEC-020 | Dependencies, plugin packages and extension builds are versioned and scanned |
| SEC-021 | Widget and Extension cannot access CoreHub/ShopERP database directly |
| SEC-022 | Cross-tenant domain collision is rejected |
| SEC-023 | AI Link can be revoked without deleting the assistant |
| SEC-024 | Public errors must not expose internal stack traces, tenant secrets or infrastructure details |

## 9.3. Privacy and Vietnam compliance

The implementation team must conduct a legal review against applicable Vietnamese personal data protection and cybersecurity requirements in force at deployment time, including Law 91/2025/QH15 and relevant implementing instruments where applicable.

At minimum:
- Publish a clear privacy notice and identify data controller/processor roles.
- Define purpose and legal basis for each personal-data processing activity.
- Minimize collection and retention.
- Provide contact/rights request channels.
- Document processors and cross-border transfers where applicable.
- Separate service consent from marketing consent.
- Define treatment of conversation content, voice data, page context and contact details.
- Avoid ingesting private or confidential website content into public assistant knowledge.
- Do not use visitor conversations for model training by default.

This SRS is not a legal opinion. Counsel/PO must approve the final privacy notices, retention schedule and data-processing agreements before production.

---

# 10. NON-FUNCTIONAL REQUIREMENTS

## 10.1. Performance

| ID | Requirement | Target |
|---|---|---|
| NFR-001 | Widget loader must be asynchronous | MUST |
| NFR-002 | Widget must not block Largest Contentful Paint | MUST |
| NFR-003 | Initial launcher payload should be minimized and lazy-load panel/chat code | SHOULD |
| NFR-004 | Widget initialization after loader download | Target p95 < 1.5s on a representative broadband test, excluding network/server cold start |
| NFR-005 | Chat API overhead excluding LLM/provider latency | Target p95 < 500ms |
| NFR-006 | UI must remain responsive on low-end Android devices | MUST |
| NFR-007 | Voice latency targets are defined only after AI voice provider selection and measurement | P0/P4 |

Performance targets must be measured on a documented test matrix, not assumed.

## 10.2. Availability and resilience

- Public config and hosted AI Link must have health checks and graceful error states.
- Widget failure must not break the host website.
- AI service unavailable → show fallback message; do not fake a successful answer.
- Metering deny/failure must preserve Tenant AI's existing policy; no silent unmetered LLM usage.
- Configuration cache must have revocation/expiry behavior.
- Widget kill switch must take effect within a defined bounded cache TTL.
- Failed installation checks must be retryable and provide diagnostic IDs.

## 10.3. Scalability

Design for gradual rollout:

| Stage | Indicative scale | Main validation |
|---|---:|---|
| Pilot | 10–50 tenants | Correct tenant isolation, installation success, usage/cost baseline |
| Stage 1 | 100 tenants | Widget performance, API rate limits, metering |
| Stage 2 | 1,000 tenants | Cache, queue, operator workload, domain/config scale |
| Stage 3 | 10,000 tenants | Horizontal scaling only when metrics justify it |

Do not provision infrastructure for 10,000 tenants before pilot data. Establish scaling gates using real measurements.

## 10.4. Compatibility matrix

| Surface | Initial support target | Limitations |
|---|---|---|
| Website Widget | Current Chrome, Edge, Firefox, Safari; Android Chrome; iOS Safari | Must test CSP, blockers, in-app browsers |
| Hosted AI Link | Modern mobile/desktop browsers | Browser must support required web APIs |
| WordPress Plugin | Supported WP/PHP versions to be defined after P0 | Plugin/theme conflicts possible |
| Shopify | Approved app/embed flow | Subject to Shopify platform policies and review |
| Browser Extension | Chrome/Edge desktop first | Browser store permission and policy requirements |
| Android Overlay | Android versions to be defined at P4 | OEM battery/background restrictions; no iOS overlay |

## 10.5. Accessibility

- Keyboard operation for widget.
- Screen-reader labels.
- Contrast and focus indicators.
- Touch targets suitable for mobile.
- Do not convey state by color alone.
- Respect browser/OS reduced-motion preferences.

---

# 11. DATA, RETENTION AND OBSERVABILITY

## 11.1. Data classification

| Data | Classification | Handling |
|---|---|---|
| Public widget config | Public | No secrets |
| Tenant assistant settings | Tenant confidential | Owner/admin access |
| Domain verification token | Secret | Hash/encrypt; short expiry |
| Anonymous session token | Secret | Short-lived, scoped |
| Conversation content | Personal/tenant data depending context | Access control and retention |
| Page context | Potentially personal/confidential | Explicit consent, minimize, short retention |
| Audio stream | Sensitive content | Ephemeral processing; no recording by default |
| Usage aggregate | Internal/business | Tenant-scoped reports |
| Audit events | Internal/security | Restricted access |

## 11.2. Retention

Retention periods must align with the existing Tenant AI conversation policy and legal review. Do not create a conflicting retention policy in this platform.

- Widget technical diagnostics: short retention, configurable.
- Installation job history: retain only as needed for operations/audit.
- Domain verification tokens: expire and invalidate after use.
- Anonymous sessions: expire automatically.
- Consent records: retain according to approved compliance schedule.
- Audio: no raw audio persistence by default.
- Page context: do not retain beyond the conversation purpose unless separately disclosed and approved.

## 11.3. Metrics

Minimum metrics:
- `assistant_created_total`
- `assistant_published_total`
- `widget_config_requests_total`
- `widget_load_success_total`
- `widget_load_failure_total`
- `public_session_created_total`
- `ai_chat_request_total`
- `ai_chat_latency_ms`
- `ai_chat_error_total`
- `installation_job_total`
- `installation_success_rate`
- `domain_verification_failure_total`
- `ai_link_open_total`
- `qr_open_total`
- `extension_session_total`
- `voice_session_start_total`
- `voice_session_failure_total`
- `metering_denied_total`
- `abuse_rate_limited_total`

Never place raw conversation text, API keys, session tokens or personal data in metric labels.

---

# 12. ERROR HANDLING

| Code | Meaning | User-facing behavior |
|---|---|---|
| `ASSISTANT_NOT_PUBLISHED` | Assistant not active | “Trợ lý hiện chưa được kích hoạt.” |
| `DOMAIN_NOT_VERIFIED` | Website not verified | Show owner setup guidance; visitor gets unavailable state |
| `ORIGIN_NOT_ALLOWED` | Origin not bound to widget | Block request and log security event |
| `SESSION_EXPIRED` | Session expired | Offer to start a new conversation |
| `RATE_LIMITED` | Too many requests | Explain temporary limit and retry timing |
| `METERING_DENIED` | Usage quota unavailable/denied | Preserve Tenant AI deny behavior |
| `AI_SERVICE_UNAVAILABLE` | Tenant AI unavailable | Offer retry; never fabricate answer |
| `VOICE_NOT_AVAILABLE` | No verified AI voice capability | Keep text chat available |
| `MIC_PERMISSION_DENIED` | User denied microphone | Continue text chat |
| `INSTALLATION_CHECK_FAILED` | Integration check failed | Show exact step and diagnostic ID |
| `PLATFORM_UNSUPPORTED` | Platform path unsupported | Offer AI Link/QR or assisted setup |
| `CONSENT_REQUIRED` | User has not approved context/mic | Explain and wait for explicit action |

---

# 13. IMPLEMENTATION ROADMAP AND GATES

## 13.1. P0 – Codebase and platform audit (NO FEATURE CODING)

**Goal:** Establish an evidence-based baseline and prevent duplicate systems.

Required audit:
1. Existing Gateway public API and authentication patterns.
2. Account/Identity/Device Trust and anonymous/public session capabilities.
3. Tenant AI API contract, caller scopes, signed tenant context, metering integration and conversation storage.
4. Voice Service actual code and whether AI speech capability exists.
5. Metering service codes, quota and usage ingestion contract.
6. Current UI.Platform and Blazor/PWA conventions.
7. Existing website/domain mapping and KhachLink storefront domain handling.
8. Existing QR/link generation utilities.
9. Existing admin/operator authorization and audit.
10. Existing deployment, nginx, static asset hosting, CDN and secrets conventions.

Deliverables:
- `AS_IS_MODIFY_MISSING_CONFLICT.md`
- `SYSTEM_BOUNDARY_AND_SEQUENCE_DIAGRAMS.md`
- `API_CONTRACT_GAP.md`
- `SECURITY_THREAT_MODEL.md`
- `IMPLEMENTATION_TASK_CARDS.md`
- `P0_DECISION_REGISTER.md`

**Gate P0:** PO reviews and approves actual service boundaries, endpoints, data ownership and release scope before P1 coding.

## 13.2. P1 – Universal Assistant Core + Website Widget + AI Link/QR

Scope:
- Assistant configuration and publish state.
- Public session bootstrap.
- Website Widget SDK.
- Hosted AI Link.
- QR generation.
- Chat integration with Tenant AI.
- Usage/metering integration.
- Consent and privacy baseline.
- Basic tenant dashboard and kill switch.
- Basic operator-assisted flow (manual queue acceptable).

Gate P1:
- A tenant can create/publish an assistant.
- AI Link works on mobile and desktop.
- QR opens the correct assistant.
- Widget works on at least one Vạn An-owned/controlled website and one external test website.
- Cross-tenant isolation tests pass.
- No secret in browser artifacts.
- Metering deny/failure behavior verified.
- Widget failure does not break host website.
- Text chat works; voice is disabled unless verified adapter exists.

## 13.3. P2 – Zero-Code Installation Manager

Scope:
- Domain verification.
- CMS detection.
- WordPress plugin.
- Installation checker.
- Owner setup wizard.
- Operator queue and scoped assisted installation.
- Knowledge setup UX.
- Pause/rollback/uninstall.

Gate P2:
- Non-technical owner can complete a supported WordPress installation following wizard only.
- Installation checker verifies actual runtime, not just snippet presence.
- Operator access is scoped and audited.
- Domain spoofing tests pass.
- Rollback and uninstall verified.

Shopify and other builders are separate compatibility tasks; do not block P2 core on every platform.

## 13.4. P3 – Browser Extension

Scope:
- Chrome/Edge extension.
- User-triggered side panel/floating panel.
- Per-site permissions.
- Selected text/page context handoff.
- Account session and revoke.
- Store listing compliance.

Gate P3:
- Extension works on a defined allowlist of ordinary websites.
- No access to passwords/cookies/payment fields.
- No page body collection without explicit action.
- Extension CSP and package scan pass.
- Permission disclosure approved.

## 13.5. P4 – Android Floating Assistant

Scope:
- Android app.
- Overlay permission onboarding.
- Floating bubble and compact chat.
- Explicit voice activation only if supported.
- OS lifecycle and battery behavior.
- Accessibility and OEM compatibility.

Gate P4:
- User can grant/revoke overlay permission.
- Overlay can be moved, hidden and dismissed.
- No microphone use without user action.
- App stops capture correctly.
- Tested on representative Android devices and OS versions.
- Play Store policy review completed if distributed through Play.

## 13.6. Dependency order

```text
P0 Audit
  |
  v
P1 Core + Widget + AI Link/QR
  |
  v
P2 Zero-Code Manager + WordPress
  |
  +------------------+
  |                  |
  v                  v
P3 Browser         Platform-specific
Extension         integrations (Shopify etc.)
  |
  v
P4 Android Floating Assistant
```

Do not start P3/P4 implementation before P1 security/session contracts are stable.

---

# 14. TESTING AND ACCEPTANCE

## 14.1. Required test layers

- Unit tests: configuration, domain binding, session, consent, capability resolver.
- Integration tests: Gateway/public facade, Tenant AI, Metering, Voice adapter.
- Security tests: tenant spoofing, origin spoofing, session replay, XSS, SSRF, rate-limit bypass.
- Browser tests: desktop/mobile, CSP, blocked third-party scripts, navigation.
- E2E tests: onboarding → publish → widget/AI Link → chat → usage.
- Regression tests: existing KhachLink, ShopERP, Booking and Gateway functionality.
- Performance tests: loader overhead, API latency, concurrent sessions.
- Accessibility tests: keyboard, focus, screen reader basics.
- Manual tests: microphone permissions, Android overlay and browser store installation.

## 14.2. Acceptance scenarios

| Test ID | Scenario | Expected result |
|---|---|---|
| AT-001 | Merchant creates assistant without coding | Assistant draft created |
| AT-002 | Merchant publishes AI Link | Link opens correct assistant |
| AT-003 | QR scanned on Android | Hosted assistant opens |
| AT-004 | Widget loaded on verified domain | Launcher appears |
| AT-005 | Widget loaded on unverified domain | Request rejected safely |
| AT-006 | Client sends forged TenantId | Server ignores/rejects forged tenant |
| AT-007 | Visitor asks a question | Tenant AI returns grounded response |
| AT-008 | Tenant A attempts to access Tenant B session | Access denied |
| AT-009 | Metering denies usage | Existing AI deny policy applied |
| AT-010 | Metering cannot be verified | Existing structured-only degradation/refusal policy applied; no unmetered LLM call |
| AT-011 | Widget script fails | Host website remains usable |
| AT-012 | Visitor opens voice without clicking mic | No microphone permission requested |
| AT-013 | Visitor denies microphone | Text chat remains usable |
| AT-014 | Owner pauses assistant | Widget/Link stops serving new sessions within cache bound |
| AT-015 | Domain is removed | Widget requests from that origin rejected |
| AT-016 | Installation checker sees snippet but JS runtime fails | Installation marked failed, not successful |
| AT-017 | Operator accesses unassigned tenant | Access denied and audited |
| AT-018 | Extension opens on password/payment page | Context capture disabled |
| AT-019 | Extension page context not consented | No page body sent |
| AT-020 | Android overlay permission revoked | Overlay stops; normal app remains usable |
| AT-021 | AI voice adapter unavailable | Voice UI hidden/disabled |
| AT-022 | AI attempts to create order | Action rejected; existing order flow remains authoritative |
| AT-023 | SSRF scanner URL points to private IP | Request blocked |
| AT-024 | QR/link revoked | New session cannot be created |
| AT-025 | Existing KhachLink chat regression suite | All baseline tests pass |

## 14.3. Definition of Done

A phase is complete only when:
- Code matches approved architecture and existing conventions.
- No duplicate identity, tenant, metering or AI engine is introduced.
- Unit/integration/E2E tests pass.
- Security tests for that phase pass.
- Runtime verification is performed against deployed build where applicable.
- Monitoring and rollback instructions exist.
- Documentation and project state are updated.
- No fake/stub production behavior is presented as complete.
- Any unresolved item is recorded as a blocker, not silently bypassed.

---

# 15. DEVIN IMPLEMENTATION INSTRUCTIONS

## 15.1. Mandatory workflow

1. Read this SRS fully.
2. Read current Tenant AI SRS and inspect `9_TenantAI/vanan-ai`.
3. Read Voice Service SRS and `voice-service-master-plan.md`; inspect `10_Voice/vanan-voice`.
4. Inspect Metering, Gateway, Account/Identity, UI.Platform, KhachLink and deployment conventions.
5. Produce P0 audit deliverables before feature coding.
6. Map every requirement to EXISTING / MODIFY / MISSING / CONFLICT.
7. Identify all actual API contracts; do not invent endpoints and claim they exist.
8. Propose minimal data schema and migration plan.
9. Submit P0 findings for PO approval.
10. Implement P1 only after approval; stop at each gate for evidence review.

## 15.2. Non-negotiable constraints

- Do not rewrite Gateway.
- Do not rewrite Tenant AI.
- Do not rewrite Voice Service.
- Do not rewrite Metering.
- Do not create duplicate account, tenant, permission or wallet systems.
- Do not place privileged keys in browser/mobile clients.
- Do not let public clients access databases.
- Do not trust TenantId, AssistantId, domain or role asserted by untrusted client without server validation.
- Do not bypass domain verification for production widget.
- Do not scrape social platform data outside approved APIs and permissions.
- Do not claim direct widget embedding inside Facebook/TikTok native apps.
- Do not request microphone permission on page load.
- Do not record audio by default.
- Do not create orders/payments or mutate business data from AI.
- Do not introduce NATS/Redis/SFU/LiveKit or new infrastructure without measured need and explicit gate approval.
- Do not use EF `EnsureCreated` in production where existing migration conventions apply.
- Do not silently change existing database schema.
- Do not use stubs or mocked success as production completion.
- Preserve existing TDD/E2E/guard-check/project-state workflow.
- Every infrastructure change must have rollback procedure.

## 15.3. Required deliverables

### P0
- Codebase audit report.
- Existing/Modify/Missing/Conflict matrix.
- Service boundary diagram.
- API contract mapping.
- Security and privacy threat model.
- P1 task cards and estimates.
- PO decision register.

### P1
- Assistant configuration/publication module.
- Widget SDK and loader.
- Hosted AI Link and QR.
- Public session and chat facade.
- Tenant AI integration.
- Metering integration.
- Basic tenant dashboard.
- Admin/operator kill switch.
- Unit/integration/E2E/security tests.
- Deployment, monitoring and rollback guide.

### P2
- Domain verification.
- Installation Manager.
- WordPress plugin.
- Installation checker.
- Operator-assisted installation queue.
- Plugin compatibility and security tests.

### P3/P4
- Extension and Android deliverables listed in their respective phase gates.

---

# 16. RISKS AND MITIGATIONS

| Risk | Impact | Mitigation |
|---|---|---|
| “Zero-code” interpreted as universal automatic install | Merchant expectation mismatch | Separate plugin-supported, snippet, assisted and Link/QR methods; communicate honestly |
| Third-party platforms block injection | Widget cannot appear in native app | Use approved API/link/QR/extension paths |
| Tenant context spoofing | Cross-tenant data leak | Server-side resolution, signed context, isolation tests |
| Public widget abuse | Unexpected LLM cost | Metering, quotas, rate limits, bot detection, kill switch |
| Widget conflicts with host website | Merchant site regression | Shadow DOM/iframe, async loader, compatibility suite |
| CSP/ad blockers prevent loader | Widget not displayed | Diagnostics and AI Link fallback |
| Domain ownership cannot be proven | Unauthorized assistant binding | DNS/file verification or assisted proof review |
| Browser Extension permissions too broad | Privacy/store rejection | Minimal permissions and user-triggered page context |
| Android OEM restrictions | Overlay unreliable | Compatibility matrix and clear limitations |
| AI voice not supported by existing Voice Service | False feature claim / rework | Separate AI voice adapter; chat-first until verified |
| Operator handles customer credentials | Account compromise | No password collection; collaborator access, least privilege |
| Conflicting retention policies | Compliance risk | Reuse Tenant AI retention where relevant; central legal review |
| Overengineering before product validation | High cost | P1 Link/QR + Widget pilot before Extension/Android |

---

# 17. OPEN DECISIONS FOR PRODUCT OWNER

The following decisions must be recorded during P0/P1 planning; they are not permission to bypass security controls.

| ID | Decision | Default recommendation |
|---|---|---|
| PO-001 | First pilot customer category | Existing Vạn An F&B/SME tenant with usable approved knowledge |
| PO-002 | Public AI Link URL/domain | Use a dedicated Vạn An-controlled HTTPS subdomain after DNS/deployment review |
| PO-003 | Anonymous conversation retention | Reuse current Tenant AI approved retention; do not create a conflicting default |
| PO-004 | Guest user login requirement | Text chat may start anonymously with strict limits; login only for continuity/personalized actions |
| PO-005 | WordPress supported versions | Define after compatibility test |
| PO-006 | Shopify launch timing | Separate app approval and platform review |
| PO-007 | AI voice provider and protocol | P0 audit; no provider assumption in this SRS |
| PO-008 | Usage pricing and rate card | Approve service codes and unit economics before production billing |
| PO-009 | Domain verification methods | DNS TXT primary; file/meta token secondary |
| PO-010 | Operator-assisted SLA and pricing | Define after pilot workload measurement |
| PO-011 | Public conversation review permissions | Tenant role matrix and privacy review |
| PO-012 | Cross-surface conversation continuity | Authenticated user or explicit secure transfer only |

---

# 18. FINAL PRODUCT BASELINE

## 18.1. What the first release must achieve

A non-technical business owner must be able to:

1. Register/login.
2. Create a business AI assistant.
3. Provide basic business knowledge.
4. Preview and publish the assistant.
5. Obtain a shareable AI Link and QR without installing anything.
6. Add a widget to a supported website through a simple, documented or assisted process.
7. See whether the assistant is active and how much it is being used.
8. Pause the assistant at any time.

A website visitor must be able to:

1. Open the website or AI Link.
2. Start text chat without installing an app.
3. Receive answers grounded in the tenant's approved knowledge.
4. Choose whether to grant microphone/page-context permissions.
5. Continue using text chat when voice is unavailable.

## 18.2. Final architecture decision

**One AI Core – Multiple Surfaces – One Tenant Policy – Controlled Permissions.**

- Website Widget is the first embedded surface.
- AI Link/QR is the universal no-install fallback.
- Installation Manager removes technical barriers for merchants.
- Browser Extension is a user-installed companion, not a way to bypass website permissions.
- Android Floating Assistant is a later native surface, not a web widget.
- Tenant AI, Voice Service and Metering remain independent services and are reused through explicit contracts.

**P1 success is not “support every website and every social app”. P1 success is proving that a non-technical merchant can publish a useful AI assistant and that visitors can use it reliably through both a website widget and an AI Link/QR.**

---

**END OF SRS – VANAN UNIVERSAL AI ASSISTANT & ZERO-CODE INTEGRATION PLATFORM v1.0**
