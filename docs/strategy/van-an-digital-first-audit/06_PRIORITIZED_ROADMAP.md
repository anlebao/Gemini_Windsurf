# 06 — PRIORITIZED ROADMAP

Dependency-aware sequencing based on audit evidence. Complexity S/M/L/XL with assumptions; no false time precision. Existing approved plans are referenced rather than duplicated (Universal AI Assistant W0–W6, Voice P5/P6, Identity L0–L3, ATRS waves).

---

## P0 — Stabilize & protect (do first; unblocks everything)

| # | Item | Outcome | Scope / non-scope | Modules | Prereqs | Risk | Acceptance criteria | Test strategy | Complexity |
|---|---|---|---|---|---|---|---|---|---|
| P0-1 | Automated PG backup + restore drill | No data-loss window | All 5 PG DBs; non-scope: multi-region | scripts/, VPS cron, object storage | — | Low | Scheduled dumps ship off-box; quarterly restore verified; RPO/RTO documented | Restore drill script + checklist | S |
| P0-2 | Observability baseline | Detect degradation before users | Correlation IDs, central log query, basic alerts (health fail, metering deny spike, NATS lag) | All services, Serilog enrich, one dashboard | — | Low | Alert fires on induced failure; runbook linked | Chaos-lite test + alert verification | M |
| P0-3 | Webhook security audit + fix | Payment/e-invoice callbacks trustworthy | Verify signature/token/replay per provider | `WebhookController`, `IWebhookService`, providers | — | Medium | Forged webhook rejected in tests; per-provider verify documented | Unit + integration tests per provider | M |
| P0-4 | Stub quarantine | No silent fake-success paths | `OmnichannelService`, `Providers/POS/*` | CoreHub | — | Low | Stubs cannot be activated in prod; or implemented | Config guard + tests | S |
| P0-5 | Security quick wins | Dependency scan + secrets rotation runbook | `dotnet list package --vulnerable`, container scan in CI; rotation doc | CI, docs | — | Low | CI fails on critical vulns; runbook exists | CI job + doc review | S |
| P0-6 | TenantAI rate-card seed verification | Metering actually enforced for AI | Verify `ai.chat.message` Deny card on prod DB | vanan-metering | — | Low | Card resolves; deny path tested | Metering API check + test | S |

## P1 — Complete digital presence foundation

| # | Item | Outcome | Modules | Prereqs | Complexity |
|---|---|---|---|---|---|
| P1-1 | Finish approved identity cards: ID-P1 trust fields → MFA-1 admin TOTP → P4 email-first OTP + OtpService hardening → AF1 antiflood | Tiered trust L0–L3 real | CoreHub, ShopERP, Gateway, Brevo | P0 done | M (cards exist) |
| P1-2 | Tenant lifecycle SRS + offboarding/export basics | Clean tenant lifecycle | CoreHub, ShopERP admin | — | M |
| P1-3 | Knowledge corpus adoption drive | Every active tenant has ≥3 approved docs + products | TenantAI, ShopERP admin, onboarding UX | W3 pipeline done | S (ops) |
| P1-4 | a11y + performance audit of public surfaces | WCAG-ish baseline, WASM boot <3s | KhachLink, Directory, UI.Platform | — | M |
| P1-5 | Unified consent/preferences center v1 | Consent capture + retention schedule | CoreHub, KhachLink profile, legal | Legal review Law 91/2025 | L |

## P2 — Complete customer interaction

| # | Item | Outcome | Modules | Prereqs | Complexity |
|---|---|---|---|---|---|
| P2-1 | **W4 Business Actions** (per approved master plan): structured handoff→lead/ticket, booking request creation, staff context | AI converts, not just answers | Gateway, CoreHub, ShopERP ShopInbox, TenantAI | W1–W3 done; authoritative availability read (CO-04) | L |
| P2-2 | Unified customer-360 timeline (chat+orders+bookings+loyalty+voice) | Staff sees full context | CoreHub read model, ShopERP UI | Conversation aggregate decision | M |
| P2-3 | AI→human context packet (transcript summary, intent, customer ref) | No repeated explanations | TenantAI + handoff API + ShopInbox | P2-1 | M |
| P2-4 | Voice P5 pilot completion + P6 rollout gates | Proven call quality at scale | 10_Voice, metering | ≥100 real calls, TURN metrics | M (ops) |
| P2-5 | AI voice spike → decision (Gemini Live) | Evidence for W6 | TenantAI adapter prototype | Provider decision D12 approved | S (spike) |

## P3 — Connect channels & workflows

| # | Item | Outcome | Modules | Prereqs | Complexity |
|---|---|---|---|---|---|
| P3-1 | **Channel adapter layer v1 + first real channel** (recommend Messenger or Zalo OA — PO decision) | Meet customers on their channel | New `channel-adapters` bounded context in CoreHub/Gateway, conversation aggregate | CH-08 identity model; official API approval | L |
| P3-2 | Unified inbox across first-party + 1 external channel | Staff one queue | ShopInbox extension | P3-1 | M |
| P3-3 | Workflow builder decision + v1 (or buy) | Tenant automation | CoreHub or external engine | PO decision (build vs n8n-class) | L |
| P3-4 | ZNS evaluation for transactional notifications | Reach customers without app | Notification infra | Zalo OA approval | M |
| P3-5 | Notifications preference center + delivery-status dashboard | Channel consent honored | CompositeNotification, customer prefs | P1-5 | M |

## P4 — Connect commerce money

| # | Item | Outcome | Modules | Prereqs | Complexity |
|---|---|---|---|---|---|
| P4-1 | **Payment gateway integration** (VNPay and/or MoMo; or bank-aggregator) — checkout capture + webhook verify + refund | Close digital transaction loop | Gateway, ShopERP checkout, metering topup reuse | PO provider decision; H2/H3 done | L |
| P4-2 | Real refund money-movement path | Refunds actually return money | RefundOrchestration + gateway | P4-1 | M |
| P4-3 | Quote/RFQ flow for service businesses | Vision use-case (print/design/hotel) | Booking extension or new aggregate | Product decision | M |
| P4-4 | E-invoice live-tenant verification + provider credential onboarding | Compliance in production | EInvoice providers, admin | P0-3 | S |
| P4-5 | Supplier ordering portal (basic) | Procurement loop | ShopERP + supplier-facing pages | — | M |

## P5 — Expand ecosystem discovery & network effects

| # | Item | Outcome | Modules | Prereqs | Complexity |
|---|---|---|---|---|---|
| P5-1 | M1 `/jobs` job marketplace (un-defer) | JobMarket profile real | KhachLink, products-as-jobs model | SRS for jobs | M |
| P5-2 | M2 group-buy (mua chung) | Bulk purchase coordination | New module | SRS required (explicitly deferred) | L |
| P5-3 | Marketplace listing lifecycle for 3rd-party sellers | Full EC-01..EC-09 chain | Directory + settlement + disputes | Settlement legal review | XL |
| P5-4 | Loyalty Alliance GA hardening + cross-tenant campaigns | Network-effect retention | AllianceWallet, InternalLoyalty | Legal/tax review of points | M |
| P5-5 | TimLaThay ranking quality signals + freshness SLA | Trustworthy discovery | ITenantSearchService, Directory | DI-05/08 | M |

## P6 — Scale & optimize

| # | Item | Outcome | Prereqs | Complexity |
|---|---|---|---|---|
| P6-1 | Voice P6 rollout (10k merchants gates) | Proven at scale | P2-4 gates | L |
| P6-2 | CoreHub bounded-context extraction (chat/workflows first) | Reduce blast radius | Stable module boundaries | L |
| P6-3 | NATS HA + multi-region decision | Resilience | P0-2 metrics | M |
| P6-4 | Cost dashboards (LLM/voice/storage per tenant) | Unit economics | Metering attribution complete | M |
| P6-5 | Hybrid hosting evaluation (Proxmox vs GCP per workload) | Cost/latency optimization | P6-3 | M |
| P6-6 | Mobile apps decision (HRApp/StationApp real or archive; admin PWA exists) | Clarity | PO decision | S |

## Critical dependency chain

```text
P0 (backup/observability/webhook/stubs)
  └─► P1 (identity tiers, lifecycle, consent)
        └─► P2 (W4 actions needs: handoff ticket + availability read + timeline)
              ├─► P3 channels (needs conversation aggregate + consent)
              ├─► P4 payments (independent-ish; needs webhook audit done)
              └─► P5 ecosystem (needs settlement legal + P4 money loop)
                    └─► P6 scale
```

**Recommended first-quarter focus:** P0 all → P1-1/P1-3 → W4 (P2-1) → payment gateway decision (P4-1) in parallel → first channel spike (P3-1).
