#!/bin/sh
WASM=/usr/share/nginx/html/_framework/VanAn.KhachLink.wasm.gz

# .NET WASM stores strings as UTF-16LE. Convert search strings to UTF-16LE and grep binary.
echo "=== Bug 1: Vietnamese font (UTF-16LE search, should be > 0) ==="
echo -n "Giỏ hàng của bạn: "; cat $WASM | gunzip | grep -a -c "$(printf 'Gi\xe1\xbb\x8f h\xc3\xa0ng c\xe1\xbb\xa7a b\xe1\xba\xa1n' | iconv -f UTF-8 -t UTF-16LE)"
echo -n "Thanh toán: "; cat $WASM | gunzip | grep -a -c "$(printf 'Thanh t\xc3\xb3an' | iconv -f UTF-8 -t UTF-16LE)"
echo -n "Tổng cộng: "; cat $WASM | gunzip | grep -a -c "$(printf 'T\xe1\xbb\x95ng c\xe1\xbb\x99ng' | iconv -f UTF-8 -t UTF-16LE)"
echo -n "Sản phẩm: "; cat $WASM | gunzip | grep -a -c "$(printf 'S\xe1\xba\xa3n ph\xe1\xba\xa9m' | iconv -f UTF-8 -t UTF-16LE)"
echo ""
echo "=== Mojibake (UTF-16LE search, should be 0) ==="
# Mojibake chars are mostly ASCII-range, stored as UTF-16LE with 00 high byte
echo -n "Thanh toÃ¡n (mojibake): "; cat $WASM | gunzip | grep -a -c "$(printf 'Thanh to\xc3\x83\xc2\xa1n' | iconv -f UTF-8 -t UTF-16LE)"
echo ""
echo "=== Bug 2: GetShortId (ASCII, should be > 0) ==="
echo -n "GetShortId: "; cat $WASM | gunzip | grep -a -c "GetShortId"
