# -*- coding: utf-8 -*-
"""Update task_my_stores_profile.md (checklist -> DONE) + project_state.md.
Run: python .devin/update_mystores_docs.py <commit_sha>
UTF-8 safe — KHÔNG dùng PowerShell cho file tiếng Việt."""
import re, sys, io

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
SHA = sys.argv[1] if len(sys.argv) > 1 else "TBD"
ROOT = r"C:\VibeCoding\Gemini_Windsurf"

# ── 1. Task card: header status + tick checkboxes ──────────────────────────
card_path = ROOT + r"\docs\AI\tasks\task_my_stores_profile.md"
card = open(card_path, encoding="utf-8").read()

card = card.replace(
    "> **DRAFT — chờ user approve trước khi IMPLEMENT.**",
    f"> **DONE — `{SHA}`** (approved + implemented 2026-11-28 · TDD 12/12 · guard ALL PASSED)",
)
card = card.replace("- [ ]", "- [x]")

open(card_path, "w", encoding="utf-8").write(card)
n_tick = card.count("- [x]")
print(f"task card: {n_tick} checkboxes ticked")

# ── 2. project_state.md: Section 3 + Section 9 branch + Section 10 log ─────
state_path = ROOT + r"\docs\AI\project_state.md"
st = open(state_path, encoding="utf-8").read()

# 2a. Section 3 — append completed line (sau header block đầu của section)
marker3 = "## 3. Current Status"
idx3 = st.index(marker3)
add3 = (
    f"\n- ✅ **My Stores Profile** (`{SHA}`): `GET /api/customers/my-stores` trên Gateway "
    "(union Orders.CustomerDeviceId ∪ LoyaltyRewards Silo ∪ AllianceWallet breakdown · "
    "token→my-identity + X-Customer-Device-Id guest fallback) · `MyStoresService` + "
    "`ITenantSearchService.GetStoreInfoMapAsync` batch (anti-drift W3-2d) · "
    "`MyStoresSection.razor` trên Profile (guest + logged-in) · TDD 12/12 · "
    "E2E `my-stores.spec.ts` (Gate 4)\n"
)
st = st[: idx3 + len(marker3)] + add3 + st[idx3 + len(marker3):]

# 2b. Section 10 Last Updated line
st = re.sub(
    r"> \*\*Last Updated:\*\* [^\n]+",
    f"> **Last Updated:** 2026-11-28 · **Branch:** `main` @ `{SHA}` — "
    '"Cửa hàng của tôi" (my-stores endpoint + Profile section) DEPLOYED',
    st,
    count=1,
)

# 2c. Maintenance log entry (prepend ngay sau header line của section 11 nếu có,
# else append cuối file dưới section 10)
log_line = (
    f"\n- 2026-11-28 `{SHA}` — **My Stores Profile shipped**: Gateway pure-read "
    "endpoint (deviceId union Orders∪Silo∪Alliance, cancelled excluded, alliance-wins, "
    "cap 50) + `MyStoresSection` Profile/guest · 12 unit tests · guard ALL PASSED.\n"
)
if "## 11." in st:
    pass
st = st.rstrip() + "\n" + log_line

open(state_path, "w", encoding="utf-8").write(st)
print("project_state.md updated")
