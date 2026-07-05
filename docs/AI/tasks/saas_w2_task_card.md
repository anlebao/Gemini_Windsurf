# TASK CARD — SaaS W2: .NET SDK Upgrade + Package Security

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** VAS Stream F complete
> **Branch:** `feature/saas-w2-dotnet-upgrade-package-security`
> **Estimated sessions:** 1
> **Sprint:** 1 (Blockers)

## Objective
Upgrade .NET SDK 8.0.100 → 8.0.22+ to patch CVEs. Replace outdated auth packages 2.3.0 with 8.0.x versions.

## Prerequisites (verify before code)
- [ ] Verify `dotnet --version` — current 8.0.100
- [ ] Verify `global.json` — SDK version pinning (if exists)
- [ ] Verify `Directory.Packages.props:54-56` — auth packages 2.3.0
- [ ] Check for `Directory.Packages.props` central package management
- [ ] Grep all `.csproj` files for `TargetFramework` — confirm all `net8.0`

## Files to Modify
| File | Changes |
|------|---------|
| `global.json` (if exists) | Update SDK version to 8.0.22+ |
| `Directory.Packages.props:54-56` | Replace `Microsoft.AspNetCore.Authentication` 2.3.0 → 8.0.x |
| `Directory.Packages.props:24-39` | Upgrade Microsoft.Extensions packages to latest 8.0.x |
| All `.csproj` files | Verify `TargetFramework=net8.0` (no change needed) |
| `docker-compose.yml` | Update base image tag if hardcoded .NET version |

## Detailed Task List

### W2-T1: Install .NET SDK 8.0.22+
```bash
# Download and install .NET 8.0.22 SDK
# Verify: dotnet --version should show 8.0.22x
```
- Update `global.json` if exists:
```json
{
  "sdk": {
    "version": "8.0.22x",
    "rollForward": "latestPatch"
  }
}
```

### W2-T2: Replace outdated auth packages
**File:** `Directory.Packages.props:54-56`
```xml
<!-- BEFORE -->
<PackageVersion Include="Microsoft.AspNetCore.Authentication" Version="2.3.0" />
<PackageVersion Include="Microsoft.AspNetCore.Authentication.Core" Version="2.3.0" />
<PackageVersion Include="Microsoft.AspNetCore.Authentication.Abstractions" Version="2.3.0" />

<!-- AFTER: Remove these — they are .NET Framework 2.x packages, not needed in .NET 8 -->
<!-- Microsoft.AspNetCore.Authentication.JwtBearer (8.0.8) already covers this -->
<!-- Microsoft.AspNetCore.Authentication.Cookies (already at 2.3.9 → upgrade to 8.0.x) -->
```
- Remove `Microsoft.AspNetCore.Authentication` 2.3.0 (framework package, not needed)
- Remove `Microsoft.AspNetCore.Authentication.Core` 2.3.0
- Remove `Microsoft.AspNetCore.Authentication.Abstractions` 2.3.0
- Upgrade `Microsoft.AspNetCore.Authentication.Cookies` 2.3.9 → 8.0.x

### W2-T3: Upgrade Microsoft.Extensions packages
**File:** `Directory.Packages.props:24-39`
- Upgrade all `Microsoft.Extensions.*` from 9.0.3 → 9.0.x latest (or 8.0.x for consistency)
- Upgrade `Microsoft.Extensions.Hosting` 8.0.1 → 8.0.x latest
- Upgrade `Microsoft.Extensions.Caching.StackExchangeRedis` 8.0.8 → 8.0.x latest
- Upgrade `Microsoft.Extensions.Diagnostics.HealthChecks` 8.0.0 → 8.0.x latest

### W2-T4: Build + fix breaking changes
- `dotnet restore` — update packages
- `dotnet build VanAn.sln` — fix any breaking changes from package upgrades
- Common issues: namespace changes, API signature changes, obsolete warnings → errors

### W2-T5: Test login flow
- Run tests that cover authentication:
  - `JwtTokenServiceTests.cs`
  - `HKDBooksEndpointTests.cs` (uses JWT)
  - `TenantOnboardingApiTests.cs` (uses JWT)
- Verify cookie auth still works after package upgrade
- Verify OIDC still works (if configured)

### W2-T6: Build + guard + all tests pass
- Build 0 errors, guard pass, all 1114+ tests pass

## Verification
- [ ] `dotnet --version` — 8.0.22+
- [ ] `Directory.Packages.props` — no 2.3.0 auth packages
- [ ] `Directory.Packages.props` — all Microsoft.Extensions at latest 8.0.x or 9.0.x
- [ ] Login flow tests PASS (JWT + Cookie + OIDC)
- [ ] Build 0 errors, guard pass, all tests pass

## Rollback
- Git revert (restore old package versions)
- If SDK upgrade breaks: install old SDK, restore `global.json`
- If package upgrade breaks: pin to previous working version

## Open Questions
- Q1: Stay on .NET 8 LTS or jump to .NET 9? (Decision: D4 — stay on .NET 8)
- Q2: Microsoft.Extensions 9.0.3 → 9.0.x latest or downgrade to 8.0.x? (Investigate compatibility)
- Q3: BCrypt.Net-Next 4.0.3 → 5.0.x? (Check breaking changes first)
