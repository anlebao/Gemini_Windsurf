# Task Card A — Issue #197: Rate-card theo nhóm tenant (Item 0 — Global → TenantType → TenantStatus → Tenant override)

> **Status:** CHỜ DUYỆT (2026-10-29)
> **Phase:** Item 0 (master plan §1) · **Gate:** build 2 solution 0 err + Metering.Tests + Core.Tests PASS
> **Căn cứ:** Issue #197 · master plan `phi-nen-tang-metering-issue197-upgrade-master-plan.md`
> **Quyết định:** D1=A header claims · D2 tách theo size · "chưa verify" = Pending

---

## 1. Mục tiêu

Cho phép cấu hình rate-card khác nhau theo: **loại hình tenant (HKD / Enterprise_SuperSmall / Enterprise_SME / Enterprise_Large / HTX)** · **trạng thái verify (verified=Active / unverified=Pending)** · **override riêng 1 tenant (ưu tiên cao nhất)**. Precedence resolve: **Tenant > TenantType > TenantStatus > Global** — miss → null (fail-open, không đổi hành vi GĐ-1).

## 2. Thay đổi

### 2.1 Domain (8_Metering/vanan-metering/src/VanAn.Metering.Domain)
| File | Action |
|---|---|
| `RateCardVersion.cs` | `RateCardScope` enum (Global=0 · TenantType=1 · TenantStatus=2 · Tenant=3) + properties `Scope` (default Global) + `ScopeValue` (string?) + `Create(...)` overload params |

### 2.2 Infra (VanAn.Metering.Infra)
| File | Action |
|---|---|
| `RateCardService.cs` | `ResolveForTenantAsync(serviceCode, tenantId?, tenantType?, tenantStatus?, atUtc)` — precedence 4 tầng · `CreateAsync` uniqueness per (ServiceCode, Scope, ScopeValue) + validation scope/value · `PatchAsync`/`DeactivateAsync` giữ nguyên · helper map enum↔string chuẩn (`TenantTypeScopeValue`, `TenantStatusScopeValue`) |
| `WalletService.cs` | `CheckAsync(...)` nhận tenantType/tenantStatus → resolve scoped |
| `UsageService.cs` | ingest (single + batch) nhận attributes → resolve scoped tại occurredAtUtc |
| `MeteringDbContext.cs` + `Program.cs` (`EnsureP7Schema`) | cột `Scope` + `ScopeValue` — idempotent DDL (chưa EF migrations tới P13) |

### 2.3 API + Contracts (VanAn.Metering.Api / Contracts)
| File | Action |
|---|---|
| `RateCardContracts.cs` | `RateCardCreateRequest`/DTO += `scope` + `scopeValue` (optional) |
| `Program.cs` | check + ingest đọc optional headers `X-Tenant-Type` / `X-Tenant-Status` · rate-card POST/GET trả scope · `/v1/rate-cards/resolve` nhận headers |

### 2.4 Callers (D1=A)
| File | Action |
|---|---|
| ShopERP `Services/MeteringApiClient.cs` | `ResolveTenantAttributesAsync()` — type từ `VasFeatureFlagService.GetTenantTypeAsync` (PG) · status từ mirror `Tenants` (Active→verified / Pending→unverified) · cache 60s · gửi header mọi check + ingest |
| Gateway `Services/MeteringApiClient.cs` | attributes từ Tenants PG (chat.*, feature.ocr) · store.visit anonymous → không header |

### 2.5 UI (ShopERP)
| File | Action |
|---|---|
| `Admin/MeteringRateCards.razor` | form += Phạm vi (Global/Loại hình/Trạng thái/Tenant) + ScopeValue động (dropdown 5 loại hình · dropdown verified/unverified · tenant picker `ListTenantsAsync`) · bảng += cột Phạm vi · resolve preview (nhập test attributes → hiện card theo precedence) |

## 3. Test matrix (thêm vào VanAn.Metering.Tests + ShopERP.Tests)

| # | Test |
|---|---|
| M1 | resolve precedence: Tenant override thắng TenantType thắng TenantStatus thắng Global |
| M2 | tạo 2 chain active cùng ServiceCode khác scope → OK (không "serviceCode_active_exists") |
| M3 | backward compat: không header/scope null → resolve Global như cũ |
| M4 | check/ingest nhận headers → deny/required tính theo card scoped đúng tầng |
| M5 | validation: scope=TenantType mà value không hợp lệ → 400 · scope=Tenant mà value không phải GUID → 400 |
| M6 | resolve preview API: trả đúng card theo attributes test |

## 4. File plan tổng

`RateCardVersion.cs` · `RateCardService.cs` · `WalletService.cs` · `UsageService.cs` · `MeteringDbContext.cs` · `Program.cs` (Api + Infra) · `RateCardContracts.cs` · ShopERP `MeteringApiClient.cs` · Gateway `MeteringApiClient.cs` · `MeteringRateCards.razor` · tests (~6 file)

## 5. Validation

`dotnet build VanAn.sln` 0 err · `dotnet build VanAn.Metering.sln` 0 err · `guard-check.ps1` ALL PASSED · `dotnet test` VanAn.Metering.Tests (M1-M6) + Core.Tests (regression) — ShopERP.Tests 8 fail pre-existing (Inventory `FeatureUsageRecorder` DI) không thuộc batch này.