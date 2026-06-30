# Architecture Validation Layer Rules

**Created:** 2026-06-30
**Phase:** Phase 0 - Architecture Validation Layer Enhancement
**Status:** ACTIVE

## Overview

The Architecture Validation Layer is a set of automated checks that validate code vs deployment consistency to prevent architecture mismatches from reaching production. This layer was created in response to a critical gap where CoreHub was configured as a background service in code but as an HTTP service in docker-compose.prod.yml, which was not detected by existing validation.

## Purpose

1. **Detect Architecture Mismatches:** Validate that code architecture matches deployment configuration
2. **Prevent Configuration Drift:** Ensure docker-compose files reflect actual service types
3. **CI/CD Gatekeeping:** Block deployment of invalid configurations
4. **Documentation:** Provide clear validation rules for developers

## Validation Scripts

### 1. Docker Compose Validation (`scripts/validate-docker-compose.ps1`)

**Purpose:** Validate docker-compose configuration files for architecture consistency.

**Validations:**
- **File Existence:** Ensures docker-compose file exists
- **CoreHub Configuration:** Detects if CoreHub is incorrectly configured as HTTP service (should be background service)
- **Gateway Configuration:** Validates Gateway has required dependencies and healthchecks
- **Environment Variables:** Validates environment variable presence
- **Logging Configuration:** Ensures logging is configured for services
- **Required Services:** Validates that required services (gateway, shoperp, khachlink) are present

**Usage:**
```powershell
powershell -File scripts/validate-docker-compose.ps1 -ComposeFile docker-compose.prod.yml
```

**Exit Codes:**
- `0`: All validations passed
- `1`: Validation failed with errors

**Current Known Issues:**
- CoreHub validation currently fails (expected) - CoreHub is configured as HTTP service in docker-compose.prod.yml but should be background service. This will be fixed in Phase 2.

### 2. Environment Variable Validation (`scripts/validate-env-vars.ps1`)

**Purpose:** Validate environment variable configuration across environments.

**Validations:**
- **File Existence:** Ensures .env file exists
- **Required Variables:** Validates presence of required variables (POSTGRES_PASSWORD, JWT_SECRET_KEY, SEQ_ADMIN_PASSWORD)
- **Variable Naming:** Validates environment variable naming conventions
- **Empty Values:** Detects variables with empty values
- **Secret Strength:** Detects weak or default secret values
- **Docker Compose Consistency:** Validates that variables referenced in docker-compose are defined in .env

**Usage:**
```powershell
powershell -File scripts/validate-env-vars.ps1 -EnvFile .env
```

**Exit Codes:**
- `0`: All validations passed
- `1`: Validation failed with errors

## CI/CD Integration

### CI Pipeline (`.github/workflows/ci.yml`)

**Job:** `docker-compose-validation`
- **Runs after:** `build` job
- **Runs in parallel with:** `architecture-tests`, `gateway-startup`, `khachlink-startup`
- **Mode:** BLOCKING (fails pipeline if validation fails)
- **Timeout:** 5 minutes

**Steps:**
1. Checkout code
2. Validate docker-compose.prod.yml
3. Validate environment variables (using .env.example if available)

### CD Pipeline (`.github/workflows/cd.yml`)

**Job:** `pre-deployment-validation`
- **Runs after:** `build-and-push` job
- **Runs before:** `deploy` job
- **Mode:** BLOCKING (fails deployment if validation fails)
- **Timeout:** 5 minutes

**Steps:**
1. Checkout code
2. Validate docker-compose.prod.yml
3. Validate environment variables (using .env.example if available)

## Validation Rules

### Rule 1: CoreHub Must Be Background Service

**Description:** CoreHub is a background service (uses `Host.CreateDefaultBuilder`) and must NOT be configured as an HTTP service in docker-compose.

**Check:** Detects `ASPNETCORE_URLS` in CoreHub service configuration

**Severity:** ERROR (blocking)

**Current Status:** ❌ FAILING - CoreHub has `ASPNETCORE_URLS=http://+:80` in docker-compose.prod.yml

**Fix Phase:** Phase 2 - Docker Compose Production Fix

### Rule 2: Gateway Must Have Dependencies

**Description:** Gateway depends on other services and must have `depends_on` configuration.

**Check:** Validates presence of `depends_on:` in Gateway service

**Severity:** ERROR (blocking)

**Current Status:** ✅ PASSING

### Rule 3: Required Services Must Exist

**Description:** Gateway, ShopERP, and KhachLink are required services and must be defined in docker-compose.

**Check:** Validates presence of gateway, shoperp, and khachlink services

**Severity:** ERROR (blocking)

**Current Status:** ✅ PASSING

### Rule 4: Logging Must Be Configured

**Description:** All services should have logging configuration for observability.

**Check:** Validates presence of `logging:` in docker-compose

**Severity:** WARNING (non-blocking)

**Current Status:** ✅ PASSING

### Rule 5: Required Environment Variables Must Be Set

**Description:** Critical secrets and configuration must be set in environment.

**Check:** Validates POSTGRES_PASSWORD, JWT_SECRET_KEY, SEQ_ADMIN_PASSWORD

**Severity:** ERROR (blocking)

**Current Status:** ✅ PASSING (when .env is properly configured)

### Rule 6: Secrets Must Be Strong

**Description:** Default or weak secrets are not allowed in production.

**Check:** Detects common weak patterns (password=password, secret=secret, etc.)

**Severity:** ERROR (blocking)

**Current Status:** ✅ PASSING (when .env is properly configured)

## Architecture Consistency Tests

**File:** `6_Tests/VanAn.Architecture.Tests/ArchitectureConsistencyTests.cs`

**Purpose:** Programmatic validation of code vs docker-compose consistency.

**Tests:**
- `VA-CONSISTENCY-001`: Validates docker-compose.prod.yml exists and is valid YAML
- `VA-CONSISTENCY-002`: Validates CoreHub service type matches code architecture (currently FAILING - detects HTTP service bug)
- `VA-CONSISTENCY-003`: Validates Gateway service dependencies
- `VA-CONSISTENCY-004`: Validates environment variable consistency
- `VA-CONSISTENCY-005`: Validates container dependency graph

**Current Status:** 4/5 passing (1 expected fail detecting actual bug)

## Startup Test Enhancements

**Files:**
- `6_Tests/VanAn.Integration.Tests/GatewayStartupTests.cs`
- `6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs`

**Enhancements:**
- Added architecture validation to startup tests
- Validates that services don't have DbContext inappropriately injected
- Validates clean architecture boundaries

**Current Status:** ✅ PASSING

## Known Issues and Future Work

### Current Known Issues

1. **CoreHub HTTP Service Configuration (Expected)**
   - **Issue:** CoreHub configured as HTTP service in docker-compose.prod.yml
   - **Detection:** VA-CONSISTENCY-002 test and validate-docker-compose.ps1
   - **Impact:** Architecture mismatch between code and deployment
   - **Fix Phase:** Phase 2 - Docker Compose Production Fix
   - **Workaround:** None - validation correctly blocks deployment until fixed

2. **Docker Compose Syntax Check Disabled**
   - **Issue:** docker-compose command not available in all environments
   - **Workaround:** Syntax check disabled, only file existence validated
   - **Future:** Add YAML syntax validation library

### Future Enhancements

1. **Enhanced Regex Patterns**
   - Improve regex patterns for more precise validation
   - Add multiline regex support for complex YAML structures

2. **Additional Validation Rules**
   - Validate service resource limits
   - Validate healthcheck configurations
   - Validate network configurations
   - Validate volume mount configurations

3. **Environment-Specific Validation**
   - Add validation for docker-compose.dev.yml
   - Add validation for docker-compose.edge.yml
   - Add environment-specific variable validation

4. **Automated Fix Suggestions**
   - Provide suggested fixes for validation failures
   - Integrate with code review tools

## Maintenance

**Updating Validation Rules:**
1. Update validation scripts in `scripts/` directory
2. Update this documentation with new rules
3. Update ArchitectureConsistencyTests.cs if adding programmatic checks
4. Test changes locally before committing
5. Run CI pipeline to validate

**Adding New Validation Scripts:**
1. Create new script in `scripts/` directory
2. Follow existing naming convention: `validate-<name>.ps1`
3. Add to CI job in `.github/workflows/ci.yml`
4. Add to CD job in `.github/workflows/cd.yml`
5. Document in this file

## References

- **Master Plan:** `docs/AI/tasks/architecture_refactor_master_plan.md`
- **Phase 0 Task Card:** `docs/AI/tasks/phase0_validation_layer_enhancement_task_card.md`
- **CI Workflow:** `.github/workflows/ci.yml`
- **CD Workflow:** `.github/workflows/cd.yml`
- **Validation Scripts:** `scripts/validate-docker-compose.ps1`, `scripts/validate-env-vars.ps1`
- **Architecture Tests:** `6_Tests/VanAn.Architecture.Tests/ArchitectureConsistencyTests.cs`