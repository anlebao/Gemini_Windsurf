import { test, expect } from '@playwright/test';

// TEMP diagnostic — đo horizontal overflow trong AI widget trên /a/{slug}.
const LINK_URL = 'https://shopai.timlathay.com/a/rvAntamAi01x';

test.use({ viewport: { width: 390, height: 844 } });

test('diagnose left-edge overflow', async ({ page }) => {
  await page.goto(LINK_URL, { waitUntil: 'domcontentloaded' });
  await expect(page.locator('.vanan-ai-widget')).toBeVisible({ timeout: 30000 });

  const input = page.locator('.vanan-ai-widget input[type="text"], .vanan-ai-widget input:not([type])').first();
  await input.fill('Cho tôi hỏi về các dịch vụ chăm sóc da và bảng giá chi tiết nhất có thể');
  await input.press('Enter');
  await expect(input).toBeEnabled({ timeout: 90000 });

  const report = await page.evaluate(() => {
    const out: any = { docOverflow: document.documentElement.scrollWidth - document.documentElement.clientWidth, widgets: [] };
    document.querySelectorAll('.vanan-ai-widget, .vanan-ai-panel, .vanan-ai-messages, .vanan-ai-msg, .vanan-ai-msg-body, .vanan-ai-bubble, .vanan-ai-meta, .vanan-ai-header, .assistant-header, .assistant-main').forEach((el: any) => {
      const r = el.getBoundingClientRect();
      out.widgets.push({
        cls: el.className.toString().slice(0, 60),
        left: Math.round(r.left), right: Math.round(r.right),
        scrollW: el.scrollWidth, clientW: el.clientWidth,
        clipped: el.scrollWidth > el.clientWidth + 2
      });
    });
    return out;
  });
  const styles = await page.evaluate(() => {
    const w: any = document.querySelector('.vanan-ai-widget');
    const p: any = document.querySelector('.vanan-ai-panel');
    const cw = getComputedStyle(w), cp = getComputedStyle(p);
    return {
      widget: { width: cw.width, left: cw.left, right: cw.right, display: cw.display, alignItems: cw.alignItems, position: cw.position },
      panel: { width: cp.width, display: cp.display, flexDir: cp.flexDirection, borderRadius: cp.borderRadius, boxShadow: cp.boxShadow.slice(0, 40), maxHeight: cp.maxHeight, overflow: cp.overflow, scopeAttr: p.getAttributeNames().filter((n: string) => n.startsWith('b-')) },
    };
  });
  const rules = await page.evaluate(() => {
    const out: any[] = [];
    const walk = (rules: CSSRuleList, media: string) => {
      for (const r of Array.from(rules)) {
        if (r instanceof CSSMediaRule) walk(r.cssRules, media + ' @media ' + r.conditionText);
        else if (r instanceof CSSStyleRule && r.selectorText.includes('vanan-ai-panel'))
          out.push({ sel: r.selectorText.slice(0, 80), media, width: (r.style as any).width, maxH: (r.style as any).maxHeight });
      }
    };
    for (const s of Array.from(document.styleSheets)) { try { walk(s.cssRules, ''); } catch {} }
    return out;
  });
  console.log('RULES', JSON.stringify(rules, null, 1));
  const probe = await page.evaluate(() => {
    const w: any = document.querySelector('.vanan-ai-widget');
    const d1 = document.createElement('div'); d1.style.cssText = 'width:100%;height:0'; w.appendChild(d1);
    const d2 = document.createElement('div'); d2.style.cssText = 'width:100vw;height:0'; w.appendChild(d2);
    const d3 = document.createElement('div'); d3.style.cssText = 'width:100%;height:0;position:absolute'; document.body.appendChild(d3);
    const r = { innerWidth: window.innerWidth, d1_100pctOfWidget: d1.getBoundingClientRect().width, d2_100vw: d2.getBoundingClientRect().width, d3_body100pct: d3.getBoundingClientRect().width, vwUnit: getComputedStyle(d2).width };
    d1.remove(); d2.remove(); d3.remove();
    return r;
  });
  console.log('PROBE', JSON.stringify(probe));
  console.log('STYLES', JSON.stringify(styles));
  console.log(JSON.stringify(report, null, 1));
});
