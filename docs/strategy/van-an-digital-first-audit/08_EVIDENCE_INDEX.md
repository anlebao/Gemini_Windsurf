# 08 — EVIDENCE INDEX

Map of every major audit conclusion → repository evidence. Statuses: ✅ verified (deployed+RV) · 🟡 code-only · ❌ absent (searched) · ❓ unknown.

---

## Platform services

| Module | Evidence | Status |
|---|---|---|
| Gateway | `2_Gateway/Program.cs`, `2_Gateway/Controllers/*` (~70), `docker-compose.gateway.yml`, `cd-multivps.yml` matrix | ✅ deployed (CD runs cited) |
| CoreHub | `3_CoreHub/Program.cs`, `3_CoreHub/Services/**` (~200 files), `3_CoreHub/Infrastructure/Migrations/*` | ✅ deployed in-process |
| ShopERP | `5_WebApps/ShopERP/Components/Pages/**` (150 razor), `Migrations/*`, `docker-compose.shoperp.yml` | ✅ deployed |
| KhachLink | `5_WebApps/KhachLink/Pages/*` (48), `Components/*`, `nginx.conf`, `docker-compose.khachlink.yml` | ✅ deployed |
| Directory | `5_WebApps/Directory/Components/Pages/{Home,StoreFinder,Audit}.razor`, `docker-compose.directory.yml` | ✅ deployed |
| Metering | `8_Metering/vanan-metering/src/**`, `docker-compose.metering.yml`, `.devin/rules/metering.md` | ✅ deployed :5050 |
| TenantAI | `9_TenantAI/vanan-ai/src/**` (Api/Ai/Domain/Infra/Contracts), `docker-compose` PG pgvector, port 5060 | ✅ deployed |
| Voice | `10_Voice/vanan-voice/src/**`, `docker-compose.voice.yml`, Coturn, port 5070 | ✅ deployed |
| ATRS | `11_Atrs/vanan-atrs/src/**` (Domain entities listed), `docker-compose.atrs.yml`, port 5080, `/metrics` | ✅ deployed |
| UI.Platform | `UI.Platform/Components/**`, `VanAn.UI.Platform.csproj` | ✅ library |
| Shared Domain | `1_Shared/Domain.cs` (7,331 lines, ~173 types), `1_Shared/Domain/Aggregates/**` | ✅ |
| Crawler | `7_Tooling/VanAn.Crawler/**`, `2_Gateway/Controllers/CrawlController.cs` | 🟡 |
| Mobile apps | `4_MobileApps/{HRApp,StationApp}/MainPage.xaml` (~36 lines), `apps/HRApp`, `apps/ecoNexus.Apps.sln` | ❌ stub / ❓ scope |

## Key feature evidence

### Identity & trust
- Tiered identity plan: `docs/AI/plans/van-an-identity-tiered-trust-master-plan.md`, `docs/AI/plans/identity-inventory-p0.md`
- ATRS: `11_Atrs/vanan-atrs/src/VanAn.Atrs.Domain/*.cs` (WebAuthnChallenge, DeviceCredential, RecoveryCode, DisputeCase, StaffPermissionGrant, SecurityPolicy, ConsentRecord, AccountTrustProfile…)
- RV sweeps: `6_Testing/rv-scripts/atrs-wave*-sweep*.mjs` + reports in project_state §3/§10
- Email OTP: `3_CoreHub/Services/Otp/*`, `5_WebApps/ShopERP/Services/OtpService.cs`, `CustomerIdentityController.VerifyOtp`, `6_Testing/e2e-tests/email-otp-login.spec.ts`, commit `41a1ac4d`/`072abaed`
- Passkey login: `atrs-passkey-login-rv.mjs`, commits `cb937c05`..`4b2f0dc0`

### Digital presence / discovery
- Profiles: `1_Shared/Domain/Aggregates/KhachLinkAggregate/KhachLinkProfile.cs` (6 profiles)
- Search: `2_Gateway/Services/TenantSearchService.cs`, `VanAnPublicSearchBar`, `VanAnDidYouMeanChip` (UI.Platform/Composite), spec `w3-2d` commits `23d41ceb`..`cb48993f`
- Directory: `5_WebApps/Directory/Components/Pages/*`, `CatalogService`, `InstanceConfigService`
- Instance gate: `3_CoreHub/Services/InstanceFeatureGate.cs`, `KhachLinkInstanceController`, commit `26701679`/`5a6f02bd` + spec `instance-feature-gate.spec.ts` 14 pass
- QR: `3_CoreHub/Services/QrCodeService.cs`, `Scan.razor`, `Claim.razor`, `Pages/Qr/*`

### AI layer
- Engine: `9_TenantAI/vanan-ai/src/VanAn.TenantAi.Ai/*` (IntentRouter, PolicyEngine, KnowledgeRetriever, AnswerValidator, PromptBuilder, UsagePermissionFilter)
- Public facade: `2_Gateway/Controllers/PublicTenantAiController.cs`, `PublicAssistantLinkController.cs`, `AssistantLinkController.cs`
- Entities/migrations: `AssistantLink` + `20261122072851_AddAssistantLink`; `ShopFeatureSettingsEntity.AIChat_Enabled`
- Widget: `UI.Platform/Components/Ai/AiChatWidget.razor`, `IAiChatClient`, `TenantAiPublicHttpService`
- Hosted page: `5_WebApps/KhachLink/Pages/Assistant.razor` (`/a/{slug}`)
- Knowledge pipeline: ingest `DocumentIngestRequest`/`ProductsBatchRequest`, `IngestSanitizer`, approval; W3a `628eb0e3`, W3b `aba02a5a` (menu OCR), W3c `ada4a3d8`+`586e9247` (URL crawl SSRF-guarded), W3d `f01e8ac2`+`7780010a` (mic+TTS), W3e `cf44e0d7` (did-you-mean+TTS price)
- W0 audit: `docs/AI/tasks/universal_ai_assistant/w0_audit/*` (9 docs), SRS `docs/requirements/SRS_VanAn_AI_Assistant_Platform_v2.0.md`, plan `docs/AI/plans/universal-ai-assistant-master-plan.md`
- Metering codes: `MeteringServiceCodes` (`ai.chat.message` Deny, `ai.chat.degraded`/`ai.embedding.batch` MeterOnly), `QuotaGate.cs`

### Voice / video
- Service: `10_Voice/vanan-voice/src/{Api,Contracts,Domain,Infra}`, `VoiceHub`, `CallService`, `PermissionService`, `MeteringClient`, `voice_calls`/`voice_call_events`
- Client: `VoiceCallPanel.razor`, voice-sdk/media JS, `RealtimeChatPanel`, `DeviceTokenValidator`
- Evidence: tests 112+/98/98; RV sweeps 12/12, sim 3/3, e2e 7/7, video 25/25; commits `b7ddbdd3`, `88d03d6b`, `9c47c47c`..`a0514941`, `fbc9a694`
- AI voice: absent except browser mic/TTS (W3d); provider audit `w0_audit/VOICE_PROVIDER_AUDIT.md` (Gemini Live candidate)

### Commerce
- Orders: `OrdersController`, `OrderFlowController`, `PublicOrdersController`, `OrderService`, `OrderFlowService`, `OrderSyncSubscriber`, Kitchen pages; Option C docs in `.windsurfrules` + state §1
- Booking: `3_CoreHub/Services/Booking/BookingService.cs`, `PublicBookingController`, `TenantBookingController`, ShopERP `Components/Pages/Booking/*` (12 pages), migrations `AddBookingScheduling`, `AddBookingIdempotencyAndConflictGuard`, `AddOfferingNeedsReview`, `AddOfferingIsSellable`; commits `5fee688d`, `6a11c4f3`
- Vouchers: `3_CoreHub/Services/Vouchers/*`, `VoucherTypeRegistry`, `VoucherTextParseService`, `LlmEnhanceVoucherTextParseService`, spec `SRS_ShopERP_VoucherFramework_v1.0.md`
- Accounting: `Accounting*Service`, `HKDBookService`, `Journal/*`, `GeneralLedger`, `PeriodClosingService`, regime-lock `615d85ac` RV 34/34; SRS `SRS_ShopERP_Accounting_Engine_v1.2.md`
- E-invoice: `3_CoreHub/Services/Providers/EInvoice/{Viettel,Misa}EInvoiceProvider.cs`, `EInvoiceProviderFactory/Registry`, `BatchInvoiceProcessor`, `DnsnBookService`, `WebhookController`, ShopERP `Components/Pages/EInvoice/*`
- Payment: `VietQrController` (URL gen only), `api/webhooks/payment`, `QrPaymentModal.razor`, `PaymentComplianceService`, `RefundOrchestrationService` (accrual; explicit no-VNPay/MoMo comment lines 20,71) — ❌ no gateway
- POS providers: `3_CoreHub/Services/Providers/POS/{KiotViet,Sapo}POSProvider.cs` — stub `Task.FromResult` 🟡/❌

### Channels
- FB leads: `3_CoreHub/Services/FacebookLeadService.cs`, `LeadManagementService`, `Lead` entity 🟡 (endpoint verify ❓)
- Social links: `5_WebApps/KhachLink/Components/SocialHub.razor` (outbound only)
- Notifications: `CompositeNotificationService` (Brevo `BrevoEmailService`, ESMS `EsmsNotificationService`, push no-op note), `PushNotificationService` (VAPID Web Push), `NotificationCenterService`, `PushNotificationBackgroundService`, `OutboxNotificationService`
- Absent: Messenger/WhatsApp/Telegram/Zalo customer adapters ❌; ZNS ❌; `OmnichannelService` stub ❌ (`OmnichannelService.cs:34` TODO)

### Ecosystem
- HTX/Cooperatives: `CooperativesController`, `Membership/` services, `HtxProfile`, `Member`, `HoptacxaPortal.razor`, commits `176d4aae`/`a3b2f2fc`; `/api/cooperatives` prod data verified
- Community commerce: `CommunityOrderService` (Haversine nearby + shipper accept), `CommunityFundService`, `CommunityAdminService`, `PromoCampaignService`, `SocialCampaignService`, `MissionService`, ShopERP `Community/*` pages, KhachLink `Disputes*`, `Missions`, `NearbyOrders/Products`
- Loyalty/alliance: `Loyalty*Service` family, `AllianceWalletService`, `InternalLoyaltyController`, `GlobalRedemptionCatalogController`, `LoyaltyReadRouter`, `LoyaltySyncSubscriber`; SRS `SRS — VẠN AN LOYALTY ALLIANCE.md`, spec `loyalty-alliance-spec.md`
- CTV/salesman: `SalesmanService`, `CollaboratorVerificationService`, `SalesmanQR.razor`, `SalesmanStore.razor`, `ReferralCommissionCalculator`, `ProductReferralConfigService`
- Jobs/group-buy: JobMarket profile exists; `/jobs` deferred M1; group-buy deferred M2 (project_state §4)

### Infrastructure
- Sync: `NatsSyncWorker.cs` (subject prefix `vanan.cloud.*`/`vanan.shoperp.*`), `SimpleOutboxProcessor`, subscribers `OrderSyncSubscriber`/`TenantSyncSubscriber`/`UserSyncSubscriber`/`LoyaltySyncSubscriber`/`PaymentConfirmedSubscriber`; subjects list verified by grep
- Metering: `8_Metering/vanan-metering` full service; `MeteringApiClient`, `IMeteringClient`, `PublicTopupController` (topup-options gate), `PublicMeteringController`
- Storage: `CloudinaryImageStorageService`, `R2StorageService`, `R2StorageController`, `R2CleanupService`, `ImageUploadController`
- Observability: Serilog in `2_Gateway/Program.cs:91`, `5_WebApps/ShopERP/Program.cs:57`; health `/health`,`/health/detail` (`Program.cs:2143-2221`), `/v1/health` per service; ATRS `/metrics` Prometheus — no tracing/APM found ❌
- Backup: `scripts/backup-db.sh` (SQLite only, 7-day); manual pg_dump verified once (ATRS W6a, project_state) — scheduled PG backup ❌
- CI/CD: `.github/workflows/cd-multivps.yml`, `deployment-impact.yml`, `scripts/ci-full.ps1`, `guard-check.ps1`, compose files per service
- Load tests: `6_Tests/VanAn.Load.Tests`, `6_Testing/load-tests` 🟡 (results not reviewed)
- Tests: `6_Tests/VanAn.sln` — Architecture/Core/Integration/E2E/Load/Omnichannel/OrderFlow/ShopERP/Unit test projects; per-service test projects (63 files in 8/9/10/11 tests dirs); Playwright ~174 spec files in `6_Testing`

## Docs baseline used

- `docs/knowledge-base/00-core/PROJECT_CONTEXT.md` (stack/ADRs)
- `docs/AI/project_state.md` §1–§5 (verified deployment/RV evidence)
- `docs/AI/tasks/universal_ai_assistant/w0_audit/*` (9 evidence docs, 2026-11-22)
- `docs/requirements/SRS_VanAn_AI_Assistant_Platform_v2.0.md`, `B. SRS – Vạn An Voice Service v1.1.md`, `van_an_tenant_ai_srs_v1.md`, `van_an_self_serve_onboarding_srs_v1.md`, `van_an_appointment_booking_srs_v1.1_mvp`, `SRS — Vạn An Loyalty Alliance.md`, `SRS — Vạn An Membership Infrastruct.md`, `van_an_phi_nen_tang_metering_srs_v1.md`
- `docs/specs/SRS_ShopERP_Accounting_Engine_v1.2.md`, `SRS_ShopERP_VoucherFramework_v1.0.md`, `SRS_EntryPoints_Retention_Upgrade_v1.0.md`
- `.devin/rules/governance.md`, `.windsurfrules` (architecture boundaries)

## Not verified this pass (explicit unknowns)

- Prod DB contents (tenant counts, wallet balances, rate-card seeds, corpus coverage per tenant)
- Facebook webhook endpoint registration + signature verification on prod
- E-invoice provider live credentials/usage
- Load-test latest results
- NATS cluster topology in prod compose
- `apps/` MAUI apps intended scope
