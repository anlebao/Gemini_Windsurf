import { chromium, FullConfig } from '@playwright/test';
import { loadEnvConfig } from './utils/env-config';

const config = loadEnvConfig();

async function globalSetup(_config: FullConfig) {
  const browser = await chromium.launch();
  const page = await browser.newPage();

  await page.goto(`${config.SHOPERP_URL}/login`);
  await page.fill('#username', 'admin@vanan.vn');
  await page.fill('#password', 'VanAn@2026');
  await page.click('button[type="submit"]');
  await page.waitForURL(`${config.SHOPERP_URL}/`);

  await page.context().storageState({ path: 'auth/admin.json' });
  await browser.close();
}

export default globalSetup;
