# TASK CARD: PWA-OFFLINE - Phase 6 - E2E Validation + Governance

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Validate end-to-end offline scenario via Playwright + RV on VPS + update governance docs (project_state.md, ADR-001).
- **Nghiệp vụ áp dụng:** Final validation trước merge `feature/khachlink-wasm` → `main`.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/playwright_validation.md` + `.devin/workflows/review.md`
- **Execution Mode:** REVIEW_ONLY (governance) + Playwright validation

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Section 1 correction)
  - `docs/Architecture/ADR001-Station-Architecture.md` (v3 addendum update)
  - `6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs` (rewrite for WASM)
  - `6_Testing/e2e-tests/` (new offline E2E spec)
  - `docs/AI/tasks/tech_debt_multi_vps_checkout.md` (mark TD-PWA-001 resolved)
- **Boundary Rules:**
  - KHÔNG thay đổi KhachLink code (validation only).
  - KHÔNG thay đổi Gateway/ShopERP.
  - Playwright governance: 1 spec only (offline scenario).

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **project_state.md Section 1:** "Blazor Server (NOT WASM)" → "Blazor WebAssembly" (now true).
- [ ] **ADR-001 v3 addendum:** Document KhachLink render mode = WASM.
- [ ] **KhachLinkStartupTests:** Rewrite cho WASM (bUnit TestContext hoặc DI smoke).
- [ ] **Playwright offline spec:** Load online → disconnect → navigate → checkout → reconnect → verify order.
- [ ] **TD-PWA-001:** Mark RESOLVED in tech_debt_multi_vps_checkout.md.
- [ ] **Remove Phase 0 quick fix** (replaced by real WASM offline).

## 5. SUCCESS CRITERIA
- [ ] SC1: `project_state.md` Section 1 = "Blazor WebAssembly (KhachLink PWA)".
- [ ] SC2: ADR-001 v3 addendum updated with WASM render mode.
- [ ] SC3: KhachLinkStartupTests rewritten + PASS.
- [ ] SC4: Playwright offline E2E spec created + PASS.
- [ ] SC5: RV on VPS: PWA install on real Android device + offline works.
- [ ] SC6: TD-PWA-001 marked RESOLVED.
- [ ] SC7: Phase 0 quick fix removed (offline shell replaced by real WASM).
- [ ] SC8: `dotnet build VanAn.sln` PASS.
- [ ] SC9: guard-check.ps1 PASS.
- [ ] SC10: All acceptance criteria from master plan Section 6 checked.

**Implementation Date:** _TBD_
**Branch:** `feature/khachlink-wasm` → merge to `main` after RV PASS

## 6. ACTIVE SKILLS (MAX 3)
- `playwright_guard` — E2E governance
- `playwright_cost_optimizer` — 1 spec only
- `domain-integrity-validation` — verify no Domain regressions

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 2
- **Verified Facts:**
  - Fact 1: KhachLinkStartupTests hiện 10 tests PASS (Server semantics)
  - Fact 2: Playwright E2E specs exist trong `6_Testing/e2e-tests/`
- **Assumptions:**
  - A1: bUnit available cho WASM component tests (verify package).
- **Open Questions:**
  - Q1: KhachLinkStartupTests rewrite approach — bUnit hay DI smoke? → verify bUnit availability.
  - Q2: Playwright offline simulation — Chrome DevTools Protocol `Network.emulateNetworkConditions`?
- **Recommended Action:** Proceed to REVIEW + Playwright validation.

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| project_state.md | Documentation change only | None |
| ADR-001 | Documentation change only | None |
| KhachLinkStartupTests | Test rewrite — may temporarily fail | Rewrite before merge |
| e2e-tests/ | New spec — no impact existing | Isolated |

## 9. TDD & E2E TESTING STRATEGY
- **Playwright E2E:** 1 offline scenario spec (per playwright_guard).
- **RV protocol:** VPS deploy + real Android device test.
- **Test boundary:** No unit tests (validation phase).

## 10. JIT PLANNING + PURE EXECUTION
| Session | JIT Planning | Pure Execution |
|---|---|---|
| S1 | Chốt KhachLinkStartupTests rewrite approach | Rewrite tests + verify PASS |
| S2 | Write Playwright offline E2E spec | Run spec + fix flaky |
| S3 | Update governance docs (project_state + ADR + tech debt) | Documentation |
| S4 | RV on VPS — real Android device | Deploy + test + report |

## 12. ESTIMATED EFFORT
- 2-3 sessions. **BLOCKER:** Phase 1-5 ALL must be complete.
