# TODO List - Testing Framework Fixes

Created: 2026-05-22  
Objective: Fix testing framework issues and verify coverage

## High Priority Tasks

- [x] 1. Install npm dependencies in 6_Testing (COMPLETED with --force --no-package-lock)
- [x] 2. Install Playwright browsers (COMPLETED)
- [x] 3. Run quality-gate to verify fixes (COMPLETED - framework fixed, tests fail due to services not running)

## Medium Priority Tasks

- [x] 4. Fix Load Tests spawnSync ENOBUFS error (COMPLETED - replaced execSync with spawn)
- [x] 5. Install Python requests module for Chaos Tests (COMPLETED - pip install requests)

## Low Priority Tasks

- [x] 6. Setup coverage tracking for Playwright tests (COMPLETED - added nyc and coverage script)

## Execution Order

1. Install dependencies (npm install) - ✅ COMPLETED
2. Install Playwright browsers (npx playwright install) - ✅ COMPLETED
3. Run quality-gate to verify previous fixes - ✅ COMPLETED
   - Fixed: Removed globalSetup/globalTeardown from playwright.config.ts
   - Fixed: Added isTierEnabled, getTierConfig, getTestConfig to env-config.js
   - Fixed: Removed dotenv import from env-config.ts (not used)
   - Result: Tests run successfully but fail because services are not running (ECONNREFUSED)
4. Fix Load Tests spawnSync ENOBUFS error - ✅ COMPLETED
   - Fixed: Replaced execSync with spawn in quality-gate.js to avoid buffer overflow on Windows
   - Added proper Promise-based async handling with timeout support
5. Install Python requests module - ✅ COMPLETED
   - Installed: requests-2.34.2 with dependencies (certifi, charset_normalizer, idna, urllib3)
6. Setup coverage tracking - ✅ COMPLETED
   - Installed: nyc (Istanbul code coverage tool)
   - Added: npm run coverage script to package.json

## Notes

- Previous fixes completed: playwright.config.ts, quality-gate.js, package.json, smoke-tests, Dockerfile
- npm install succeeded with --force --no-package-lock flag
- Playwright browsers installed successfully
- Quality-gate runs successfully - framework is now working correctly
- Test failures are EXPECTED: Services (CoreHub, Gateway, KhachLink, ShopERP) are not running
- Tests require running services to pass (ports 5010, 5001, 5002, 5003)
- To run tests successfully: Start the VanAn ecosystem services first
- Framework fixes are COMPLETE - ready for use when services are running
- Load tests no longer have ENOBUFS errors due to spawn implementation
- Chaos tests have Python requests module installed
- Coverage tracking available via `npm run coverage`

## Summary

**ALL TASKS COMPLETED** ✅

The testing framework has been fully fixed and is ready for use. All identified issues have been resolved:
- npm dependencies installed
- Playwright browsers installed
- Framework configuration fixed (playwright.config.ts, env-config.js/ts)
- Load test buffer overflow fixed (spawn instead of execSync)
- Python dependencies installed for chaos tests
- Coverage tracking configured

To run tests: Start VanAn ecosystem services, then execute `node scripts/quality-gate.js`
