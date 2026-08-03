#!/usr/bin/env python3
data = open("/tmp/vk.wasm", "rb").read()

def find(pattern, encoding="utf-16-le"):
    return data.count(pattern.encode(encoding))

# Strings that should be present after fix
print("=== Fix verification (UTF-16LE) ===")
checks = {
    "oninput": "Binding fix (@bind:event=oninput) — should be 1+",
    "store-search-input": "Search box input ID — should be 1+",
    "store-finder-search": "Search box CSS class — should be 1+",
    "store-finder-card-hover": "Store card CSS — should be 1+",
}
for s, desc in checks.items():
    c = find(s)
    status = "PASS" if c >= 1 else "FAIL"
    print(f"  [{status}] {s}: {c} — {desc}")

# NavMenu dedup: check strings that were in mobile bottom-nav
# "Giỏ hàng" appears in: desktop sidebar (1) + header aria-label (1) = 2 after fix (was 3 before)
print("\n=== NavMenu dedup verification ===")
dedup_checks = {
    "Gio hang": "Giỏ hàng — desktop sidebar + header (should be ~2, was ~3 before fix)",
    "Diem thuong": "Điểm thưởng — desktop sidebar + header (should be ~2-3, was ~3-4 before fix)",
    "Nhiem vu": "Nhiệm vụ — desktop sidebar + header (should be ~2, was ~3 before fix)",
    "Doi diem": "Đổi điểm — desktop sidebar + header (should be ~1-2, was ~2-3 before fix)",
    "Don hang": "Đơn hàng — KEPT in mobile bottom-nav + desktop sidebar (should be 2+)",
    "Lien minh": "Liên minh — KEPT in mobile bottom-nav + desktop sidebar (should be 2+)",
    "Cua hang": "Cửa hàng — KEPT in mobile bottom-nav + desktop sidebar (should be 2+)",
}
for s, desc in dedup_checks.items():
    c = find(s)
    print(f"  {s}: {c} — {desc}")

# Vietnamese with diacritics
print("\n=== Vietnamese diacritics (UTF-16LE) ===")
vi_checks = {
    "Giỏ hàng": "Cart — desktop + header",
    "Điểm thưởng": "Loyalty — desktop + header",
    "Nhiệm vụ": "Missions — desktop + header",
    "Đổi điểm": "Redeem — desktop + header",
    "Đơn hàng": "Orders — KEPT in mobile",
    "Liên minh": "Alliance — KEPT in mobile",
}
for s, desc in vi_checks.items():
    c = find(s)
    print(f"  {s}: {c} — {desc}")
