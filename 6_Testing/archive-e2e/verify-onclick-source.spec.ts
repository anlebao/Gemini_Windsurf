import { test } from '@playwright/test';

const BASE_URL = 'https://app.khachvip.online';

test('verify-onclick-source — check which elements have literal @onclick', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true });
  await context.request.post(`${BASE_URL}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
    headers: { 'Content-Type': 'application/json' },
  });
  await context.request.post(`${BASE_URL}/api/admin/impersonate/00000000-0000-0000-0000-000000000001`);

  const page = await context.newPage();
  await page.goto(`${BASE_URL}/admin/tenants`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(8000);

  // Check ALL elements with @onclick attribute
  const onclickElements = await page.evaluate(() => {
    // Find all elements with @onclick attribute (literal)
    const all = document.querySelectorAll('*');
    const results: any[] = [];
    all.forEach(el => {
      const attrs = Array.from(el.attributes);
      const onclickAttr = attrs.find(a => a.name === '@onclick');
      const blazorOnclickAttr = attrs.find(a => a.name.startsWith('blazor:'));
      if (onclickAttr || blazorOnclickAttr) {
        results.push({
          tag: el.tagName,
          class: el.className?.substring(0, 60),
          hasLiteralOnclick: !!onclickAttr,
          literalValue: onclickAttr?.value,
          hasBlazorOnclick: !!blazorOnclickAttr,
          blazorAttrs: attrs.filter(a => a.name.startsWith('blazor:')).map(a => a.name),
          hasBAttr: el.hasAttribute('b-') || Array.from(attrs).some(a => a.name.startsWith('b-')),
        });
      }
    });
    return results;
  });

  console.log('=== ELEMENTS WITH @onclick ===');
  onclickElements.forEach((e, i) => {
    console.log(`[${i}] tag=${e.tag} class="${e.class}" literal=@onclick(${e.literalValue}) blazor=${JSON.stringify(e.blazorAttrs)} b-attr=${e.hasBAttr}`);
  });

  // Check if ANY element has blazor: attributes (circuit hydration indicator)
  const blazorAttrCount = await page.evaluate(() => {
    return document.querySelectorAll('[blazor\\:onclick], [b-]').length;
  });
  console.log('Total elements with blazor: or b- attrs:', blazorAttrCount);

  // Check if the page has any non-VanAButton buttons with @onclick
  const nonVananButtons = onclickElements.filter(e => !e.class?.includes('vanan-button'));
  console.log('Non-VanAButton elements with @onclick:', nonVananButtons.length);

  await context.close();
});
