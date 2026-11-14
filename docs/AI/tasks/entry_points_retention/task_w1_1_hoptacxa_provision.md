# Task W1-1 — HOPTACXA PROVISION: profile `Hoptacxa` + instance `hoptacxa.timlathay.com`

> **Status:** PENDING · **Wave:** W1 — `docs/AI/plans/entry-points-retention-master-plan.md`
> **SRS refs:** FR-D1, FR-D2, FR-P1, FR-P2 · **Gate 5:** CÓ (thêm enum `KhachLinkProfile.Hoptacxa` — user đã duyệt qua SRS)
> **Phụ thuộc:** không — chạy song song W1-2

## Mục tiêu giá trị

`https://hoptacxa.timlathay.com` load được, hiển thị PWA với nav profile `Hoptacxa` (portal — không cart/orders/scan), manifest/icon riêng, label "Hợp tác xã".

## Scope

1. **`KhachLinkProfile.Hoptacxa`** — enum additive trong `1_Shared/.../KhachLinkAggregate`. Verify deserialize path (`Enum.TryParse` trong `KhachLinkInstanceHttpService`) — instance cũ/cache cũ fallback FullCommerce an toàn.
2. **Preset `GetPresetFlags`** trong `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstances.razor`:
   `ShowHome/Stores/Campaigns/Community/Profile = true`; `ShowCart/Orders/Scan/QrClaim/LoyaltyHistory/Missions/Rewards/AllianceWallet/StaffDashboard = false`; **`ShowJobs = false`** (đợi M1 ship `/jobs` mới bật).
3. **Labels:** dropdown admin "Hợp tác xã" + `GetProfileLabel` trong `KhachLinkLayout.razor` + `WhatsNewBanner`/`OnboardingTour` direction mapping (FullCommerce→Hoptacxa không cần tour đặc biệt — map `None` nếu chưa có design).
4. **Instance row:** tạo qua admin `KhachLinkInstances` — domain `hoptacxa.timlathay.com`, profile `Hoptacxa`, `OwnerTenantId = null`, Label "Hợp tác xã", IsActive=true. Icon/logo upload sau (FR-P3 — W3).
5. **Infra (manual, user/VPS):**
   - DNS: A record `hoptacxa.timlathay.com` → `136.85.94.119`.
   - Cert: `certbot certonly --expand -d timlathay.com -d hoptacxa.timlathay.com` (webroot, pattern `scripts/init-ssl-timlathay.sh`).
   - Không đổi nginx config (wildcard `*.timlathay.com` đã cover).

## Files

- `1_Shared/Domain/Aggregates/KhachLinkAggregate/` (enum — verify path khi implement)
- `5_WebApps/ShopERP/Components/Pages/Admin/KhachLinkInstances.razor` (preset + dropdown)
- `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` (`GetProfileLabel`)
- Tests: preset test + profile label test + deserialize fallback test (`KhachLinkInstanceHttpService` với `"Hoptacxa"` string)

## Tests (trước khi push)

- `GetPresetFlags(Hoptacxa)` trả đúng preset (unit test mới).
- ByDomainResponse `"Hoptacxa"` → parse đúng profile; string lạ → fallback FullCommerce.
- guard-check + `dotnet build VanAn.sln` + Core.Tests.

## RV checklist (sau deploy)

- [ ] `curl -I https://hoptacxa.timlathay.com` → 200, cert hợp lệ (không SNI mismatch).
- [ ] `/manifest.json` → name/short_name/icon riêng instance (không phải default).
- [ ] WASM load → bottom nav chỉ có: Trang chủ, Cửa hàng, Khuyến mãi, (Community nếu role), Tài khoản — KHÔNG có cart/scan/orders.
- [ ] `/api/v1/khachlink-instances/by-domain/hoptacxa.timlathay.com` → profile `"Hoptacxa"`, `ownerTenantId: null`.
- [ ] commienphi.timlathay.com regression — nav Reseller preset không đổi.
