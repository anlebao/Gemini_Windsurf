import { test } from '@playwright/test';

const BASE_URL = 'https://app.khachvip.online';

test('diag-raw — raw HTML response before JS', async ({ browser }) => {
  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true });
  await context.request.post(`${BASE_URL}/api/platform/login`, {
    data: { Username: 'sysadmin@vanan.vn', Password: '2026@vanan' },
    headers: { 'Content-Type': 'application/json' },
  });
  await context.request.post(`${BASE_URL}/api/admin/impersonate/00000000-0000-0000-0000-000000000001`);

  const page = await context.newPage();

  // Capture the raw response before JS processes it
  const responsePromise = page.waitForResponse(resp => resp.url().includes('/admin/tenants'));

  await page.goto(`${BASE_URL}/admin/tenants`);
  const response = await responsePromise;
  const rawHtml = await response.text();

  // Check for Blazor markers in RAW HTML
  const hasBlazorMarker = rawHtml.includes('<!--Blazor:');
  const markerCount = (rawHtml.match(/<!--Blazor:/g) || []).length;
  const hasServerType = rawHtml.includes('"type":"server"');
  const hasPrerenderId = rawHtml.includes('prerenderId');
  const literalOnclickCount = (rawHtml.match(/@onclick/g) || []).length;
  const blazorOnclickCount = (rawHtml.match(/blazor:onclick/g) || []).length;

  // Check for script references
  const hasBlazorScript = rawHtml.includes('blazor.web.js');
  const hasBlazorServerScript = rawHtml.includes('blazor.server.js');

  // Check for error indicators
  const hasErrorUi = rawHtml.includes('blazor-error-ui');
  const hasReconnectModal = rawHtml.includes('components-reconnect-modal');

  // Find the first Blazor marker if it exists
  const markerMatch = rawHtml.match(/<!--Blazor:([\s\S]*?)-->/);
  const firstMarker = markerMatch ? markerMatch[0].substring(0, 300) : 'NONE';

  console.log('=== RAW HTML ANALYSIS ===');
  console.log('HTML length:', rawHtml.length);
  console.log('Has Blazor marker:', hasBlazorMarker);
  console.log('Marker count:', markerCount);
  console.log('Has server type:', hasServerType);
  console.log('Has prerenderId:', hasPrerenderId);
  console.log('Literal @onclick count:', literalOnclickCount);
  console.log('blazor:onclick count:', blazorOnclickCount);
  console.log('Has blazor.web.js:', hasBlazorScript);
  console.log('Has blazor.server.js:', hasBlazorServerScript);
  console.log('Has error UI:', hasErrorUi);
  console.log('Has reconnect modal:', hasReconnectModal);
  console.log('First marker:', firstMarker);

  // Check what scripts are referenced
  const scriptMatches = rawHtml.match(/<script[^>]*src="([^"]*)"[^>]*>/g) || [];
  console.log('Scripts:', JSON.stringify(scriptMatches));

  await context.close();
});
