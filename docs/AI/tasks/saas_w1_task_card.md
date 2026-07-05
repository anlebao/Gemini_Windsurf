# TASK CARD — SaaS W1: Secrets + Production Config Hardening

> **Status:** NOT STARTED | INVESTIGATE → PLAN → IMPLEMENT
> **Prerequisite:** VAS Stream F complete
> **Branch:** `feature/saas-w1-secrets-config-hardening`
> **Estimated sessions:** 1
> **Sprint:** 1 (Blockers)

## Objective
Remove all hardcoded secrets from production code. Replace placeholders in `appsettings.Production.json`. Ensure zero secrets in source code.

## Prerequisites (verify before code)
- [ ] Verify `5_WebApps/ShopERP/Program.cs:261` — OIDC default `your-secret-here`
- [ ] Verify `5_WebApps/ShopERP/Program.cs:341` — default password `VanAn@2026`
- [ ] Verify `3_CoreHub/Program.cs:77` — connection string with password
- [ ] Verify `5_WebApps/ShopERP/appsettings.Production.json` — 8 placeholders
- [ ] Grep all `__REPLACE_` patterns in config files

## Files to Modify
| File | Changes |
|------|---------|
| `5_WebApps/ShopERP/Program.cs:261` | REMOVE `?? "your-secret-here"` default — throw if missing |
| `5_WebApps/ShopERP/Program.cs:341` | REMOVE `?? "VanAn@2026"` default — throw if missing in Production |
| `3_CoreHub/Program.cs:77` | REMOVE `?? "Host=localhost;...Password=VanAn@2024!"` — throw if missing |
| `3_CoreHub/Program.cs:257` | REMOVE `?? "Host=localhost;...Password=VanAn@2024!"` — throw if missing |
| `5_WebApps/ShopERP/appsettings.Production.json` | Replace all `__REPLACE_*` with env var references `${VAR_NAME}` |
| `scripts/start-apps.ps1:41` | Remove hardcoded connection string, read from env |
| `scripts/seed-production-users.ps1:20` | Remove hardcoded password |
| `scripts/create-systemadmin.ps1:19` | Remove hardcoded password |

## Detailed Task List

### W1-T1: Remove default secrets from Program.cs
**Pattern:** Replace `?? "default-value"` with fail-fast:
```csharp
// BEFORE:
var secret = builder.Configuration["Jwt:Secret"] ?? "default-secret";
// AFTER:
var secret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret configuration is required.");
```

Apply to:
1. `ShopERP/Program.cs:261` — OIDC ClientSecret
2. `ShopERP/Program.cs:341` — Seed OwnerPassword (only throw in Production, keep default in Development)
3. `3_CoreHub/Program.cs:77` — DefaultConnection
4. `3_CoreHub/Program.cs:257` — ProjectMemory connection string

### W1-T2: Fix appsettings.Production.json
Replace all placeholders with environment variable references:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "${SQLITE_DB_PATH}",
    "Redis": "${REDIS_CONNECTION_STRING}"
  },
  "Jwt": {
    "Secret": "${JWT_SECRET_MIN_32_CHARS}"
  },
  "DataProtection": {
    "KeyDirectory": "${DATA_PROTECTION_KEY_DIR}"
  },
  "Brevo": {
    "ApiKey": "${BREVO_API_KEY}",
    "SenderEmail": "${BREVO_SENDER_EMAIL}"
  },
  "Esms": {
    "ApiKey": "${ESMS_API_KEY}",
    "SecretKey": "${ESMS_SECRET_KEY}",
    "BrandName": "${ESMS_BRANDNAME}"
  }
}
```

### W1-T3: Fix scripts
- `scripts/start-apps.ps1` — read connection string from env var, not hardcoded
- `scripts/seed-production-users.ps1` — require password as parameter
- `scripts/create-systemadmin.ps1` — require password as parameter

### W1-T4: Add startup validation
Add config validation in `Program.cs` for Production environment:
```csharp
if (builder.Environment.IsProduction())
{
    ValidateProductionConfig(builder.Configuration);
}
```
- Check all required config values present
- Check JWT secret length >= 32 chars
- Check DataProtection key directory exists and is writable
- Throw on missing with clear error message

### W1-T5: Build + guard + tests pass
- Verify test files with hardcoded JWT secrets are OK (test-only, not production)
- Build 0 errors, guard pass, all tests pass

## Verification
- [ ] `grep -r "VanAn@2026" 5_WebApps/ 3_CoreHub/` — 0 results in production code
- [ ] `grep -r "your-secret-here" 5_WebApps/` — 0 results
- [ ] `grep -r "VanAn@2024" 3_CoreHub/` — 0 results
- [ ] `grep -r "__REPLACE_" 5_WebApps/` — 0 results
- [ ] `appsettings.Production.json` — all values reference env vars
- [ ] Startup validation throws on missing config in Production
- [ ] Build 0 errors, guard pass, all tests pass

## Rollback
- Git revert (restore default values)
- If startup validation breaks tests: add test-specific config overrides

## Open Questions
- Q1: Seed OwnerPassword — keep default in Development? (Yes, only throw in Production)
- Q2: Test files with hardcoded JWT secrets — acceptable? (Yes, test-only)
- Q3: Environment variable format in appsettings.Production.json — `${VAR}` or `__VAR__`? (Verify .NET config supports)
