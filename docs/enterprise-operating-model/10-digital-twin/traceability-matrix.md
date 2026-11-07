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

## Coverage

- Verified chain links: 16/27 activities fully code-verified; 9 partially (endpoint/handler names inferred); 2 manual (no software — legitimately UNVERIFIED).
- UNKNOWN open: exact `/api/membership-*` routes, OCR pipeline internals, POS notify mechanism, reverse `vanan.shoperp.*` subject names — enumerate at X0 drift run or targeted P3-followup.
