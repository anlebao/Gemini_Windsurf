import { Page, Locator, expect, test } from '@playwright/test';

/**
 * Strict Assertion Helpers for E2E Tests (Wave 8 — Regression Prevention)
 *
 * These helpers exist to prevent the 7 anti-patterns fixed in Stream B Waves 1-7
 * from re-emerging in the E2E suite. They are OPTIONAL — existing tests are not
 * required to migrate. New tests SHOULD prefer these over hand-rolled OR-tautology
 * or silent-skip patterns.
 *
 * Anti-patterns prevented:
 *   - Pattern A (OR-tautology): use `assertOneOf` instead of `expect(a||b||c).toBeTruthy()`
 *   - Pattern B (silent-skip):  use `assertVisibleOrSkip` instead of `if(isVisible){...}` no else
 */

/**
 * Assert that at least one of the given selectors is visible on the page.
 * Returns the first visible selector so the caller can act on it.
 * Fails hard if NONE are visible — no silent pass.
 *
 * Replaces Pattern A (OR-tautology):
 *   // BEFORE (tautology — passes if page renders anything)
 *   const hasX = await page.locator('.x').isVisible().catch(() => false);
 *   const hasY = await page.locator('.y').isVisible().catch(() => false);
 *   expect(hasX || hasY).toBeTruthy();
 *
 *   // AFTER (strict — fails if neither .x nor .y is visible)
 *   const visible = await assertOneOf(page, ['.x', '.y'], { message: 'Expected x or y' });
 *
 * Note: If exactly one of the states is a genuine "valid alternative" (e.g., success
 * OR error both acceptable), this helper is the right tool. If only ONE state is
 * correct, use a direct `await expect(page.locator('.x')).toBeVisible()` instead —
 * do not use this helper to disguise a weak assertion.
 */
export async function assertOneOf(
  page: Page,
  selectors: string[],
  opts: { timeout?: number; message?: string } = {}
): Promise<string> {
  const timeout = opts.timeout ?? 5000;
  const deadline = Date.now() + timeout;
  let lastError: unknown = null;

  while (Date.now() < deadline) {
    for (const selector of selectors) {
      try {
        const visible = await page.locator(selector).isVisible().catch(() => false);
        if (visible) return selector;
      } catch (err) {
        lastError = err;
      }
    }
    // Small delay between polling iterations (not Playwright waitForTimeout)
    await new Promise(resolve => setTimeout(resolve, 100));
  }

  throw new Error(
    opts.message ??
      `assertOneOf: none of selectors visible within ${timeout}ms: ${selectors.join(', ')}${
        lastError ? ` (last error: ${String(lastError)})` : ''
      }`
  );
}

/**
 * Assert a locator is visible, or explicitly `test.skip` with a reason.
 * Returns true if visible (caller can proceed), never returns false.
 *
 * Replaces Pattern B (silent-skip):
 *   // BEFORE (silent-skip — passes vacuously when element absent)
 *   const btn = page.locator('#btn');
 *   if (await btn.isVisible()) {
 *     await btn.click();
 *     await expect(page.locator('.result')).toBeVisible();
 *   }
 *
 *   // AFTER (explicit skip — test runner reports skip, not fake pass)
 *   await assertVisibleOrSkip(page, '#btn', 'Button not present on this page variant');
 *   await page.locator('#btn').click();
 *   await expect(page.locator('.result')).toBeVisible();
 *
 * Use this when a feature is genuinely conditional (e.g., browser doesn't support
 * an API, optional UI variant). Do NOT use it to dodge a missing feature — if the
 * element should always be there, use `await expect(locator).toBeVisible()` directly.
 */
export async function assertVisibleOrSkip(
  page: Page,
  selector: string,
  reason: string,
  opts: { timeout?: number } = {}
): Promise<boolean> {
  const timeout = opts.timeout ?? 5000;
  const visible = await page
    .locator(selector)
    .isVisible()
    .catch(() => false);

  if (!visible) {
    test.skip(true, reason);
  }

  return visible;
}

/**
 * Assert a locator is visible, or explicitly `test.skip` with a reason.
 * Locator overload — for cases where the caller already has a Locator object.
 */
export async function assertLocatorVisibleOrSkip(
  locator: Locator,
  reason: string,
  opts: { timeout?: number } = {}
): Promise<boolean> {
  const visible = await locator.isVisible().catch(() => false);

  if (!visible) {
    test.skip(true, reason);
  }

  return visible;
}

/**
 * Assert that the current page URL matches a regex.
 * Hard-fails if the URL does not match — no fallback to "any of these URLs".
 *
 * Replaces Pattern A (OR-tautology on URLs):
 *   // BEFORE (tautology — passes if URL contains anything in the OR list)
 *   const isOnTracking = page.url().includes('/order-tracking/');
 *   const hasTrackingLink = await page.locator('a[href*="/order-tracking/"]').isVisible();
 *   expect(isOnTracking || hasTrackingLink).toBeTruthy();
 *
 *   // AFTER (strict — asserts the canonical expected URL)
 *   await assertUrlMatches(page, /\/order-tracking\//, 'Expected redirect to /order-tracking/{id}');
 */
export async function assertUrlMatches(
  page: Page,
  pattern: RegExp,
  message?: string
): Promise<void> {
  await expect(page).toHaveURL(pattern);
  // toHaveURL already throws on mismatch; message param is for caller clarity in stack traces
  void message;
}
