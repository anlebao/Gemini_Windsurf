#!/usr/bin/env python3
import gzip
import sys

WASM_PATH = "/usr/share/nginx/html/_framework/VanAn.KhachLink.wasm.gz"

with gzip.open(WASM_PATH, "rb") as f:
    data = f.read()

def count_utf16le(needle_str):
    needle = needle_str.encode("utf-16-le")
    count = 0
    pos = 0
    while True:
        pos = data.find(needle, pos)
        if pos == -1:
            break
        count += 1
        pos += 1
    return count

def count_utf8(needle_str):
    needle = needle_str.encode("utf-8")
    count = 0
    pos = 0
    while True:
        pos = data.find(needle, pos)
        if pos == -1:
            break
        count += 1
        pos += 1
    return count

print("=== Bug 1: Vietnamese font (UTF-16LE search, should be > 0) ===")
print(f"  Giỏ hàng của bạn: {count_utf16le('Giỏ hàng của bạn')}")
print(f"  Thanh toán: {count_utf16le('Thanh toán')}")
print(f"  Tổng cộng: {count_utf16le('Tổng cộng')}")
print(f"  Sản phẩm: {count_utf16le('Sản phẩm')}")
print(f"  🛒 (emoji): {count_utf16le('🛒')}")
print(f"  ✕ (x mark): {count_utf16le('✕')}")
print()
print("=== Mojibake (UTF-16LE search, should be 0) ===")
print(f"  'Giá» hÃ' (mojibake): {count_utf16le('Giá» hÃ')}")
print(f"  'Thanh toÃ¡n' (mojibake): {count_utf16le('Thanh toÃ¡n')}")
print(f"  'Tá»ng' (mojibake): {count_utf16le('Tá»ng')}")
print(f"  'Sáº£n' (mojibake): {count_utf16le('Sáº£n')}")
print()
print("=== Bug 2: GetShortId (ASCII, should be > 0) ===")
print(f"  GetShortId: {count_utf8('GetShortId')}")
print(f"  CreatedOrderRef: {count_utf8('CreatedOrderRef')}")
