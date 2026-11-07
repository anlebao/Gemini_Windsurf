# Responsibility Matrix (RACI) — Critical Flows

> SRS §4.3. R=Responsible · A=Accountable · S=Supporting · Appr=Approval authority · Sys=executing system.
> Legal responsibility caveats: see `unresolved-questions.md` Q3/Q4 — entries marked † need business-owner confirmation.

## F4 — Merchant onboarding & tenant provisioning

| Activity | R | A | S | Appr | Sys |
|---|---|---|---|---|---|
| Merchant acquisition (field) | Salesperson (ACTR-010) † | Vạn An Ecommerce | Hạt Nhân (ACTR-012) | Vạn An Ecommerce | — |
| Tenant application submit | Shop Owner (ACTR-002) | Shop Owner | Salesperson | — | Gateway/ShopERP |
| Application review | ApplicationStatus workflow (Draft→Submitted→NeedInfo→Approved) | Vạn An Ecommerce † | — | Vạn An Ecommerce operator (ACTR-014) | MembershipTypes.cs |
| Identity verification L1–L4 | Shop Owner | Shop Owner | OTP/Zalo (L1), Hạt Nhân (L4 manual) | L4 → Hạt Nhân/Vạn An † | — |
| Tenant provisioning (record, routing, SQLite) | Gateway/CoreHub (ACTR-020/022) | Vạn An Technology | — | automated after approval | TenantManagementService |
| Feature activation | TenantFeatureSettings | Shop Owner † | Vạn An ops | Shop Owner | ShopFeatureSettingsService |
| HTX marker (MarkAsHtx) | Vạn An ops / startup backfill | Vạn An Ecommerce † | — | Vạn An operator | HtxProfileService |

## F1 — Order lifecycle (Option C)

| Activity | R | A | S | Appr | Sys |
|---|---|---|---|---|---|
| Place order | Customer (ACTR-001) | Customer | — | — | KhachLink |
| Create order (PG, snapshot) | Gateway (ACTR-020) | Vạn An Technology | — | — | Gateway order creator |
| Route by ShopInstanceId | NATS (SYS-015) | Vạn An Technology | — | — | vanan.cloud.order.created.{si} |
| Display + confirm | Staff/Owner (ACTR-002/004) | Shop Owner | — | Shop Owner | ShopERP replica |
| Kitchen execute | Masterchef (ACTR-005) | Shop Owner | Staff | — | ShopERP |
| Status sync back | Sync workers (ACTR-022) | Vạn An Technology | — | — | vanan.shoperp.* |

## F2+F3 preview — metering & voucher (detail at X1)

| Activity | R | A | Appr | Sys |
|---|---|---|---|---|
| Quota check / deny | vanan-metering (ACTR-023) | Vạn An Technology | — | Metering API |
| Wallet top-up | Shop Owner | Shop Owner | Payment provider confirm | VietQR flow |
| Voucher posting | ChiefAccountant (ACTR-006) | Shop Owner † | per voucher type | VoucherTypeRegistry + strategy |

## Rules applied

- An ORGANIZATION never appears as R — humans/software execute; orgs appear only as A (§4.4).
- Software actors can be R (Gateway creates order) but never Appr for business decisions.
- † = legal/accountability assignment requires owner confirmation — not inferred from code (§16.5).
