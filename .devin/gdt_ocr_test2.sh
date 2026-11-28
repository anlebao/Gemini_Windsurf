#!/bin/bash
set -x
UA='Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/125.0'
# 1. fresh captcha with session
sudo docker exec vanan-crawler-1 sh -c "curl -sk -c /tmp/cj -A '$UA' 'https://tracuunnt.gdt.gov.vn/tcnnt/mstdn.jsp' -o /dev/null && curl -sk -b /tmp/cj -A '$UA' 'https://tracuunnt.gdt.gov.vn/tcnnt/captcha.png?uid=777' -o /tmp/cap2.png && ls -la /tmp/cap2.png"
# 2. install imagemagick for preprocessing (test only)
sudo docker exec vanan-crawler-1 sh -c 'apt-get update -qq >/dev/null 2>&1; apt-get install -y -qq imagemagick >/dev/null 2>&1; which convert'
# 3. preprocess + OCR variants
sudo docker exec vanan-crawler-1 sh -c 'convert /tmp/cap2.png -resize 400% -colorspace Gray -negate -threshold 55% /tmp/cap2_proc.png; ls -la /tmp/cap2_proc.png'
sudo docker exec vanan-crawler-1 sh -c 'echo RAW7:; tesseract /tmp/cap2.png stdout --psm 7 -l eng 2>/dev/null; echo RAW8:; tesseract /tmp/cap2.png stdout --psm 8 -l eng 2>/dev/null; echo PROC7:; tesseract /tmp/cap2_proc.png stdout --psm 7 -l eng 2>/dev/null; echo PROC8:; tesseract /tmp/cap2_proc.png stdout --psm 8 -l eng 2>/dev/null; echo PROC13:; tesseract /tmp/cap2_proc.png stdout --psm 13 -l eng 2>/dev/null; echo DONE'
