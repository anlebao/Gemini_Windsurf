# Business-to-Code Traceability Matrix — DRAFT (P3, finalize at P5)

> Chain per SRS §7.3: Activity → Module → Service/Handler → API/Event → DB Entity → Output.
> Unverifiable links = UNKNOWN/UNVERIFIED — never invented.

## F1 — Order lifecycle

| ACTV | Activity | Module | Handler/Service | API/Event | DB | Output | Status |
|---|---|---|---|---|---|---|---|
| ACTV-202 | Submit checkout | SYS-002 | `PublicOrdersController.CreateCheckoutOrder` | POST /api/public/orders/checkout | — | request | VERIFIED |
| ACTV-203 | Create order PG | SYS-002→003 | `OrderService.CreateOrderFromCommandAsync` | — | `Order` (DATA-004) | Order entity | VERIFIED |
| ACTV-204 | Route+publish | SYS-002/015 | publisher w/ routingKey | `vanan.cloud.order.created.{si}` | ShopInstance (DATA-002) | NATS msg | VERIFIED |
| ACTV-205 | Replica persist | SYS-004 | `OrderSyncSubscriber` | subscribed subject | order replica (DATA-005) | SQLite row | VERIFIED |
| ACTV-206 | Notify POS/kitchen | SYS-004 | UNVERIFIED (SignalR inferred) | — | — | display | INFERRED |
| ACTV-207 | Confirm order | SYS-004 | UNVERIFIED (UI flow) | — | replica status | status | INFERRED |
| ACTV-208 | Kitchen prepare | SYS-004 | `KitchenStatus` transitions | — | replica | status | INFERRED→V (enum exists) |
| ACTV-209 | Status sync back | SYS-009/015 | subscribers + NatsSyncWorker | `status.changed.{si}`, `vanan.shoperp.*` | DATA-004/005 | PG update | VERIFIED |
| ACTV-210 | Track order | SYS-005→002 | `GetPublicOrder` | GET /api/public/orders/{id} | DATA-004 | DTO | VERIFIED |
| ACTV-211 | Loyalty accrual | SYS-003 | `AllianceWalletService`/`LoyaltyBalanceSyncPublisher` | `loyalty.changed.{deviceId}` | wallet/loyalty (TBD DATA) | points | VERIFIED |
| ACTV-212 | Payment confirmed | SYS-003 | `WalletService` (comment) | `payment.confirmed.{si}` | wallet (DATA-010) | event | INFERRED |

## F4 — Onboarding/provisioning

| ACTV | Activity | Module | Handler/Service | API/Event | DB | Output | Status |
|---|---|---|---|---|---|---|---|
| ACTV-102 | Submit application | SYS-002 | `MembershipApplicationsController` (I) | membership app endpoints | DATA-007 | Draft→Submitted | INFERRED (endpoint names) |
| ACTV-103 | OCR extract | SYS-002 | `OcrOnboardingController` | OCR endpoints | — | fields | INFERRED |
| ACTV-104 | Review application | SYS-002 | `MembershipAdminController` (I) | admin endpoints | DATA-007 | Approved/NeedInfo/Rejected | VERIFIED (state machine) |
| ACTV-105 | KYC L1 OTP | SYS-002 | UNVERIFIED handler | — | verification level | flag | VERIFIED (enum) / I (impl) |
| ACTV-107 | Create tenant | SYS-003 | `TenantManagementService.CreateTenantAsync` | — | `Tenant` (DATA-001) | entity | VERIFIED |
| ACTV-108 | Assign ShopInstance | SYS-003 | `AssignShopInstanceAsync` (+capacity) | — | DATA-002 | link | VERIFIED |
| ACTV-109 | Publish verified | SYS-009/015 | `TenantEvents`/publisher | `tenant.verified` | — | event | VERIFIED |
| ACTV-110 | Mirror tenant | SYS-004 | `TenantSyncSubscriber` | subscribed | DATA-003 | SQLite row | VERIFIED |
| ACTV-111 | Bind domain/instance | SYS-002 | `KhachLinkInstanceController`/`DomainRegistrarController` | endpoints (I) | — | binding | INFERRED |
| ACTV-113 | MarkAsHtx | SYS-003 | `HtxProfileService` | `tenant.type.changed` | DATA-012 | HTX marker | VERIFIED |

## VALCN — Community commerce (D1)

| ACTV | Activity | Module | Handler/Service | API/Event | DB | Output | Status |
|---|---|---|---|---|---|---|---|
| ACTV-610 | Resolve mode + snapshot | SYS-003 | `CommerceModeService.ResolveModeForTenantAsync` | — | `SystemSetting`/`Tenant` (DATA-013) | Order snapshot | VERIFIED |
| ACTV-611 | Configure mode/rates | SYS-002 | `CommerceModeController` | POST /api/admin/commerce-mode/* | DATA-013 | settings | VERIFIED |
| ACTV-612 | Nearby orders | SYS-002 | `CommunityOrderService.GetNearbyOrdersAsync` | GET /api/community/nearby-orders | DATA-004/013 | DTO list | VERIFIED |
| ACTV-613 | Accept delivery | SYS-002/003 | `CommunityOrderService.AcceptOrderAsync` | POST /api/community/orders/{id}/accept | `DeliveryTask`+`Order` (DATA-013/004) | task + delivering | VERIFIED |
| ACTV-614 | Delivery transitions | SYS-002/003 | `DeliveryWorkflowService.TransitionStatusAsync` → `OrderWorkflowService` | POST …/pickup\|delivering\|delivered\|failed | DATA-013 | status + JE | VERIFIED |
| ACTV-615 | GPS ping + push | SYS-002 | `DeliveryWorkflowService.RecordLocationAsync` + `LocationHub` | POST /api/community/location/update | `DeliveryTracking` (DATA-013) | SignalR push | VERIFIED |
| ACTV-616 | Payment split + fund accrual | SYS-003 | `WalletService` (COD/external legs) + `OrderService.GenerateResellerAccountingEntriesAsync` | POST …/wallet/confirm-cod · confirm-external-payment | `WalletTransaction` (DATA-010) + `AccountingEntry` (DATA-009) | wallet legs + 3 booksets | VERIFIED |
| ACTV-617 | Fund spend + audit | SYS-002/003 | `CommunityFundService.SpendAsync` | POST /api/admin/community-fund/spend | DATA-010 + `CommunityFundSpendRecord` (DATA-013) | spend tx + audit | VERIFIED |
| ACTV-618 | COD remittance | SYS-002/003 | `WalletService` (Remittance+Settlement pair) | POST /api/community/wallet/remit | DATA-010 | settlement | VERIFIED |
| ACTV-619 | Eligibility eval | SYS-002/003 | `CommunityAdminService.GetEligibleCustomers{ForTenant}Async` | GET …/community/eligible · …/tenant-community/eligible | DATA-013 | DTO page | VERIFIED |
| ACTV-620 | Role activate/deactivate | SYS-002/003 | `CommunityAdminService.ActivateRole{ForTenant}Async` | POST …/activate-role\|deactivate-role | `CommunityRole` (DATA-013) | role state | VERIFIED |
| ACTV-621 | Referral & commission | SYS-003 | `SalesmanService` + `ReferralCommissionCalculator` | /api/community/salesman/*, /resolve-referral | `SalesReferral` (DATA-013) | commission | INFERRED (call chain partial) |

## Guard parking — giữ xe QR (D1 addendum)

| ACTV | Activity | Module | Handler/Service | API/Event | DB | Output | Status |
|---|---|---|---|---|---|---|---|
| ACTV-622 | Issue session | SYS-002/003 | `GuardService.IssueAsync` | POST /api/guard/issue | `VehicleSession` (DATA-014) | QR + short code + ticket | VERIFIED |
| ACTV-623 | Photo upload | SYS-002 | `GuardService.PresignUploadAsync`/`UploadPhotoAsync` | POST /api/guard/{presign-upload,upload-photo} | R2 keys (DATA-014) | photo keys | VERIFIED |
| ACTV-624 | Claim session | SYS-002/003 | `GuardController.Claim` → `GuardService.ClaimAsync` | POST /api/guard/claim | DATA-014 | Claimed | VERIFIED |
| ACTV-625 | Verify scan | SYS-002/003 | `GuardService.VerifyAsync` | POST /api/guard/verify | DATA-014 + `GuardScanLog` | verify result + log | VERIFIED |
| ACTV-626 | Checkout | SYS-002/003 | `GuardService.CheckoutAsync` | POST /api/guard/checkout/{id} | DATA-014 | CheckedOut | VERIFIED |
| ACTV-627 | Flag/Void | SYS-002/003 | `GuardService.FlagAsync`/`VoidAsync` | POST /api/guard/{flag,void}/{id} | DATA-014 | Flagged/Voided | VERIFIED |
| ACTV-628 | Board + sync | SYS-002 | `GuardService.GetTodaySessionsAsync`/`GetSessionStatusesAsync` | GET …/sessions/today · POST …/my-sessions | DATA-014 | DTOs | VERIFIED |

## Coverage

- Verified chain links: 16/27 activities fully code-verified; 9 partially (endpoint/handler names inferred); 2 manual (no software — legitimately UNVERIFIED). VALCN D1 (added 2026-11-08): +12 activities — 11 VERIFIED, 1 INFERRED (ACTV-621). Guard parking D1 addendum: +7 activities — all VERIFIED (ACTV-622..628).
- UNKNOWN open: exact `/api/membership-*` routes, OCR pipeline internals, POS notify mechanism, reverse `vanan.shoperp.*` subject names, `PublishDeliveryStatusUpdateAsync` channel (SignalR vs NATS), SMS-OTP deposit wallet flow — enumerate at X0 drift run or targeted P3-followup.
