#!/bin/bash
UA='Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/125.0'
sudo docker exec vanan-crawler-1 sh -c "curl -sk -c /tmp/cj -A '$UA' -H 'Accept-Language: vi-VN,vi;q=0.9,en;q=0.8' 'https://tracuunnt.gdt.gov.vn/tcnnt/mstdn.jsp' -o /dev/null -w 'page:%{http_code}'"
echo ""
sleep 20
sudo docker exec vanan-crawler-1 sh -c "curl -sk -b /tmp/cj -A '$UA' 'https://tracuunnt.gdt.gov.vn/tcnnt/captcha.png?uid=424242' -o /tmp/cap.png -w 'captcha:%{http_code}'"
echo ""
sleep 10
echo -n "OCR_TEXT="
sudo docker exec vanan-crawler-1 sh -c "tesseract /tmp/cap.png stdout --psm 7 -l eng 2>/dev/null" | tr -cd '[:alnum:]'
echo ""
