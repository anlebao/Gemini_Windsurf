# Task Card W4: Merchant Referral — QR + Attribution + Payout (D2)

> **Status:** ⏳ PLANNED (awaiting session start)
> **Week:** W4 / 5
> **Effort:** ~1-1,5 tuần
> **Master plan:** `docs/AI/tasks/gtm_drill_mvp/master_plan.md`
> **Top-level card:** `docs/AI/tasks/gtm_drill_mvp/task_card.md`
> **Prerequisite:** ✅ W3 COMPLETE · ✅ Domain mod D2 APPROVED (2026-09-06)

## Objective

CTV giới thiệu merchant qua QR/link → claim attribution (referrer) → admin approve → manual payout hoa hồng (dynamic amount do SystemAdmin đặt). Mở rộng `Order.ReferralCode` (salesman product referral, đã có [V]) sang **merchant-level referral**.

**Hoa hồng = dynamic bởi SystemAdmin** (Decision #3, approved 2026-09-06): prefill từ SystemSetting `Referral_CommissionAmount`, chỉnh trong payout modal + option "đặt làm mặc định mới".

## Scope Checklist

### Task 4.1: DOMAIN MOD D2 (✅ APPROVED) — claim referral fields
**File:** `1_Shared/Domain/Aggregates/TenantAggregate/TenantClaimRequest.cs` — UPDATE (file theo vị trí thực tế khi implement)
- [ ] Thêm 2 nullable fields: `ReferredByCustomerId` (Guid?) + `ReferralChannel` (string? "qr"|"link") — backward compatible (existing rows NULL), không đổi identity
- [ ] KHÔNG đụng `AccountingEntry`/`BaseEntity`. Backward compatible — existing rows NULL.
- [ ] Không thêm business key VO (Single-Identity pattern giữ nguyên)

### Task 4.2: Migration
- [ ] PG migration: `TenantClaimRequests` + 2 cột (PG-only)
- [ ] SQLite migration: `TenantClaimRequests` + 2 cột (nếu Claim pipeline dùng SQLite — verify lúc implement)

### Task 4.3: Claim flow wiring
**Files:** `1_Shared/DTOs/.../ClaimDtos.cs` — UPDATE · `2_Gateway/Controllers/TenantClaimController.cs` — UPDATE · `3_CoreHub/Services/TenantClaimService.cs` — UPDATE
- [ ] `SubmitClaimRequest` DTO + `ReferrerCustomerId` field
- [ ] `TenantClaimController.Submit` — pass `ReferrerCustomerId` qua service
- [ ] `TenantClaimService.SubmitClaimAsync` — ghi nhận referrer vào `TenantClaimRequest.ReferredByCustomerId` + `ReferralChannel`

### Task 4.4: CTV refer page (KhachLink)
**File:** `5_WebApps/KhachLink/Pages/Refer.razor` — NEW (customer auth)
- [ ] Hiển thị link `{domain}/claim?ref={customerId:N}` + QR code (dùng qrcode.js official v1.4.4 vendored — precedent Guard QR fix `9f8495e9`)
- [ ] Copy link button (JS interop `navigator.clipboard.writeText`)
- [ ] `[Authorize]` — customer phải login để lấy `customerId`
- [ ] UI Platform components

### Task 4.5: Claim.razor đọc `?ref=`
**File:** `5_WebApps/KhachLink/Pages/Claim.razor` — UPDATE (đã mở ở Task 2.2)
- [ ] Đọc query param `ref` → parse `Guid` → pass qua `ClaimHttpService.SubmitClaim` → Gateway → service
- [ ] Backward compatible: nếu không có `ref` param, `ReferrerCustomerId = null`

### Task 4.6: Admin referral payout (manual MVP)
**File:** `5_WebApps/ShopERP/Pages/Admin/TenantManagement.razor` — UPDATE (tab "Referrals" hoặc mở rộng ClaimsQueue)
- [ ] List claim đã approve có referrer + nút "Tín dụng hoa hồng" → credit Wallet CTV (dùng phương thức credit có sẵn của `WalletService` — verify lúc implement)
- [ ] Amount **dynamic do SystemAdmin đặt**: prefill từ SystemSetting `Referral_CommissionAmount` (default 50.000đ [A]), chỉnh trực tiếp trong payout modal + option "đặt làm mặc định mới"
- [ ] **Manual payout MVP** — auto trigger khi có payment tracking = DEFER
- [ ] `[Authorize(Policy="RequirePlatformAdmin")]` (SystemAdmin only)

### Task 4.7: Tests
- [ ] Domain: `TenantClaimRequest` giữ referrer fields (2 nullable, backward compatible)
- [ ] Service: `SubmitClaimAsync` với/without referrer; approve → payout credit đúng amount
- [ ] Controller integration: `POST /api/v1/tenant-claims/submit` với `ReferrerCustomerId` → 200; without → 200 (backward compatible)
- [ ] Arch test: `TenantClaimRequest` không đụng `AccountingEntry`/`BaseEntity`

## Prerequisites

- ✅ W3 COMPLETE (metrics + dashboard live)
- ✅ Domain mod D2 APPROVED (2026-09-06)
- ✅ `TenantClaimRequest` entity (live)
- ✅ `TenantClaimController` + `TenantClaimService` (live)
- ✅ `WalletService` credit method (live — verify exact method signature lúc implement)
- ✅ `qrcode.js` official v1.4.4 vendored (Guard QR fix `9f8495e9`)
- ✅ `SystemSetting` table (live)
- ✅ KhachLink customer auth (live)
- ✅ Claim.razor `?name=` prefill (W2 done)

## Verification

1. **Build:** `dotnet build VanAn.sln` → 0 errors
2. **Migration:** PG/SQLite migration apply success — `TenantClaimRequests` + 2 cột mới tồn tại
3. **Backward compatible:** existing claim rows (NULL referrer) vẫn query được
4. **API:** `POST /api/v1/tenant-claims/submit` với `ReferrerCustomerId` → 200; without → 200
5. **KhachLink Refer page:** login → thấy link + QR; copy link → clipboard
6. **Claim flow:** `/claim?ref={customerId}` → submit → `TenantClaimRequest.ReferredByCustomerId` populated
7. **Admin payout:** approve claim có referrer → mở payout modal → prefill amount from SystemSetting → chỉnh amount → credit Wallet CTV → verify Wallet balance tăng
8. **Dynamic amount:** chỉnh amount trong modal → credit đúng amount; "đặt làm mặc định mới" → SystemSetting updated
9. **Tests:** unit + integration + arch ALL PASS
10. **E2E:** `gtm-referral.spec.ts` PASS (chạy sau build, theo Playwright rules)

## Governance checklist

- [ ] Domain purity: D2 = +2 nullable fields (backward compatible), không đổi identity. KHÔNG đụng `AccountingEntry`/`BaseEntity`.
- [ ] KhachLink HTTP-only: `ClaimHttpService` → Gateway, KHÔNG inject DbContext
- [ ] UI Platform: Refer.razor + TenantManagement Referrals tab dùng VanAn.UI.Platform components
- [ ] Không tạo .csproj mới
- [ ] Pattern #8: query tenant/customer so sánh property trực tiếp — cấm `EF.Property<Guid>`
- [ ] QR code: dùng qrcode.js official v1.4.4 vendored (KHÔNG dùng vendored corrupt — precedent `9f8495e9`)
- [ ] Manual payout MVP: admin confirm trước credit; auto trigger = DEFER
- [ ] Dynamic amount: SystemSetting `Referral_CommissionAmount` + editable trong modal

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Domain mod D2 phá backward compatibility | +2 nullable fields, existing rows NULL. Migration additive only. |
| R2 | WalletService credit method signature mismatch | Verify exact method signature lúc implement; nếu cần → adapt call |
| R3 | QR code render fail (vendored corrupt) | Dùng qrcode.js official v1.4.4 vendored (precedent `9f8495e9`) |
| R4 | Referrer fake (customer tự giới thiệu chính mình) | MVP: admin approve manual → verify referrer != claimer. Auto validation = DEFER. |
| R5 | Payout sai amount | Manual MVP (admin confirm); amount dynamic editable; verify Wallet balance sau credit |
| R6 | SystemSetting `Referral_CommissionAmount` not seeded | Default 50.000đ [A] in code fallback if SystemSetting NULL |

## Files

| # | File | Action | Status |
|---|---|---|---|
| 1 | `1_Shared/Domain/Aggregates/TenantAggregate/TenantClaimRequest.cs` | UPDATE (D2 +2 fields) | ⏳ |
| 2 | `1_Shared/DTOs/.../ClaimDtos.cs` | UPDATE (ReferrerCustomerId) | ⏳ |
| 3 | `2_Gateway/Controllers/TenantClaimController.cs` | UPDATE (referrer pass-through) | ⏳ |
| 4 | `3_CoreHub/Services/TenantClaimService.cs` | UPDATE (referrer attribution) | ⏳ |
| 5 | PG/SQLite migration | NEW (+2 columns) | ⏳ |
| 6 | `5_WebApps/KhachLink/Pages/Refer.razor` | NEW | ⏳ |
| 7 | `5_WebApps/KhachLink/Pages/Claim.razor` | UPDATE (?ref= prefill) | ⏳ |
| 8 | `5_WebApps/ShopERP/Pages/Admin/TenantManagement.razor` | UPDATE (Referrals tab) | ⏳ |
| 9 | `6_Tests/VanAn.Core.Tests/Growth/ReferralTests.cs` | NEW | ⏳ |
| 10 | `6_Tests/VanAn.Integration.Tests/Growth/ReferralFlowTests.cs` | NEW | ⏳ |
| 11 | `6_Testing/e2e-tests/gtm-referral.spec.ts` | NEW | ⏳ |

## Open questions (resolve before/during W4 session)

1. **WalletService credit method:** Exact method signature? `CreditAsync(customerId, amount, description)` hay khác? → verify lúc implement.
2. **Referrer = Customer hay Salesman?** `ReferredByCustomerId` = Customer (CTV commerce). Salesman referral đã có `Order.ReferralCode` [V]. → Confirm: W4 = Customer-level merchant referral (CTV giới thiệu merchant mới).
3. **Claim pipeline DB:** `TenantClaimRequest` ở PG hay SQLite? → verify lúc implement (affect migration target).
4. **Referral validation:** Referrer != claimer (cùng customer)? → MVP: admin manual verify. Auto validation = DEFER.

## Related

- Master plan: `docs/AI/tasks/gtm_drill_mvp/master_plan.md`
- Top-level card: `docs/AI/tasks/gtm_drill_mvp/task_card.md`
- W3 (done): `docs/AI/tasks/gtm_drill_mvp/task_card_w3_revenue_proof.md`
- W5 (next): `docs/AI/tasks/gtm_drill_mvp/task_card_w5_consent_flags_deploy.md`
- TenantClaimRequest (D2 target): `1_Shared/Domain/Aggregates/TenantAggregate/TenantClaimRequest.cs`
- WalletService (payout): `3_CoreHub/Services/WalletService.cs`
- qrcode.js vendored (precedent): Guard QR fix `9f8495e9`
- Order.ReferralCode (salesman precedent): `1_Shared/Domain.cs:1839`
