#!/bin/bash
UA='Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/125.0'
sleep 30
sudo docker exec vanan-crawler-1 sh -c "curl -sk -b /tmp/cj -c /tmp/cj -A '$UA' -H 'Referer: https://tracuunnt.gdt.gov.vn/tcnnt/mstdn.jsp' -H 'Content-Type: application/x-www-form-urlencoded' --data 'cm=cm&mst=0100107518&fullname=&address=&cmt=&captcha=x2bh' 'https://tracuunnt.gdt.gov.vn/tcnnt/mstdn.jsp' -o /tmp/res.html -w 'post:%{http_code}'"
echo ""
sleep 2
sudo docker exec vanan-crawler-1 python3 -c "
import re, html as h
raw = open('/tmp/res.html', encoding='utf-8', errors='replace').read()
txt = re.sub(r'<script.*?</script>', '', raw, flags=re.S)
txt = re.sub(r'<[^>]+>', ' ', txt)
txt = h.unescape(re.sub(r'\s+', ' ', txt)).strip()
print('HAS_CAPTCHA_ERROR:', 'xac nhan' in txt.lower().replace('nha&#7853;p',''))
i = txt.find('Mã số thuế')
print('SNIPPET:', txt[max(0,i-50):i+400] if i>=0 else txt[:300])
rows = re.findall(r'<tr[^>]*>(.*?)</tr>', raw, flags=re.S)
for r in rows[:12]:
    cells = [re.sub(r'<[^>]+>','',c).strip() for c in re.findall(r'<t[dh][^>]*>(.*?)</t[dh]>', r, flags=re.S)]
    cells = [h.unescape(c) for c in cells if c.strip()]
    if cells: print('ROW:', ' | '.join(cells)[:150])
"
