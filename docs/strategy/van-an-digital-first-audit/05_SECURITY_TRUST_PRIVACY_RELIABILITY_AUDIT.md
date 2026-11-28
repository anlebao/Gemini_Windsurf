# 05 — SECURITY, TRUST, PRIVACY & RELIABILITY AUDIT

Findings classified Critical / High / Medium / Low / Informational with evidence + remediation direction. Secrets never reproduced.

---

## A. Verified strong controls (evidence, not claims)

| Control | Evidence |
|---|---|
| Tenant isolation at DB layer | `IMustHaveTenant` + EF global filters; arch tests; single-identity rule enforced |
| Server-side tenant resolution for public AI | `PublicTenantAiController.ResolveStoreTenantAsync`, `PublicAssistantLinkController` slug→tenant, signed HMAC ctx to vanan-ai |
| AI abuse economics | Session cap 20/h, `tenant-ai-public`/`public-metering` rate limits, QuotaGate Deny, `ai.chat.degraded` MeterOnly, per-IP device-caller limit |
| Prompt-injection / cross-tenant AI defenses | ThreatSuite T1–T10, PolicyEngine, AnswerValidator, IngestSanitizer, usage permissions |
| WebAuthn/passkeys production-grade | ATRS: challenge one-time TTL, origin/RP-ID binding, replay reject, UV=Required, rate guard — RV verified incl. real ceremony |
| Accounting integrity | Immutable `AccountingEntry`, reversal-only, period closing, regime-lock |
| Idempotency | Order/booking/metering/alliance idempotency keys; unique constraints (voice FR-007, booking slots) |
| Secrets handling discipline | `DevToken__Secret` unset-by-default + window protocol; caller secrets server-side; env/GH secrets; no secrets in client bundles (W0 audit) |
| Rate limiting breadth | Per-endpoint policies + nginx 5-layer + per-IP device limits |
| Audit trail | `AuditLog` infra, `AccountSecurityEvent`, audit explorer (A20), append-only metering |

## B. Findings

### HIGH

| # | Finding | Evidence | Risk | Remediation |
|---|---|---|---|---|
| H1 | **No automated scheduled PostgreSQL backup/DR verified** | Only `scripts/backup-db.sh` (SQLite, 7-day local retention); one-time manual `pg_dump`→restore verified for ATRS (W6a). No cron/CD backup job found for PG databases (VanAnDbContext, metering, tenantai, voice, atrs) | Data loss on VPS failure; RPO undefined | Scheduled `pg_dump` + object-storage shipping + quarterly restore drill; define RPO/RTO per DB |
| H2 | **Payment acceptance is bank-transfer + webhook/manual only** | `VietQrController` (URL gen), `WebhookController /api/webhooks/payment`; `RefundOrchestrationService` comment "No payment provider integration (VNPay/MoMo)" | Reconciliation risk, fraud surface on manual confirm, no automated refund | Payment gateway integration; webhook signature verification audit; reconciliation job |
| H3 | **Inbound webhook signature verification unverified** | `WebhookController.ReceiveWebhook` reads raw body → `IWebhookService`; verification depth not traced for e-invoice providers and payment | Spoofed payment/invoice callbacks could mark orders paid / ingest fake invoices | Trace + test signature/token verification per provider; add replay protection |
| H4 | **`OmnichannelService` + POS providers are silent stubs returning success** | `OmnichannelService.cs` in-memory dicts + TODO; `KiotVietPOSProvider`/`SapoPOSProvider` `Task.FromResult(Success:true)` | False confidence; if wired to UI/sync jobs, silent data loss | Remove or implement; add guard so stub providers can't be selected in prod config |

### MEDIUM

| # | Finding | Evidence | Risk | Remediation |
|---|---|---|---|---|
| M1 | **No centralized observability** | Serilog per service, `/health`+`/health/detail`, ATRS `/metrics` Prometheus only; no tracing (OpenTelemetry absent), no alerting rules verified | Slow MTTR, silent degradation | Adopt OpenTelemetry + one dashboard/alert channel; ship correlation IDs end-to-end |
| M2 | **Secrets in env files / VPS .env; no vault** | `.env.*` examples, `cd-deployment.md` wiring, `keys/` dirs in CoreHub/ShopERP | Credential sprawl, rotation manual | Document rotation runbook; consider sealed secrets/SOPS short-term |
| M3 | **PII handling: phone/email encrypted in-memory matching; retention uneven** | `CustomerIdentityController` PII-encrypted match; `RetentionCleanupService` for AI conversations; no unified retention schedule | Law 91/2025 compliance gap (legal review pending per W0) | Data inventory + retention policy per store; consent center |
| M4 | **Cross-tenant `IgnoreQueryFilters` used in public/community paths** | `CommunityOrderService.GetNearbyOrdersAsync`, customer lookups | Correct-by-design but easy to misuse → leak if DTO over-shares | Audit each IgnoreQueryFilters call site for field minimization; add arch test listing allowed sites |
| M5 | **AI session/token model client-generated** | W0 C2/K3: sessionId client string; optional signed token deferred | Session fixation/replay residual (mitigated by tenant scoping + caps) | W5 signed session token as planned |
| M6 | **Facebook Lead webhook verification unverified** | `FacebookLeadService` exists; `X-Hub-Signature-256` check not confirmed | Fake lead injection | Verify + test signature path before enabling |
| M7 | **Dependency/container security scanning absent in CI** | `ci-full.ps1`, `cd-multivps.yml` — build+test+deploy; no Trivy/npm audit/dotnet list verified | Vulnerable deps ship silently | Add `dotnet list package --vulnerable` + container scan to CI |
| M8 | **Sync conflict handling basic** | NATS outbox PG↔SQLite both directions; last-writer surfaces | Edge cases on concurrent edits | Document conflict policy; add reconciliation reports (exists for loyalty — extend) |
| M9 | **Guest device-id is bearer credential** | `X-Customer-Device-Id` grants guest chat/voice/orders | Device-id theft → impersonation within guest scope | Rate limits exist; consider binding to attestation later; document trust model |
| M10 | **CoreHub monolith = large blast radius** | ~200 services in-process | One bug can take down all business logic | Progressive extraction (metering/AI/voice/ATRS already separate — continue pattern for chat/workflows) |

### LOW / INFORMATIONAL

| # | Finding | Evidence |
|---|---|---|
| L1 | Dead files in prod tree | `RetryService.cs.bak`, `StateMachineService.cs.bak` |
| L2 | Duplicate MAUI app dirs | `4_MobileApps` vs `apps/HRApp` |
| L3 | Health endpoint detail exposes process info | `Gateway /health/detail` — ensure not public in prod config |
| L4 | AI conversation retention exists; call recordings policy undefined | Voice stores events not media — confirm no recording retained |
| L5 | `Domain.cs` 7.3k lines single file | Maintainability, not security |

## C. Privacy & consent posture

- **Good:** tenant-scoped everything; AI conversations tenant-isolated + retention job; metering attribution metadata; consent records exist in ATRS/Membership; secrets never client-side.
- **Gaps:** no unified consent/preferences center; no data-subject export/delete workflow found; marketing-campaign consent capture unverified; AI disclosure text to end-users not verified; legal review of Law 91/2025 explicitly pending (W0 audit + project_state).
- **Recommendation:** treat consent registry + retention schedule + DSAR workflow as a P0/P1 compliance workstream before scale marketing.

## D. Reliability posture

- **Strong:** outbox pattern, idempotency, circuit breaker in MeteringClient (5-fail/60s), fail-open degradation policies, reconnect logic voice, CD smoke tests, multi-VPS routing, per-service health endpoints.
- **Weak:** backup/DR automation (H1), observability depth (M1), load-test evidence stale, single-region GCP (no multi-region failover documented), NATS single point (compose infra) — verify clustering.
- **Recommendation:** P0 = backup automation + restore drill + alerting baseline; P1 = NATS HA decision + load re-baseline.
