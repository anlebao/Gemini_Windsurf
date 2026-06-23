import { test, expect } from '@playwright/test';

test.describe('Export Excel Flow', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('#username', 'admin@vanan.vn');
    await page.fill('#password', 'VanAn@2026');
    await page.click('button[type="submit"]');
    await page.waitForURL('/');
  });

  test('should export revenue report to Excel', async ({ page }) => {
    const from = '2026-01-01';
    const to = '2026-12-31';
    const response = await page.request.get(`/api/reports/export/excel?type=revenue&from=${from}&to=${to}`);

    expect(response.status()).toBe(200);
    expect(response.headers()['content-type']).toBe('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
    expect(response.headers()['content-disposition']).toContain('revenue-report');

    const body = await response.body();
    expect(body.length).toBeGreaterThan(0);
    expect(body.toString('hex', 0, 4)).toBe('504b0304'); // OOXML ZIP magic number
  });

  test('should export inventory report to Excel', async ({ page }) => {
    const response = await page.request.get('/api/reports/export/excel?type=inventory');

    expect(response.status()).toBe(200);
    expect(response.headers()['content-type']).toBe('application/vnd.openxmlformats-officedocument.spreadsheetml.sheet');
    expect(response.headers()['content-disposition']).toContain('inventory-report');

    const body = await response.body();
    expect(body.length).toBeGreaterThan(0);
  });

  test('should reject export for unauthorized role', async ({ page }) => {
    // Log out owner and log in as staff
    await page.goto('/login');
    await page.fill('#username', 'staff@vanan.vn');
    await page.fill('#password', 'VanAn@2026');
    await page.click('button[type="submit"]');
    await page.waitForURL('/');

    const response = await page.request.get('/api/reports/export/excel?type=revenue');
    expect(response.status()).toBe(403);
  });
});
