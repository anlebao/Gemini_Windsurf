# VAN AN DIGITAL-FIRST, HUMAN-CENTERED

## Vision & Codebase Capability Gap Assessment Brief for Devin

**Document type:** Strategic Vision + Engineering Audit Brief\
**Version:** 1.0\
**Date:** 2026-11-27\
**Audience:** Devin, Engineering Team, Product Architecture\
**Purpose:** Use this vision as the strategic reference to inspect the
actual Vạn An codebase and produce an evidence-based inventory of
existing, incomplete, missing, duplicated, or misaligned modules and
features.

------------------------------------------------------------------------

# PART I --- STRATEGIC VISION

## 1. Vision statement

### VẠN AN --- DIGITAL-FIRST, HUMAN-CENTERED

**Số hóa sự hiện diện. Thông minh hóa giao tiếp. Kết nối giá trị thực.**

As people increasingly extend their presence, communication, work,
commerce, and relationships into digital spaces, Vạn An builds the
infrastructure that enables individuals, household businesses, SMEs, and
communities to establish a digital presence, communicate, connect,
transact, and grow.

Technology and AI should reduce barriers, reduce operational burdens,
and expand opportunities. People remain at the center: they are the
owners of decisions, relationships, trust, and value creation.

**Vạn An does not replace the physical world. Vạn An connects the
physical world with digital spaces to create new economic
opportunities.**

Vietnamese interpretation:

> Trong một thế giới mà con người ngày càng mở rộng sự hiện diện và hoạt
> động trên không gian số, Vạn An xây dựng hạ tầng để mỗi cá nhân, hộ
> kinh doanh và doanh nghiệp nhỏ có thể hiện diện, giao tiếp, kết nối,
> kinh doanh và phát triển trên môi trường số.
>
> Công nghệ và AI giúp giảm rào cản, giảm gánh nặng vận hành và mở rộng
> cơ hội. Con người vẫn là trung tâm, là chủ thể tạo ra giá trị và quyết
> định hướng đi của hệ sinh thái.
>
> Vạn An không thay thế thế giới vật lý. Vạn An kết nối thế giới vật lý
> với không gian số để tạo ra những cơ hội kinh tế mới.

## 2. Strategic premise

The strategic premise is **Digital-first, Physical-connected** --- not
Digital-only.

A growing share of discovery, comparison, communication,
decision-making, booking, purchasing, and customer support can begin or
happen in digital spaces. However, many services and products are
ultimately fulfilled in the physical world.

Examples: - A customer discovers a nearby restaurant online, asks
questions through AI, places an order, and collects or receives food
physically. - A customer discovers a salon, checks services and prices,
asks for advice, books an appointment, and visits the shop. - A customer
contacts a print/design business digitally, submits a brief and files,
receives a quote, approves the design, and receives physical printed
products. - A customer contacts a hotel digitally, asks about rooms,
books a stay, and experiences the service physically.

Vạn An must connect the complete journey rather than digitize only one
isolated step.

## 3. What Vạn An is building

Vạn An should be understood as **digital economic infrastructure and a
regenerative ecosystem for people, household businesses, SMEs, and
communities**.

It should not be defined merely as: - A collection of disconnected
software products. - A POS or shop-management application. - A chatbot
product. - A social network attempting to replace existing platforms. -
A marketplace attempting to compete head-on with every major
marketplace. - A clone of Manychat, Facebook, Shopee, TikTok, or Google.

Vạn An should connect existing digital channels and physical businesses
through a coherent infrastructure layer, while owning or controlling the
appropriate business capabilities: tenant identity and configuration,
business data, AI interaction, workflow orchestration, commerce
processes, customer relationships, and ecosystem services.

The ecosystem must be modular, interoperable, secure, tenant-aware, and
capable of evolving without forcing every capability into one monolithic
application.

## 4. Core strategic principles

1.  **Digital-first, Physical-connected:** Digital interaction may begin
    the journey; physical fulfillment and human service remain
    essential.
2.  **Human-centered:** AI augments people; it does not remove human
    accountability, judgment, empathy, or control.
3.  **Every business deserves a digital presence:** A small shop should
    be able to establish and operate a useful digital presence without
    needing a large IT team.
4.  **Every tenant has its own business identity and context:** Each
    shop's AI and workflows must respect tenant boundaries, data
    permissions, business rules, and approved knowledge.
5.  **Conversation can lead to action:** AI should not stop at answering
    questions. With permission and reliable integrations, it should help
    customers discover, inquire, book, order, and access services.
6.  **Omnichannel, not channel-dependent:** Customers may arrive from
    websites, PWA, QR codes, social platforms, messaging channels, or
    other approved integrations. No single channel should define the
    entire customer relationship.
7.  **Human handoff is a first-class capability:** Customers must be
    able to reach the shop owner or staff through chat, voice, or video
    where supported. Staff should receive context and conversation
    history.
8.  **Open integration, selective ownership:** Integrate with
    established platforms through official APIs where practical. Do not
    rebuild commodity platforms without a clear strategic reason.
9.  **Reusable ecosystem infrastructure:** Identity, tenant management,
    authorization, audit, metering, notifications, integration, and data
    services should be reusable across products.
10. **Trust and security by design:** Privacy, consent, data
    minimization, tenant isolation, access control, auditability,
    resilience, and recovery are foundational.
11. **Progressive implementation:** Build a coherent, working foundation
    and validate it with real tenants before scaling complexity.
12. **No artificial completeness:** Do not mark a feature complete
    because a UI, stub, mock, placeholder, or happy-path demo exists.

------------------------------------------------------------------------

# PART II --- TARGET CAPABILITY MODEL

The following is a **target capability model**, not a claim that these
modules already exist or are fully implemented. Devin must verify every
capability against the actual codebase, runtime behavior, tests,
configuration, and deployment artifacts.

## 5. Digital People & Digital Businesses

The ecosystem should support real people and real businesses as
participants in digital economic activity.

Potential capabilities to verify: - Individual/customer identity and
profile. - Business/tenant identity and profile. - Tenant onboarding and
verification states. - Staff, owner, operator, collaborator, supplier,
CTV, and customer roles. - Membership and relationships between people
and businesses. - Consent, privacy preferences, communication
preferences, and data permissions. - Account trust, device association,
and secure account recovery. - Business profile, service area, operating
hours, products, services, policies, and contact methods. - Digital
presence pages or storefront entry points. - Tenant lifecycle:
onboarding, activation, suspension, migration, and closure.

Do not assume every role or identity model should be implemented in one
service. Identify ownership boundaries and dependencies.

## 6. Digital Presence

Each tenant should be able to establish a useful and consistent digital
presence.

Potential capabilities: - Public business profile and digital
storefront. - Business identity, branding, logo, images, and content. -
Product/service catalogs and structured attributes. - Menu, price list,
availability, service descriptions, and policies. - Location, service
area, opening hours, and contact options. - Search-engine-friendly
public pages and metadata. - QR entry points and shareable links. -
Tenant-controlled content and publication workflow. - Synchronization of
relevant business information across approved channels. - Accessibility,
mobile responsiveness, and performance.

## 7. Van An AI Interaction Layer

AI interaction is a strategic layer, not merely a chatbot widget.

### 7.1 Customer-facing AI Assistant

The primary purpose is to let **customers communicate with the shop
owner or shop staff**, with AI assisting the interaction. It is not an
internal admin chatbot for shop owners or staff.

Potential capabilities: - Customer-facing chat interface. - Voice-call
and video-call entry points where supported. - AI greeting, intent
detection, question answering, and conversation guidance. -
Tenant-specific knowledge base and approved business context. -
Product/service discovery and recommendations constrained to the
tenant's actual catalog and rules. - Context-aware FAQ and policy
answers. - Conversation history and continuity across supported
channels. - Human handoff to owner/staff. - Staff assignment, queue,
availability, and escalation. - Clear indication when the customer is
interacting with AI versus a human. - Consent and disclosure where
required. - Safe fallback when confidence or data quality is
insufficient. - Multilingual capability where justified. - Abuse
prevention, rate limits, and conversation controls.

### 7.2 Tenant isolation and context security

Verify whether the implementation enforces: - Tenant identity derived
from trusted server-side context, not arbitrary client claims. -
Authenticated and authorized access to tenant resources. - Signed or
otherwise verifiable tenant context where service-to-service trust
requires it. - Strict tenant-scoped retrieval for prompts, knowledge,
catalog, CRM, orders, and conversation history. - No cross-tenant data
leakage through retrieval, caches, logs, embeddings, analytics, or
background jobs. - Input/output validation and prompt-injection defenses
appropriate to the architecture. - Secrets management, rotation, and
separation of credentials. - Auditable access to sensitive business and
customer data.

### 7.3 Human communication

The vision includes direct customer communication with a real person: -
Text chat with owner/staff. - Voice calls, including the planned P2P
WebRTC direction where technically suitable. - Video calls where
supported. - Signaling, STUN/TURN, call setup, reconnect, network
changes, and failure handling. - Human availability and routing. - Call
quality metrics and operational observability. - Escalation from AI chat
to human chat/call. - Conversation and call records subject to consent,
retention, and privacy policy.

Do not assume that voice/video is complete merely because a call screen
or signaling prototype exists. Verify end-to-end operation, security,
network behavior, and deployment readiness.

### 7.4 AI configuration and workflow tools

Potential capabilities: - Tenant knowledge management. - Approved
sources and data synchronization. - AI behavior and tone configuration
within safe bounds. - Business-specific policies and escalation rules. -
No-code or low-code workflow builder. - Trigger/action/condition
model. - Versioning, validation, preview, publish, rollback, and audit
for workflows. - Human handoff rules. - Usage metering and tenant-level
limits. - Test harness and conversation evaluation. - Safe fallback and
manual override.

A workflow builder should not become a disconnected visual toy. Verify
whether its workflows can execute reliably against real backend services
with permissions, idempotency, audit logs, and error handling.

## 8. Omnichannel & Channel Integration

Vạn An should connect customer entry points instead of forcing customers
to use one proprietary channel.

Potential channels and capabilities: - Website and embedded floating
assistant. - Vạn An PWA or mobile web experience. - QR code and deep
links. - Facebook/Instagram/Messenger integrations where official APIs
and permissions allow. - WhatsApp, Telegram, TikTok, or other channels
only where official APIs, regional availability, and business policies
support the use case. - Email, SMS, and push notifications where
appropriate. - Webhooks and partner integrations. - Unified conversation
identity and channel mapping. - Channel-specific consent, templates,
rate limits, and messaging policies. - Delivery status, retries,
deduplication, and webhook signature verification. - Unified inbox
across supported channels. - Channel abstraction so one provider outage
does not collapse the whole interaction layer.

Important: Do not assume that every social platform permits arbitrary
automation or access to all message/comment data. Verify current API
capabilities, approval requirements, pricing, and platform policies
before proposing implementation.

## 9. Discovery --- TimLaThay and local discovery

Potential capabilities: - Search for nearby shops, products, services,
and activities. - Search by intent, category, location, service area,
availability, and relevant attributes. - Tenant-controlled listing and
profile data. - Map and location discovery. - Search relevance and
ranking transparency. - Business verification and quality signals. -
Contact, chat, booking, and transaction handoff. - Sponsored placement
controls and disclosure if monetization is introduced. - Data freshness
and tenant update mechanisms.

The objective is not simply to build another generic search engine. It
is to connect a customer's local need to a relevant business and then
enable the next action.

## 10. Commerce & Service Fulfillment

Potential capabilities: - Product/service catalog. - Orders and order
lifecycle. - Booking and appointment lifecycle. - Availability and
capacity. - Quote/request-for-service flows. - Cart and checkout where
appropriate. - Payment provider integration. - Order status and customer
notifications. - Refund, cancellation, adjustment, and dispute
workflows. - ShopERP integration. - Inventory or availability
integration where applicable. - Human approval for actions that require
business judgment. - Idempotency, transaction integrity, and audit
trail.

AI must not invent availability, prices, policies, order status, or
payment success. These facts must come from authoritative services.

## 11. Relationship & Customer Intelligence

Potential capabilities: - Customer profiles and consent. - CRM and
interaction timeline. - Conversation-to-customer association. - Customer
segmentation and tags. - Customer service history. - Loyalty points,
vouchers, discounts, and redemption rules. - Campaign and notification
preferences. - Customer support cases and follow-up. - Data export,
correction, retention, and deletion workflows as required. -
Tenant-scoped analytics and access control.

Loyalty and promotional credits must be modeled according to their
actual legal and business nature. Do not represent non-cash ecosystem
benefits as cash, e-money, or a wallet unless the relevant legal and
regulatory requirements are met.

## 12. Ecosystem & Marketplace

Potential capabilities: - Marketplace discovery and listing. - Supplier
and service-provider profiles. - CTV/partner referral and task flows. -
Lead routing and attribution. - Service request and fulfillment
tracking. - Charity/community marketplace where part of the approved
roadmap. - Cross-tenant discovery and referrals under explicit
permissions. - Commission, revenue-share, and settlement records where
legally and operationally appropriate. - Reputation, complaints,
moderation, and dispute resolution. - Anti-fraud and abuse controls.

Marketplace functionality must not be confused with a simple catalog
page. Verify the full lifecycle: listing, discovery, inquiry,
acceptance, fulfillment, confirmation, settlement, support, and dispute
handling.

## 13. Digital Operations & Business Management

Potential capabilities: - ShopERP and point-of-sale operations. -
Orders, receipts, payments, and operational records. - Booking and
service operations. - Staff and role management. - Procurement and
supplier ordering. - Accounting integration and business event
processing. - E-invoice provider integration where applicable. -
Reporting and reconciliation. - Offline operation and synchronization
where required. - Auditability and immutable posted records where
required by the accounting design.

Identify which capabilities are authoritative in ShopERP, CoreHub,
Accounting Engine, or external providers. Avoid duplicate sources of
truth.

## 14. Digital Infrastructure & Shared Platform Services

Inspect the actual implementation of: - CoreHub and tenant-aware core
services. - API Gateway and service-to-service communication. -
Identity, authentication, authorization, and session management. -
Tenant provisioning and configuration. - Data ownership and service
boundaries. - PostgreSQL and other persistent storage. - Edge/offline
ShopERP and KhachLink data stores where applicable. - Sync Worker and
conflict resolution. - Event-driven processing, queues, and message
delivery. - Notification infrastructure. - File/object storage. - Search
and retrieval infrastructure. - AI model/provider abstraction. -
Metering, quotas, billing records, and usage reporting. - Observability:
logs, metrics, traces, alerts, dashboards. - Backup, restore, disaster
recovery, and tested recovery objectives. - Security controls, secrets,
encryption, vulnerability management, and audit. - CI/CD, environment
separation, infrastructure as code, and deployment automation. - Hybrid
hosting strategy, including self-managed Proxmox/Docker and selected
Google Cloud services where supported by the current architecture. -
Capacity, load testing, scaling, and cost visibility.

Do not force every service onto one hosting platform. Evaluate placement
according to latency, data sensitivity, reliability, operational
ownership, cost, and recovery requirements.

------------------------------------------------------------------------

# PART III --- INSTRUCTIONS TO DEVIN: CODEBASE AUDIT

## 15. Primary assignment

Read the **entire relevant codebase and its engineering documentation**
before proposing changes.

Your assignment is to determine:

1.  What modules and features actually exist?
2.  What works end-to-end?
3.  What exists only as UI, mock, stub, placeholder, hardcoded demo, or
    incomplete backend?
4.  What is partially implemented?
5.  What is missing relative to the target capability model in this
    document?
6.  What existing implementation conflicts with the vision or creates
    architectural risk?
7.  What is duplicated, fragmented, abandoned, or incorrectly owned by
    multiple services?
8.  What dependencies, integrations, data contracts, and security
    boundaries are absent?
9.  What should be reused, refactored, completed, separated, deprecated,
    or newly built?
10. What is the smallest coherent implementation sequence that moves Vạn
    An toward this vision without destabilizing working systems?

**Do not start implementing changes. This task is an assessment and
planning exercise only.** Wait for explicit approval before modifying
code, schema, infrastructure, or deployment.

## 16. Mandatory audit rules

-   Inspect source code, project structure, API routes, domain models,
    database schema/migrations, frontend routes/components,
    configuration, deployment files, tests, CI/CD, and available runtime
    evidence.
-   Read existing SRS/specifications and compare them with
    implementation. Do not treat an SRS as proof that a feature exists.
-   Trace important user journeys across frontend, API, services,
    database, integrations, and background workers.
-   Verify both happy paths and failure paths.
-   Run existing tests and appropriate build/static checks where safe.
    Do not run destructive commands or alter production data.
-   Do not create fake test results, claim execution without running it,
    or infer runtime success from code inspection alone.
-   Do not simplify, silently omit, or reinterpret requirements to make
    the codebase appear more complete.
-   Do not replace difficult requirements with stubs, manual
    workarounds, static responses, or mock integrations without clearly
    reporting the limitation.
-   Do not mark a feature complete merely because a screen, route,
    interface, DTO, database table, or placeholder service exists.
-   Do not assume a module name corresponds to a working capability.
-   Do not make broad architectural changes during the audit.
-   Identify evidence for every important conclusion: file paths,
    class/function names, API endpoints, schema/migration names, test
    names/results, configuration keys (never expose secret values), and
    deployment components.
-   Clearly distinguish **verified fact**, **inference**, **unknown**,
    and **recommendation**.
-   Report inaccessible repositories, missing environment variables,
    unavailable external services, and tests that could not be run.
-   Protect secrets and personal data in all audit outputs.

## 17. Required classification for every module and feature

Use the following status taxonomy:

  -----------------------------------------------------------------------
  Status                              Meaning
  ----------------------------------- -----------------------------------
  IMPLEMENTED & VERIFIED              End-to-end behavior has been
                                      verified with meaningful evidence
                                      and tests/runtime checks.

  IMPLEMENTED --- CODE REVIEW ONLY    Relevant code exists, but
                                      runtime/end-to-end behavior was not
                                      verified.

  PARTIALLY IMPLEMENTED               Some required behavior exists;
                                      material requirements remain
                                      incomplete.

  UI ONLY                             Frontend/screen exists without the
                                      required functional backend
                                      behavior.

  API/BACKEND ONLY                    Backend capability exists but
                                      required user-facing or workflow
                                      integration is missing.

  STUB / MOCK / PLACEHOLDER           Static, mocked, hardcoded, fake, or
                                      placeholder behavior.

  MISSING                             No meaningful implementation found.

  DUPLICATED / FRAGMENTED             Overlapping capability or
                                      conflicting sources of truth exist.

  MISALIGNED                          Existing implementation conflicts
                                      with the target vision,
                                      requirements, or security model.

  DEPRECATED / UNUSED                 Appears obsolete or unused; prove
                                      this before recommending removal.

  UNKNOWN / BLOCKED                   Evidence or access is insufficient
                                      to determine status.
  -----------------------------------------------------------------------

Do not use a single status for an entire large module if its features
have different states. Classify at feature level.

## 18. Required deliverables

### Deliverable A --- Executive assessment

Provide: - Overall alignment with the Vạn An Digital-first,
Human-centered vision. - The most important strengths already present in
the codebase. - The most consequential capability gaps. - The largest
architectural and operational risks. - Critical unknowns that prevent a
reliable conclusion. - A clear statement of what is verified versus
inferred.

Avoid unsupported percentage-complete claims. If a score is used,
explain the scoring method and evidence; do not use a score as a
substitute for the feature inventory.

### Deliverable B --- Complete module inventory

Create a table with at least these columns:

  -----------------------------------------------------------------------
  Field                               Required content
  ----------------------------------- -----------------------------------
  Module / service                    Actual name in the repository

  Business capability                 What it is intended to do

  Actual implementation               What the code currently does

  Feature                             Individual capability

  Status                              Use the mandatory taxonomy

  Evidence                            Exact paths, symbols, endpoints,
                                      migrations, tests

  Dependencies                        Internal and external dependencies

  Data ownership                      Which service is authoritative

  Tenant isolation                    How tenant boundaries are enforced

  Security/privacy                    Relevant controls and gaps

  Test evidence                       Test names, commands, outcomes, or
                                      not tested

  Gap / risk                          What is incomplete or unsafe

  Recommendation                      Reuse, complete, refactor,
                                      separate, deprecate, or build
  -----------------------------------------------------------------------

The inventory must include all discovered modules, not only the examples
in this document. Also map actual repository modules to the target
capabilities in this document.

### Deliverable C --- Feature gap matrix

For every target capability in Parts II and III, classify it as: -
Exists and verified. - Exists but not verified. - Partially exists. -
Missing. - Misaligned. - Not applicable / requires product decision. -
Blocked / unknown.

Include: - Requirement/capability ID. - Target capability. - Current
implementation and evidence. - Gap description. - User/business
impact. - Technical dependency. - Risk/severity. - Recommended next
action.

Do not mark a feature missing until you have searched relevant services
and integrations. Do not mark it complete based on a name or document
alone.

### Deliverable D --- End-to-end journey audit

Trace at least these journeys, adapting them to the actual
implementation:

1.  A customer discovers a nearby shop and opens its digital presence.
2.  A customer asks the shop AI about a product/service and receives a
    tenant-grounded answer.
3.  A customer moves from AI to a real staff member without losing
    context.
4.  A customer starts a voice call; verify signaling, media
    connectivity, network fallback, and call termination if implemented.
5.  A customer starts a video call if implemented.
6.  A customer asks about a product/service, books or orders, and
    receives an authoritative status.
7.  A shop owner/staff member manages customer conversations and follows
    up.
8.  A tenant is onboarded and receives isolated identity, data,
    configuration, and AI context.
9.  A customer interacts through an external channel and is associated
    with the correct tenant/customer/conversation, if implemented.
10. A business event is processed across ShopERP/CoreHub/accounting or
    other relevant services.

For each journey, show: - Entry point. - Frontend component. -
API/service path. - Data stores. - External provider dependencies. -
Authentication/authorization. - Tenant boundary. - Success behavior. -
Failure behavior. - Tests/evidence. - Missing steps.

### Deliverable E --- Architecture and source-of-truth map

Produce diagrams and explanations showing: - Actual services and their
responsibilities. - Frontend applications and channel entry points. -
Service-to-service calls and event flows. - Database ownership and data
synchronization. - Tenant context propagation. - AI retrieval and
tool/action execution path. - Human chat/voice/video path. - External
provider boundaries. - Authentication, authorization, secrets, and audit
boundaries.

Compare the actual architecture with the target capability model.
Identify duplicated responsibilities, circular dependencies, tight
coupling, hidden shared databases, and unclear ownership.

### Deliverable F --- Security, trust, privacy, and reliability audit

At minimum inspect: - Authentication and authorization. - Tenant
isolation and cross-tenant leakage risks. - API and webhook
authentication. - Secret storage and exposure. - PII and sensitive data
handling. - Consent and data retention. - AI prompt injection and
untrusted input boundaries. - AI tool/action authorization. - Human
handoff authorization. - Rate limiting, abuse prevention, and
denial-of-service exposure. - Audit logs and traceability. - Data
backup, restore, and disaster recovery. - Offline sync and conflict
handling. - Idempotency and duplicate event handling. -
Payment/order/accounting integrity where relevant. - WebRTC signaling
and media security where relevant. - Dependency and container
security. - Operational monitoring and alerting.

Classify findings by severity: Critical, High, Medium, Low, or
Informational. Include evidence and a practical remediation direction.

### Deliverable G --- Prioritized roadmap

Recommend a dependency-aware roadmap. Do not assume the phases below are
mandatory if the codebase evidence suggests a safer sequence.

Suggested framing:

-   **P0 --- Stabilize and protect:** security, tenant isolation, data
    integrity, deployment reliability, observability, backup/recovery,
    and broken core flows.
-   **P1 --- Complete the digital presence foundation:** tenant
    identity, business profile, catalog/service data, customer entry
    points, and reliable public presence.
-   **P2 --- Complete customer interaction:** customer-facing AI,
    tenant-grounded knowledge, unified conversation, human handoff, and
    staff workflow.
-   **P3 --- Connect channels and workflows:** official social/channel
    integrations, workflow builder, notifications, CRM, and booking.
-   **P4 --- Connect commerce:** ShopERP, orders, booking, payment
    integration, customer relationship, and post-transaction service.
-   **P5 --- Expand ecosystem discovery and network effects:**
    TimLaThay, supplier/CTV/marketplace capabilities, cross-tenant
    discovery, and ecosystem services.
-   **P6 --- Scale and optimize:** capacity, regional architecture,
    hybrid infrastructure, metering, cost efficiency, and operational
    automation.

For every roadmap item include: - Business outcome. - Scope and explicit
non-scope. - Required modules/services. - Prerequisites. -
Dependencies. - Risk. - Acceptance criteria. - Test strategy. -
Estimated complexity (S/M/L/XL) with assumptions. - Whether it is a new
module, an extension, or a refactor.

Do not provide false precision in time or cost estimates. State
assumptions and confidence.

### Deliverable H --- Decisions required from product owner

List decisions that cannot be safely inferred from code or this vision,
such as: - Which customer channels are mandatory in the first release? -
Which AI actions may execute automatically and which require human
confirmation? - Which service is the source of truth for each
business/customer object? - What data may be shared across tenants or
ecosystem participants? - What are the required consent, retention, and
audit policies? - Which voice/video use cases are in scope for the next
release? - Which capabilities should be integrated from third parties
rather than built? - Which modules are strategic differentiators versus
commodity infrastructure?

For each decision, explain the options, trade-offs, and consequences. Do
not silently choose on behalf of the product owner.

------------------------------------------------------------------------

# PART IV --- SPECIAL PRODUCT AND ARCHITECTURE GUARDRAILS

## 19. Correct definition of Vạn An Shop AI

**Vạn An Shop AI is a customer-facing interaction and orchestration
layer that helps customers communicate with a specific shop and its
owner/staff through AI-assisted chat, voice, and video.**

It is not primarily an internal chatbot for shop owners, staff, or
administrators.

The customer should be able to: - Ask questions about the specific
shop. - Discover relevant products and services. - Receive accurate,
tenant-grounded information. - Request human assistance. - Continue with
a human without repeating the conversation. - Proceed to
booking/order/service workflows where authorized and implemented.

The shop owner and staff need the corresponding operational tools to
receive, manage, assign, and respond to these customer interactions.

## 20. AI must not become an unsafe autonomous operator

For each AI action, identify: - What data it reads. - Which
tenant/customer it acts for. - Which permission authorizes the action. -
Whether the action is reversible. - Whether confirmation is required. -
How the action is logged. - How failure and retries are handled. - How a
human can intervene.

Never let an LLM response itself be treated as proof of payment,
booking, inventory, order completion, identity verification, or
authorization.

## 21. Multi-tenant design is foundational

Verify tenant isolation at every layer: - API routing and
authorization. - Database queries and row-level controls where
applicable. - Cache keys and invalidation. - Search indexes and
retrieval filters. - AI prompts, embeddings, and conversation memory. -
Background jobs and event consumers. - File/object storage. - Logs,
analytics, exports, and support tooling. - Webhooks and third-party
callbacks.

A tenant ID supplied by a browser is not, by itself, a trustworthy
authorization boundary.

## 22. Interoperability and platform dependency

-   Use official APIs and documented integration paths.
-   Separate channel adapters from core conversation and commerce
    domains.
-   Verify platform permissions, review requirements, regional support,
    quotas, and costs.
-   Support retries, deduplication, webhook verification, and provider
    outages.
-   Preserve a first-party customer experience without requiring every
    customer to install a new app.
-   Do not assume Vạn An can access or migrate all social-platform
    audiences or message histories.

## 23. Architecture quality

-   Respect existing working architecture unless evidence justifies
    change.
-   Prefer clear bounded contexts and explicit service ownership.
-   Avoid unnecessary microservices and duplicated databases.
-   Keep contracts versioned and observable.
-   Ensure critical writes are transactional or reliably coordinated.
-   Use idempotency for retried commands and events.
-   Design offline synchronization explicitly where required.
-   Separate development, staging, and production.
-   Do not use schema auto-creation shortcuts in production.
-   Do not introduce a new infrastructure dependency without documenting
    its operational burden and failure modes.

------------------------------------------------------------------------

# PART V --- REQUIRED OUTPUT FORMAT

## 24. Files Devin must produce

Create a dedicated audit output folder, for example:

`/docs/strategy/van-an-digital-first-audit/`

Produce at least:

1.  `00_EXECUTIVE_ASSESSMENT.md`
2.  `01_MODULE_FEATURE_INVENTORY.md`
3.  `02_VISION_CAPABILITY_GAP_MATRIX.md`
4.  `03_END_TO_END_JOURNEY_AUDIT.md`
5.  `04_CURRENT_ARCHITECTURE_AND_DATA_OWNERSHIP.md`
6.  `05_SECURITY_TRUST_PRIVACY_RELIABILITY_AUDIT.md`
7.  `06_PRIORITIZED_ROADMAP.md`
8.  `07_PRODUCT_OWNER_DECISIONS.md`
9.  `08_EVIDENCE_INDEX.md`

If the repository is too large for one pass, split the audit into
bounded areas and maintain a consolidated index. Do not omit services
silently.

## 25. Final response to the product owner

When the audit is complete, summarize:

1.  What Vạn An already has that directly supports this vision.
2.  What is implemented and verified end-to-end.
3.  What is only partially implemented or exists as a stub/mock/UI.
4.  The most important missing modules and features.
5.  The most serious architecture/security/reliability gaps.
6.  Which existing modules should be reused, completed, refactored,
    separated, or deprecated.
7.  The recommended implementation order and critical dependencies.
8.  The product-owner decisions required before implementation.

Every major conclusion must link to evidence in the audit files and
actual repository paths.

**Do not begin implementation after producing the audit. Stop and wait
for explicit product-owner approval.**

------------------------------------------------------------------------

# APPENDIX --- STRATEGIC TEST

Use this question to evaluate every proposed module or feature:

> Does this capability help a person or business establish a trusted
> digital presence, communicate, connect, transact, fulfill a real-world
> need, or create sustainable value in the Vạn An ecosystem?

If yes, identify its place in the architecture and its measurable
business outcome.

If no, explain why it is still necessary as shared infrastructure,
compliance, security, reliability, or operational support.

The goal is not to maximize the number of modules. The goal is to build
a coherent, trustworthy, interoperable digital economic infrastructure
that small businesses and communities can actually use.
