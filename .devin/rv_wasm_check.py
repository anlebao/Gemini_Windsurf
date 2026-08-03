#!/usr/bin/env python3
import sys

data = open("/tmp/vk.wasm", "rb").read()

def find_utf16(pattern):
    encoded = pattern.encode("utf-16-le")
    return data.count(encoded)

def find_ascii(pattern):
    return data.count(pattern.encode("ascii"))

print("=== UTF-16LE search (how .NET stores strings in WASM) ===")
patterns_utf16 = [
    "store-search-input", "oninput", "bi-cart3", "mobile-bottom-nav",
    "store-finder-search", "HandleSearchKeypress", "SearchStoresAsync",
    "ClearStoreSearch", "LoadAllNearbyStores", "ShareMyLocationAsync",
    "IncreaseRadiusAndSearch", "store-finder-card-hover",
    "Khong tim thay cua hang", "Vao cua hang"
]
for p in patterns_utf16:
    c = find_utf16(p)
    print(f"  {p}: {c} matches")

print("=== ASCII search ===")
for p in ["store-finder", "store-search-input", "oninput", "bi-cart3", "mobile-bottom-nav"]:
    c = find_ascii(p)
    print(f"  {p}: {c} matches")
