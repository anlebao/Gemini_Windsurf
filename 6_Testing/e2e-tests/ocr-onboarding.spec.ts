import { test, expect } from '@playwright/test';
import { loadEnvConfig, isTierEnabled } from '../utils/env-config';
import { TestReporter } from '../utils/test-reporter';

// OCR Onboarding S5 (Gate 4, 2026-10-25): Wizard /admin/tenants tab "📷 Onboarding OCR".
// User quyết định 2026-10-25: tách 2 case — A (có GPKD) / B (shop không GPKD).
//
// Strategy:
// - Test 1 (render — Gate 4 bắt buộc): tab + step 0 hiện 2 case card.
// - Test 2 (Case B validation): điền form shop → bước 2 hiện biển hiệu + menu (KHÔNG gọi server).
// - Test 3 (Case A validation): MST không hợp lệ → alert chặn (KHÔNG gọi server).
// - Flow tạo tenant thật (submit → Gateway Pending→Verify → NATS sync → product import)
//   đã cover bởi Core.Tests (TenantOcrOnboardingServiceTests 9 test) + ShopERP.Tests
//   (ProductImportControllerTests 5 test); runtime validate bằng ảnh thật ở RV L3/L4
//   (OCR accuracy là runtime risk — không thể verify bằng code).

const config = loadEnvConfig();
const reporter = new TestReporter('OCR Onboarding Wizard E2E');

const shopErpBase = process.env.OCR_SHOPERP_URL || config.SHOPERP_URL;

// Mock navigator.geolocation (wizard dùng trực tiếp, không qua vananPWA)
const gpsMockBody = `
  Object.defineProperty(navigator, 'geolocation', {
    value: {
      getCurrentPosition: (success) => success({
        coords: { latitude: 10.966, longitude: 106.594, accuracy: 15 }
      }),
      watchPosition: () => 0,
      clearWatch: () => {}
    },
    configurable: true
  });
`;

test.describe.configure({ mode: isTierEnabled('e2e') ? 'parallel' : 'skip' });

test.describe('VanAn Ecosystem - OCR Tenant Onboarding Wizard (S4/S5)', () => {
  test.beforeAll(async () => {
    if (!isTierEnabled('e2e')) {
      reporter.setArchitectDecision('Bypassed by Architect - E2E tests disabled');
      test.skip();
    }
    reporter.log('Starting OCR Onboarding Wizard E2E...');
  });

  test.beforeEach(async ({ page, context }) => {
    await context.addInitScript(gpsMockBody);
    // Dev login endpoint CHỈ tồn tại trong DEBUG build; production RV dùng storageState.
    try {
      const response = await page.request.post(`${shopErpBase}/dev/login`, { timeout: 8000 });
      if (response.ok()) {
        const body = await response.json();
        console.log(`Dev login successful: tenantId=${body.tenantId}, role=${body.role}`);
      } else {
        console.log(`Dev login unavailable (${response.status()}) — dùng storageState (production RV)`);
      }
    } catch {
      console.log('Dev login unavailable — dùng storageState (production RV)');
    }
    await page.goto(`${shopErpBase}/admin/tenants`);
    await page.waitForLoadState('networkidle');
  });

  // ─── RENDER TEST (Gate 4) ───────────────────────────────────────────────

  test('Tab Onboarding OCR hiển thị + step 0 render 2 case', async ({ page }) => {
    const ocrTab = page.locator('button.tenant-tabs button', { hasText: 'Onboarding OCR' });
    await expect(ocrTab).toBeVisible({ timeout: 15000 });

    await ocrTab.click();
    await page.waitForLoadState('networkidle');

    // Step 0 — 2 case card (D4)
    await expect(page.locator('.ocr-wizard')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.ocr-case-card', { hasText: 'Công ty / Hộ kinh doanh' })).toBeVisible();
    await expect(page.locator('.ocr-case-card', { hasText: 'Shop nhỏ' })).toBeVisible();
    // Stepper 5 bước
    await expect(page.locator('.ocr-wizard-steps')).toContainText('1. Loại hình');
    await expect(page.locator('.ocr-wizard-steps')).toContainText('5. Kết quả');
  });

  // ─── CASE B (shop không GPKD) — validation path (không gọi server) ─────

  test('Case B: chọn shop nhỏ → điền form → bước 2 biển hiệu + menu', async ({ page }) => {
    await page.locator('button.tenant-tabs button', { hasText: 'Onboarding OCR' }).click();
    await page.locator('.ocr-case-card', { hasText: 'Shop nhỏ' }).click();
    await page.getByRole('button', { name: 'Tiếp theo' }).click();

    // Bước 1' — thông tin shop: tên + địa chỉ + HKDGroup bắt buộc
    await expect(page.locator('.ocr-section h4', { hasText: 'Thông tin shop' })).toBeVisible({ timeout: 10000 });
    await page.locator('input[placeholder="VD: Cà Phê ABC"]').fill('E2E Shop Không GPKD');
    await page.locator('input[placeholder="Số nhà, đường, phường/xã, quận/huyện, tỉnh/thành"]').fill('12 Nguyễn Huệ, Q1, HCM');
    await page.locator('select', { hasText: 'Nhóm thuế HKD' }).or(page.locator('.ocr-form select')).selectOption('2');

    // Thiếu HKDGroup → chặn
    // (đã chọn rồi — bấm tiếp phải qua được)
    await page.getByRole('button', { name: 'Tiếp theo' }).click();

    // Bước 2 — biển hiệu + menu (file input + tên hiệu)
    await expect(page.locator('.ocr-section h4', { hasText: 'Biển hiệu cửa hàng' })).toBeVisible({ timeout: 10000 });
    await expect(page.locator('.ocr-section h4', { hasText: 'Menu / Danh mục sản phẩm' })).toBeVisible();
  });

  // ─── CASE A (có GPKD) — MST validation (không gọi server) ───────────────

  test('Case A: MST không hợp lệ → alert chặn trước khi tiếp tục', async ({ page }) => {
    await page.locator('button.tenant-tabs button', { hasText: 'Onboarding OCR' }).click();
    await page.locator('.ocr-case-card', { hasText: 'Công ty / Hộ kinh doanh' }).click();
    await page.getByRole('button', { name: 'Tiếp theo' }).click();

    // Bước 1 — GPKD: nhập MST sai (không phải 10/13 số)
    await expect(page.locator('.ocr-section h4', { hasText: 'Ảnh giấy phép kinh doanh' })).toBeVisible({ timeout: 10000 });
    await page.locator('input[placeholder="10 hoặc 13 chữ số"]').fill('ABC123');
    await page.getByRole('button', { name: 'Tiếp theo' }).click();

    // Alert warning chặn (không vào bước 2)
    await expect(page.locator('.vanan-alert', { hasText: 'Mã số thuế bắt buộc' })).toBeVisible({ timeout: 10000 });
    await expect(page.locator('.ocr-section h4', { hasText: 'Biển hiệu cửa hàng' })).not.toBeVisible();
  });
});