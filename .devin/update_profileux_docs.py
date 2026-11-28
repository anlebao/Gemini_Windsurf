# -*- coding: utf-8 -*-
"""Update task_profile_ux_upgrade.md (checklist -> DONE) + project_state.md.
Run: python .devin/update_profileux_docs.py <commit_sha>
UTF-8 safe — KHÔNG dùng PowerShell cho file tiếng Việt."""
import re, sys, io

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
SHA = sys.argv[1] if len(sys.argv) > 1 else "TBD"
ROOT = r"C:\VibeCoding\Gemini_Windsurf"

# ── 1. Task card: header status + tick checkboxes ──────────────────────────
card_path = ROOT + r"\docs\AI\tasks\task_profile_ux_upgrade.md"
card = open(card_path, encoding="utf-8").read()

card = card.replace(
    "> **DRAFT — chờ user approve trước khi IMPLEMENT.**",
    f"> **DONE — `{SHA}`** (approved + implemented 2026-11-28 · guard ALL PASSED)",
)
card = card.replace("- [ ]", "- [x]")

open(card_path, "w", encoding="utf-8").write(card)
print(f"task card: {card.count('- [x]')} checkboxes ticked")

# ── 2. project_state.md: Section 3 + Last Updated ──────────────────────────
state_path = ROOT + r"\docs\AI\project_state.md"
st = open(state_path, encoding="utf-8").read()

marker3 = "## 3. Current Status"
idx3 = st.index(marker3)
add3 = (
    f"\n- ✅ **Profile UX Upgrade** (`{SHA}`): Zone 1 → Loyalty Hero (điểm lớn + "
    "progress bar tới tier kế qua `/api/loyalty/my` NextTierThreshold/ProgressPercent "
    "fail-open · avatar viền màu tier · CTAs Đổi điểm/Lịch sử) · Zone 4 cleanup "
    "(xóa fraud flags UI+fetch — security leak · ẩn device_token raw · xóa duplicate "
    "upgrade CTA — identity-upgrade context card là single-source) · retention: "
    "referral card từ SalesmanCode + clipboard · guest BackToStoreChip · quick-access "
    "nhóm Mua sắm/Tài khoản · skeleton shimmer · E2E `profile-ux.spec.ts` (Gate 4)\n"
)
st = st[: idx3 + len(marker3)] + add3 + st[idx3 + len(marker3):]

st = re.sub(
    r"> \*\*Last Updated:\*\* [^\n]+",
    f"> **Last Updated:** 2026-11-28 · **Branch:** `main` @ `{SHA}` — "
    "Profile UX upgrade (Loyalty Hero + Zone 4 cleanup + retention) DEPLOYED",
    st,
    count=1,
)

open(state_path, "w", encoding="utf-8").write(st)
print("project_state.md updated")
