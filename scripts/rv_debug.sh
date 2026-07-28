#!/bin/bash
echo "=== KhachLink /missions HTML (first 2000 chars) ==="
curl -sk "https://diemthuong.khachvip.online/missions" | head -c 2000
echo
echo
echo "=== Sitemap: grep for missions ==="
curl -sk "https://khachvip.online/sitemap" | grep -i "mission" | head -5
echo "=== Sitemap: grep for redemption ==="
curl -sk "https://khachvip.online/sitemap" | grep -i "redemption" | head -5
echo "=== Sitemap: grep for admin ==="
curl -sk "https://khachvip.online/sitemap" | grep -i "admin" | head -5
echo "=== Sitemap HTML (first 1000 chars) ==="
curl -sk "https://khachvip.online/sitemap" | head -c 1000
echo
echo "=== KhachLink /missions: grep for blazor ==="
curl -sk "https://diemthuong.khachvip.online/missions" | grep -i "blazor" | head -3
