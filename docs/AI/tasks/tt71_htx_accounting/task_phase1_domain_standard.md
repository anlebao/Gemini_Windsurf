# TASK CARD: Phase 1 — Domain: AccountingStandard.TT71_2024 + TenantType.HTX

> **Master plan:** `docs/AI/plans/tt71-htx-accounting-master-plan.md`
> **Status:** PENDING — cần user approval (Gate 5 Domain exception). Q3 = CÓ (backfill).

## 1. OBJECTIVE

Thêm chuẩn kế toán HTX + kiểu tenant HTX vào Domain, mapping tenant HTX → TT 71 (+ backfill tenant HTX cũ).

## 2. GATES

- 🔴 **Gate 5 (Domain)** — user-approved exception bắt buộc (sửa `1_Shared/Domain.cs`)
- `AccountingEntry` 100% immutable — KHÔNG đụng
- Single-Identity Pattern — không đụng identity

## 3. CHANGES

| File | Change |
|---|---|
| `1_Shared/Domain.cs:3601` | `enum AccountingStandard` += `TT71_2024` (thêm CUỐI enum, không đổi thứ tự — tránh break persisted value) |
| `1_Shared/Domain.cs:3586` | `enum TenantType` += `HTX = 5` (sau Enterprise_Large=4) |
| `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` | Xác nhận `SetTenantType(TenantType, AccountingStandard)` có sẵn — HTX tenant dùng nó |
| `3_CoreHub/Services/Membership/HtxProfileService.cs` | `CreateProfileAsync`: sau khi tạo HtxProfile → `tenant.SetTenantType(TenantType.HTX, AccountingStandard.TT71_2024)` |
| **Backfill (Q3 = CÓ)** | Script idempotent (chạy khi deploy): mọi tenant có `HtxProfile` active mà `Type != HTX` → `SetTenantType(HTX, TT71_2024)` + save. Đặt ở startup (như `SEED_SHOP_INSTANCE_ID` pattern) hoặc admin endpoint 1 lần |

## 4. ACCEPTANCE

- [ ] `AccountingStandard.TT71_2024` + `TenantType.HTX` compile + tests Domain hiện có không regress
- [ ] Tạo HtxProfile → tenant trở thành HTX + standard TT71
- [ ] Backfill: tenant HTX cũ (HtxProfile active) → Type=HTX, Standard=TT71; tenant khác không đổi (idempotent)
- [ ] `dotnet build VanAn.sln` 0 errors + guard-check PASS

## 5. VERIFICATION

```powershell
dotnet build VanAn.sln
dotnet test 6_Tests\VanAn.Core.Tests --filter "FullyQualifiedName~Tenant"
```
