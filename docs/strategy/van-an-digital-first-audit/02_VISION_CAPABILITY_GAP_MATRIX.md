# 02 — VISION CAPABILITY GAP MATRIX

Classification per brief §18-C: Exists & verified / Exists not verified / Partially exists / Missing / Misaligned / N-A-product-decision / Blocked-unknown.
Severity: 🔴 Critical · 🟠 High · 🟡 Medium · ⚪ Low.

---

## §5 Digital People & Digital Businesses

| ID | Target capability | Status | Evidence | Gap | Impact | Dependency | Severity | Next action |
|---|---|---|---|---|---|---|---|---|
| ID-01 | Individual/customer identity & profile | Exists & verified | CustomerIdentity, ATRS TrustSubject, passkey login RV | — | — | — | — | Extend tiered-trust rollout (ID-P1/P4/MFA-1 cards approved) |
| ID-02 | Business/tenant identity & profile | Exists & verified | Tenant, TenantRegistration, BusinessProfile, storefront config | Lifecycle states thin | Onboarding/closure edge cases | — | 🟡 | Define tenant lifecycle SRS (suspend/migrate/close) |
| ID-03 | Tenant onboarding & verification states | Exists & verified | Onboarding, OCR wizard, crawler, claims queue, TenantStatus | Verification depth uneven | Trust signals for discovery | — | 🟡 | Consolidate verification criteria |
| ID-04 | Staff/owner/operator/collaborator/supplier/CTV/customer roles | Exists & verified | UserRole, PermissionGroup, StaffPermissionGrant, Salesman/Collaborator, Member | Role model spread across 3 services | Authorization complexity | — | 🟡 | Document unified role matrix |
| ID-05 | Membership & relationships people↔business | Partially exists | Membership infra + Member + HtxProfile; customer↔tenant link via CustomerId | No generalized relationship graph | Ecosystem features | Membership SRS | 🟡 | Extend membership model deliberately |
| ID-06 | Consent, privacy prefs, communication prefs | Partially exists | ConsentRecord (ATRS/Membership), context dismissals | No unified consent center | Compliance (Law 91/2025 pending legal review) | Legal review | 🟠 | Consent registry design |
| ID-07 | Account trust, device association, recovery | Exists & verified | ATRS full vertical (passkeys, recovery codes, disputes, devices) | Email/SMS notify channels pending | Account security UX | P4 email-first OTP | ⚪ | ATRS Wave 4-6 residuals |
| ID-08 | Business profile, service area, hours, products, policies, contacts | Exists & verified | ShopConfig, offerings, products, social links | Policies field thin | AI grounding quality | Knowledge corpus | 🟡 | Enrich profile schema via W3 auto-builder |
| ID-09 | Digital presence pages / storefront entry | Exists & verified | KhachLink profiles, Directory, `/a/{slug}` | — | — | — | — | — |
| ID-10 | Tenant lifecycle onboarding→closure | Partially exists | Status enum, conversion service, claims | No offboarding/data-export workflow | Churn/compliance | ID-02 | 🟡 | Lifecycle SRS |

## §6 Digital Presence

| ID | Capability | Status | Evidence | Gap | Severity | Next action |
|---|---|---|---|---|---|---|
| DP-01 | Public business profile & storefront | Verified | Store.razor, instances, branding | — | — | — |
| DP-02 | Branding/logo/images/content | Verified | StorefrontImages, Icon, DynamicTheme | — | — | — |
| DP-03 | Product/service catalogs + attributes | Verified | Products, AppointmentOffering, NeedsReview/IsSellable | Attribute schema basic | 🟡 | Richer attributes for discovery |
| DP-04 | Menu/price/availability/policies | Partially exists | Products+prices; availability not exposed to AI/public reliably | AI may state stale availability | 🟠 | Authoritative availability API for AI (W4 prerequisite) |
| DP-05 | Location/service area/hours/contact | Verified | StoreConfig, maps, GoogleMaps | — | — | — |
| DP-06 | SEO-friendly public pages/metadata | Verified | head-meta, JSON-LD, sitemap | — | — | — |
| DP-07 | QR entry + shareable links | Verified | QR service, assistant link/QR, claim flows | — | — | — |
| DP-08 | Tenant-controlled publish workflow | Partially exists | PendingReview/NeedsReview for products+offerings+knowledge | No page-level CMS/draft-publish | 🟡 | Extend approval pattern if needed |
| DP-09 | Sync business info across channels | Missing | No outbound sync to external platforms | Single-channel truth only on VanAn surfaces | 🟡 | Channel adapters (W5) |
| DP-10 | Accessibility/mobile/performance | Partially exists | Mobile-first UI platform, PWA; no formal a11y audit found | a11y unverified | 🟡 | a11y audit task |

## §7 AI Interaction Layer

| ID | Capability | Status | Evidence | Gap | Severity | Next action |
|---|---|---|---|---|---|---|
| AI-01 | Customer-facing chat UI | Verified | AiChatWidget, /a/{slug}, storefront embed | — | — | — |
| AI-02 | Voice/video entry points | Verified (human) | Voice/video call UI; AI voice partial | — | — | — |
| AI-03 | AI greeting/intent/QA/guidance | Verified | IntentRouter, PromptBuilder, AnswerValidator | — | — | — |
| AI-04 | Tenant knowledge base + approved context | Verified | pgvector ingest, approval workflow, usage permissions | Corpus coverage uneven across tenants | 🟠 | Onboarding checklist shipped; drive tenant adoption |
| AI-05 | Product/service discovery constrained to catalog | Verified | ProductsBatch sync, product cards, retrieval | Price=0 "giá liên hệ" handled; availability gap (DP-04) | 🟡 | W4 actions need authoritative reads |
| AI-06 | Conversation history/continuity | Verified | Session messages endpoint, resume history | Cross-channel continuity missing | 🟡 | Unified conversation model (channels) |
| AI-07 | Human handoff to owner/staff | Partially exists | Handoff chip → chat/voice; context partial | No structured ticket/assignment/queue for AI escalations | 🟠 | W4 handoff→lead/ticket |
| AI-08 | Staff assignment/queue/escalation | Partially exists | ShopInbox threads, voice round-robin; not unified for AI leads | 🟠 | W4 |
| AI-09 | AI vs human indication | Verified | Widget states, handoff chip | — | — | — |
| AI-10 | Consent/disclosure | Partially exists | Widget presence; formal AI-disclosure copy unverified | 🟡 | Legal review (Law 91/2025) |
| AI-11 | Safe fallback low confidence | Verified | Refused→handoff, degraded MeterOnly, structured-only degrade | — | — | — |
| AI-12 | Multilingual | Unknown/Blocked | Vietnamese-focused; no evidence of EN support | — | ⚪ | Product decision |
| AI-13 | Abuse prevention/rate limits | Verified | Session cap 20/h, IP rate limits, QuotaGate, ThreatSuite | — | — | — |
| AI-14 | Tenant isolation of AI context | Verified | Signed HMAC ctx, caller allowlist, IMustHaveTenant, isolation tests | — | — | — |
| AI-15 | Prompt-injection defenses | Verified | PolicyEngine, AnswerValidator, ThreatSuite T2/T3, sanitizer | — | — | — |
| AI-16 | Secrets management for AI | Verified | Caller keys server-side, X-Ai-Key, env secrets | — | — | — |
| AI-17 | Auditable sensitive access | Verified | AuditLog infra + TenantAI audit + conversation records | — | — | — |
| AI-18 | Workflow builder (trigger/action/condition, versioning, publish/rollback) | Missing | No workflow engine found | Entire capability absent | 🟠 | Product decision: build vs integrate (n8n-style) |
| AI-19 | AI usage metering + tenant limits | Verified | `ai.chat.message` Deny, `ai.chat.degraded` MeterOnly, per-assistant budget planned | Attribution metadata expanding (W2a) | — | — | Verify rate-card seed on prod |
| AI-20 | Conversation eval/test harness | Partially exists | ThreatSuite tests; no owner-facing eval suite | 🟡 | W3 test-question generation planned |

## §8 Omnichannel

| ID | Capability | Status | Evidence | Severity | Next action |
|---|---|---|---|---|---|
| CH-01 | Website + embedded assistant | Verified | Storefront widget, /a/{slug}, iframe embed W2a | — | Verify embed depth |
| CH-02 | PWA/mobile web | Verified | KhachLink PWA, install prompt | — | — |
| CH-03 | QR/deep links | Verified | QR + assistant link | — | — |
| CH-04 | FB/IG/Messenger integration | Partially exists | FB Lead Ads webhook service only; no Messenger chat | 🟠 | Channel adapter spike (official API) |
| CH-05 | WhatsApp/Telegram/TikTok channels | Missing | Config fields only for internal alerts | 🟠 | Product decision per market |
| CH-06 | Email/SMS/push notifications | Verified (email/push), Code-review (SMS) | Brevo, Web Push, ESMS | 🟡 | ZNS evaluation |
| CH-07 | Webhooks + partner integrations | Partially exists | Inbound e-invoice/payment webhooks; no outbound partner webhooks | 🟡 | Define partner API strategy |
| CH-08 | Unified conversation identity/channel mapping | Missing | Per-channel silos; `OmnichannelService` stub | 🟠 | Conversation aggregate design |
| CH-09 | Channel consent/templates/rate limits | Missing | — | 🟠 | With CH-04/05 |
| CH-10 | Delivery status/retries/dedup/signature verify | Partially exists | Idempotency on webhooks claimed; signature verification unverified | 🟠 | Audit `WebhookController` verification |
| CH-11 | Unified inbox across channels | Partially exists | ShopInbox single-channel | 🟠 | Depends CH-08 |
| CH-12 | Channel abstraction / provider outage isolation | Missing | No adapter layer | 🟠 | Build with first real channel |

## §9 Discovery — TimLaThay

| ID | Capability | Status | Severity | Next action |
|---|---|---|---|---|
| DI-01 | Nearby shop/product/service search | Verified (ITenantSearchService) | — | — |
| DI-02 | Search by intent/category/location/attributes | Partially exists | 🟡 | Structured attributes (DP-03) |
| DI-03 | Tenant-controlled listing/profile | Verified | — | — |
| DI-04 | Map & location discovery | Verified | — | — |
| DI-05 | Ranking transparency/quality signals | Partially exists | 🟡 | Verification badges exist; ranking explainability absent |
| DI-06 | Handoff to chat/booking/transaction | Verified | — | Contextual AI/voice entries shipped W3 |
| DI-07 | Sponsored placement + disclosure | Missing | 🟡 | Product decision (monetization) |
| DI-08 | Data freshness mechanisms | Partially exists | 🟡 | Freshness monitoring |

## §10 Commerce & Fulfillment

| ID | Capability | Status | Severity | Next action |
|---|---|---|---|---|
| CO-01 | Product/service catalog | Verified | — | — |
| CO-02 | Orders + lifecycle | Verified | — | — |
| CO-03 | Booking + appointment lifecycle | Verified | — | — |
| CO-04 | Availability/capacity | Partially exists | 🟠 | Expose authoritative availability to AI/bookings edge cases |
| CO-05 | Quote/RFQ flows | Missing | 🟡 | Evaluate demand (print/design use case in vision) |
| CO-06 | Cart/checkout | Verified | — | — |
| CO-07 | Payment provider integration | Partially exists (VietQR only) | 🔴 | Payment gateway selection + integration (VNPay/MoMo or bank-aggregator) |
| CO-08 | Order status + customer notifications | Verified | — | — |
| CO-09 | Refund/cancel/adjust/dispute | Partially exists | 🟠 | Money-movement refunds blocked on CO-07; dispute infra exists |
| CO-10 | ShopERP integration | Verified | — | — |
| CO-11 | Inventory/availability integration | Partially exists | 🟡 | Public availability read path |
| CO-12 | Human approval for judgment actions | Partially exists | 🟡 | NeedsReview flows; extend to AI actions (W4) |
| CO-13 | Idempotency/transaction integrity/audit | Verified | — | — |

## §11 Relationship & Customer Intelligence

| ID | Capability | Status | Severity | Next action |
|---|---|---|---|---|
| RC-01 | Customer profiles + consent | Verified/Partial | 🟡 | Consent center (ID-06) |
| RC-02 | CRM + interaction timeline | Partially exists | 🟡 | Unify chat/order/booking/voice timeline per customer |
| RC-03 | Conversation→customer association | Verified | — | — |
| RC-04 | Segmentation/tags | Partially exists | ⚪ | — |
| RC-05 | Service history | Verified (orders/bookings) | — | — |
| RC-06 | Loyalty/vouchers/discounts/redemption | Verified | — | — |
| RC-07 | Campaign + notification prefs | Partially exists | 🟡 | Preference center |
| RC-08 | Support cases + follow-up | Partially exists | 🟡 | Ticketing gap |
| RC-09 | Export/correction/retention/deletion | Partially exists | 🟠 | Formal data-subject workflows (Law 91/2025) |
| RC-10 | Tenant-scoped analytics + access control | Verified | — | — |
| RC-11 | Loyalty legal modeling (non-cash) | Product decision | 🟡 | Already modeled as points not e-money — confirm with legal |

## §12 Ecosystem & Marketplace

| ID | Capability | Status | Severity | Next action |
|---|---|---|---|---|
| EC-01 | Marketplace discovery/listing | Partially exists | 🟡 | Directory + catalog; no third-party seller lifecycle |
| EC-02 | Supplier/service-provider profiles | Partially exists | 🟡 | Members/CTV profiles exist |
| EC-03 | CTV/partner referral + tasks | Verified (partial) | — | Missions + salesman flows |
| EC-04 | Lead routing/attribution | Partially exists | 🟡 | FacebookLead→Lead; booking attribution; not unified |
| EC-05 | Service request + fulfillment tracking | Partially exists | 🟡 | Booking covers appointments; generic service requests absent |
| EC-06 | Charity/community marketplace | Partially exists | 🟡 | CommunityFund exists; marketplace thin |
| EC-07 | Cross-tenant discovery/referrals | Partially exists | 🟡 | Under explicit perms; alliance wallet |
| EC-08 | Commission/revenue-share/settlement | Partially exists | 🟡 | BookingCommission, ReferralCommission, Settlement admin; legal review needed |
| EC-09 | Reputation/complaints/moderation/disputes | Partially exists | 🟡 | FraudFlags + ATRS disputes (loyalty) + order disputes; no reputation system |
| EC-10 | Anti-fraud/abuse | Verified (partial) | — | FraudFlag, RiskScoring, self-referral block, rate limits |

## §13 Digital Operations

| ID | Capability | Status | Severity | Next action |
|---|---|---|---|---|
| OP-01 | ShopERP POS/receipts/payments | Verified | — | — |
| OP-02 | Orders/receipts/payments records | Verified | — | — |
| OP-03 | Booking/service ops | Verified | — | — |
| OP-04 | Staff/role mgmt | Verified | — | — |
| OP-05 | Procurement/supplier ordering | Partially exists | 🟡 | Purchase receipts exist; supplier portal absent |
| OP-06 | Accounting integration + events | Verified | — | — |
| OP-07 | E-invoice provider integration | Exists not verified (live tenants) | 🟡 | Confirm credentialed tenants |
| OP-08 | Reporting/reconciliation | Verified | — | — |
| OP-09 | Offline + sync | Partially exists | 🟠 | Edge mode real; OmnichannelService offline queue stub |
| OP-10 | Auditability/immutable posted records | Verified | — | — |

## §14 Infrastructure

| ID | Capability | Status | Severity | Next action |
|---|---|---|---|---|
| IF-01 | CoreHub tenant-aware services | Verified | — | — |
| IF-02 | API Gateway + S2S | Verified | — | — |
| IF-03 | Identity/auth/authz/session | Verified | — | — |
| IF-04 | Tenant provisioning/config | Verified | — | — |
| IF-05 | Data ownership boundaries | Verified (mostly) | 🟡 | Documented in Deliverable E |
| IF-06 | PG + edge SQLite stores | Verified | — | — |
| IF-07 | Sync Worker + conflict resolution | Verified (basic conflicts) | 🟡 | Conflict policy doc + tests |
| IF-08 | Event-driven queues/delivery | Verified | — | — |
| IF-09 | Notification infra | Verified | — | — |
| IF-10 | File/object storage | Verified | — | — |
| IF-11 | Search/retrieval infra | Verified | — | — |
| IF-12 | AI model/provider abstraction | Partially exists | 🟠 | Single provider (Gemini); adapter interfaces planned |
| IF-13 | Metering/quotas/billing records | Verified | — | Verify prod rate-card seeds |
| IF-14 | Observability (logs/metrics/traces/alerts/dashboards) | Partially exists | 🟠 | Serilog+health+/metrics only; no tracing/APM/alerting verified |
| IF-15 | Backup/restore/DR + tested RPO/RTO | Partially exists | 🔴 | Automated scheduled PG backup + restore drills |
| IF-16 | Security controls/secrets/vuln mgmt/audit | Verified (broad) | 🟡 | No dependency scanning/SAST in CI verified; secrets via env |
| IF-17 | CI/CD/env separation/IaC/deploy automation | Verified | — | — |
| IF-18 | Hybrid hosting strategy | Partially exists | ⚪ | GCP-only today; Proxmox documented |
| IF-19 | Capacity/load testing/scaling/cost visibility | Partially exists | 🟡 | Load tests exist; P6 gates pending; cost dashboards absent |

**Summary counts (approx.):** Verified ~38 · Partially ~30 · Missing ~9 · Misaligned 0 confirmed · Unknown/Blocked ~4 · N-A-product-decision ~3.
