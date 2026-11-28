#!/bin/bash
sudo docker cp vanan-crawler-1:/tmp/res.html /tmp/res.html
python3 - <<'PY'
import re, html as h
raw = open('/tmp/res.html', encoding='utf-8', errors='replace').read()
print('len:', len(raw))
txt = re.sub(r'<script.*?</script>', '', raw, flags=re.S)
txt = re.sub(r'<[^>]+>', ' ', txt)
txt = h.unescape(re.sub(r'\s+', ' ', txt)).strip()
print('CAPTCHA_ERROR_PRESENT:', ('xác nhận' in txt.lower()))
i = txt.find('Mã số thuế')
print('SNIPPET:', txt[max(0,i-50):i+420] if i>=0 else txt[:400])
rows = re.findall(r'<tr[^>]*>(.*?)</tr>', raw, flags=re.S)
for r in rows[:14]:
    cells = [re.sub(r'<[^>]+>','',c).strip() for c in re.findall(r'<t[dh][^>]*>(.*?)</t[dh]>', r, flags=re.S)]
    cells = [h.unescape(c) for c in cells if c.strip()]
    if cells: print('ROW:', ' | '.join(cells)[:160])
PY
