# Playwright QC Platform Hardening Plan

## Status

**Approved for documentation update. Do not implement code from this plan until separately approved.**

## Objective

Build Playwright into a deep, reliable QC automation platform that can cover most repetitive quality-control work while keeping human QC focused on exploratory testing, business judgment, and release risk review.

This plan is not a single bug fix. It is a platform hardening plan for scalable E2E automation.

## Target Outcome

Playwright should provide:

- Stable critical-path regression coverage.
- Deterministic accounting-grade test data and cleanup.
- Reliable diagnostics when failures occur.
- Clear separation between app bugs, test bugs, infrastructure bugs, and flaky timing.
- CI-friendly execution tiers.
- Governance to avoid AI/test rerun loops and assertion weakening.

## Guiding Principles

1. Playwright tests are verification contracts, not random browser scripts.
2. A failed test must produce enough evidence to diagnose without guessing.
3. Tests must not depend on unstable CSS selectors, timing, or old shared data.
4. Full test coverage must be tiered by cost and business risk.
5. Automated tests should not replace human product judgment, only repetitive regression labor.

## Workstream 1: Test Taxonomy

Define test categories:

### Tier 0: Smoke Tests

Purpose: fast confidence after every change.

Examples:

- Login page loads.
- Authenticated home page loads.
- Main navigation loads.
- Accounting page loads.

Expected runtime: under 2 minutes.

### Tier 1: Critical Path Tests

Purpose: verify core business workflows.

Examples:

- Create expense entry.
- Create revenue entry.
- View accounting history.
- View balance dashboard.
- Create order.
- Update kitchen/order state.

Expected runtime: 5-10 minutes.

### Tier 2: Regression Suite

Purpose: broad feature coverage before merge/release.

Examples:

- Validation messages.
- Role/permission boundaries.
- Edge-case form values.
- Multi-step workflows.
- Error states.

Expected runtime: 15-45 minutes depending on scope.

### Tier 3: Nightly/Release Gate

Purpose: expensive checks.

Examples:

- Visual regression.
- Cross-browser checks.
- Mobile viewport checks.
- Data integrity checks.
- Long-running flows.
- Chaos/local-first/offline scenarios.

## Workstream 2: Selector Contract Strategy

Current risk:

- Tests often rely on text or CSS classes such as `.vanan-alert__message`.
- CSS and visual text can change without breaking business behavior.

Target strategy:

Use stable `data-testid` selectors for critical UI elements.

Examples:

```razor
<button data-testid="expense-submit" type="submit">
```

```razor
<div data-testid="expense-success-alert">
```

```razor
<input data-testid="expense-amount" />
```

Rules:

- Critical workflow elements should have explicit test IDs.
- Styling classes must not be primary selectors for E2E tests.
- Text assertions are acceptable for user-visible messages, but not as the only element selector.
- Test IDs should follow domain naming, not implementation naming.

Recommended naming:

- `expense-date`
- `expense-amount`
- `expense-account`
- `expense-vendor`
- `expense-category`
- `expense-description`
- `expense-submit`
- `expense-success-alert`
- `expense-error-alert`

## Workstream 3: Deterministic Test Data for Vạn An Accounting Domains

Current risk:

- Tests may reuse shared local database state.
- Failures may depend on previous test runs.

Target strategy:

Use test-only reset/seed APIs as the primary strategy for Tier 1 and Tier 2 workflows. Unique runtime data names are still useful, but they are not sufficient for accounting, tax, invoice, and ledger workflows.

Why random-only data is not enough:

- Expense/revenue flows depend on valid accounting accounts.
- Ledger entries must remain balanced.
- Tax and invoice flows depend on legal configuration.
- Cash register invoice flows may require valid symbol/range settings.
- Tenant, user, role, opening balance, and product/service catalogs must be consistent.

Supplemental unique data pattern:

```ts
const runId = `${test.info().workerIndex}-${Date.now()}`;
const vendor = `PW Vendor ${runId}`;
```

Data rules:

- Tier 1 and Tier 2 tests should start from an approved seeded business snapshot.
- Tests should create their own transaction-level data on top of the seeded snapshot.
- Tests should clean up, reset, or isolate data.
- Tests should not depend on previous manual data.
- Critical tests should verify database/API state where appropriate.

Primary seed/reset approach:

- Provide test-only endpoints in Development/Testing/Staging only.
- Protect endpoints with configuration and a secret token.
- Disable endpoints by default in production.
- Seed a standard household-business snapshot before critical suites.

Example endpoints:

```http
POST /test-support/reset
POST /test-support/seed/household-business-standard
```

Required protection:

- `ENABLE_TEST_ENDPOINTS=true`
- `X-Test-Seed-Token`
- Environment must be `Development`, `Testing`, or approved staging.

Minimum seed snapshot for Vạn An:

- Tenant / household business profile.
- Admin user and role assignments.
- Accounting chart of accounts.
- Opening balances.
- Tax configuration.
- Cash register invoice symbol/range configuration.
- Product/service catalog.
- Payment method catalog.
- Known sample vendors/customers.

Recommended first step:

Build one seed snapshot for the accounting golden flow. Keep unique runtime data names as a supplement to prevent transaction collisions.

## Workstream 4: Authentication Strategy

Current risk:

- Login flow runs before each test.
- Auth failures can cascade into unrelated test failures.

Target strategy:

Use `storageState` for most authenticated tests.

Plan:

1. Create a global setup that logs in once.
2. Save authenticated storage state.
3. Reuse storage state across specs.
4. Keep separate tests for authentication lifecycle.

Rules:

- Do not test login in every workflow spec.
- Login tests should be isolated in auth specs.
- Business workflow tests should start already authenticated.

## Workstream 5: Failure Diagnostics

Every failure should produce:

- Screenshot.
- Video.
- Trace.
- Browser console logs.
- Network errors.
- Server log correlation.
- Test run ID.

Recommended additions:

- Attach console errors to test output.
- Capture failed request URLs and status codes.
- Add correlation ID to test data names.
- Log server-side business action entry points.

Failure classification:

- App bug.
- Test contract bug.
- Test data issue.
- Infrastructure/server startup issue.
- Flaky timing issue.
- Environment dependency issue.

## Workstream 6: Execution Governance

Rules:

- Do not rerun until green.
- Run targeted specs first.
- Max one rerun per classified flaky failure.
- If a spec fails twice, diagnose artifacts before editing code.
- Do not weaken assertions without review.
- Do not modify user-owned `.spec.ts` files without explicit approval.
- AI must not modify Tier 0 or Tier 1 `.spec.ts` files without Tech Lead or QA Lead approval.
- AI must classify the failure before proposing changes to assertions.
- AI must never weaken assertions only to make a failing test pass.
- If an assertion is changed, the reason must be documented as a business contract update, selector contract update, or obsolete test behavior.

Recommended workflow:

1. Collect failure artifacts.
2. Classify failure.
3. Identify owner: app, test, infra, data.
4. Fix smallest responsible surface.
5. Run targeted verification once.
6. Escalate if no measurable progress after three iterations.

## Workstream 7: CI Integration

Suggested CI tiers:

### Pull Request

Run:

- Build.
- Unit tests.
- Tier 0 smoke Playwright.
- Selected Tier 1 tests based on changed area.

### Main Branch

Run:

- Full Tier 1 critical path.
- Important Tier 2 regression.

### Nightly

Run:

- Full regression suite.
- Cross-browser.
- Visual checks.
- Mobile viewport checks.

### Release Gate

Run:

- Full Tier 0-3 suite.
- Manual QC exploratory checklist.
- Business owner approval for high-risk flows.

## Workstream 8: Reporting Dashboard

Target report should show:

- Pass/fail by tier.
- Flaky tests count.
- Top failing specs.
- Failure classification.
- Time trend.
- Coverage by business workflow.
- Last successful build per module.

Potential artifacts:

- Playwright HTML report.
- JSON summary.
- Markdown execution ledger.
- CI annotations.

## Workstream 9: Component Testability Standards

For every critical UI component:

- Must expose stable selectors.
- Must render deterministic states.
- Must show accessible names where appropriate.
- Must not require arbitrary sleeps.
- Must expose success/error states in testable DOM.

For forms:

- Inputs should have stable IDs or test IDs.
- Submit button should have stable test ID.
- Success and error alerts should have stable test IDs.
- Business validation messages should be asserted by visible text and test ID.

## Workstream 10: Blazor Interactivity and Hydration Contract

Blazor Web App can render DOM before C# event handlers and component state are fully interactive. This creates a common race condition: Playwright sees a button, clicks immediately, and the click is lost because hydration or SignalR interactivity is not ready.

Risk:

- DOM is visible.
- `data-testid` exists.
- Playwright clicks.
- Blazor event binding is not ready.
- Test becomes flaky or silently fails to submit.

Mandatory UI contract:

- Critical buttons must be disabled until the page/component is interactive.
- Critical buttons must remain disabled during loading/submission.
- Playwright must wait for visible and enabled state before clicking.
- Tests must not use arbitrary sleeps to avoid hydration delay.
- Components should expose ready state when appropriate.

Recommended component pattern:

```razor
<button
    data-testid="expense-submit"
    type="submit"
    disabled="@(!isInteractive || isSubmitting)">
    Lưu Chi Phí
</button>
```

Recommended Playwright pattern:

```ts
const submit = page.getByTestId('expense-submit');
await expect(submit).toBeVisible();
await expect(submit).toBeEnabled();
await submit.click();
```

Optional page-level readiness marker:

```razor
<div data-testid="app-ready" data-ready="@isInteractive.ToString().ToLowerInvariant()"></div>
```

Rules:

- `waitForTimeout` is not an acceptable readiness strategy.
- Visible DOM is not enough; interactive readiness must be testable.
- For Blazor Server, SignalR circuit readiness must be considered.
- For Blazor WASM/Auto, hydration and boot asset readiness must be considered.

## Workstream 11: Human QC Role After Automation

Playwright should reduce repetitive QC, but not remove human review entirely.

Human QC should focus on:

- Exploratory testing.
- UX judgment.
- Ambiguous business rules.
- New feature acceptance.
- Production incident review.
- Release risk signoff.

Automation should cover:

- Repetitive regression.
- Core workflow correctness.
- Known bug prevention.
- Cross-browser basics.
- Data integrity checks.

## Phased Rollout Plan

### Phase 1: Stabilize One Golden Business Flow

Focus on one Vạn An golden flow before expanding coverage:

```text
Household business login
-> Create a tax-aware cash register invoice
-> Verify generated accounting entries
-> Verify ledger/accounting history
```

Success bar:

- The flow runs 100 consecutive times without flakiness in the target test environment.
- No arbitrary sleeps.
- Seed/reset data is deterministic.
- UI and backend/accounting state are both verified.
- Failure artifacts include trace, screenshot, video, console logs, network errors, and server correlation.
- The framework patterns can be reused for later flows.

Also include the current expense form interactivity issue as a stabilization candidate, but do not expand suite breadth until one golden flow is stable.

### Phase 2: Build Core Accounting Coverage

- Expense entry.
- Revenue entry.
- Transaction history.
- Balance dashboard.
- Validation paths.

### Phase 3: Add Auth State Reuse and Data Strategy

- Global login setup.
- Test-only reset/seed API.
- Standard household-business seed snapshot.
- Unique runtime transaction data per run.
- Data cleanup or reset policy.

### Phase 4: CI Tiering

- Tier 0 on every commit.
- Tier 1 on PR/main.
- Tier 2/3 nightly.

### Phase 5: Reporting and Governance

- Add execution ledger.
- Add failure classification.
- Track flaky tests.
- Review test ROI monthly.

## Reverse Impact Analysis

### Low Risk

- Adding `data-testid` attributes.
- Improving Playwright diagnostics.
- Creating test taxonomy and docs.
- Using unique data names.
- Adding disabled/readiness state to critical buttons where behavior remains unchanged.

### Medium Risk

- Auth storage reuse.
- Test data cleanup.
- Adding test-only seed/reset endpoints.
- Updating existing selectors.
- Adding Blazor readiness markers.

### High Risk

- Running broad E2E suite on every commit.
- Rewriting many specs at once.
- Making app architecture changes only to satisfy tests.
- Over-automating unstable or unclear business behavior.
- Enabling test-support endpoints in production.
- Rewriting Tier 0/Tier 1 specs without approval.

## Success Metrics

Track these monthly:

- Percentage of critical workflows covered.
- Average time to diagnose failed test.
- Flaky test rate.
- Mean test runtime by tier.
- Number of regressions caught before release.
- Number of test failures caused by test data issues.
- Manual QC time saved.
- Golden flow consecutive pass count.
- Percentage of critical controls with readiness/testability contract.

## Definition of Done

This platform hardening is successful when:

- Critical workflows are covered by stable tests.
- Failures produce actionable artifacts.
- Tests run in predictable CI tiers.
- Flaky tests are tracked and actively reduced.
- Golden business flow runs repeatedly without flakiness.
- Test-only seed/reset strategy is available for critical accounting workflows.
- Blazor interactivity readiness is part of the UI testability contract.
- Human QC spends more time on exploratory/business review than repetitive regression.
