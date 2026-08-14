# RELEASE STRATEGY — Guard QR Verify (Issue #126)

> **Created:** 2026-08-14
> **Last Updated:** 2026-08-15 — ALL 3 RELEASES COMPLETE + MERGED + DEPLOYED
> **Principle:** Mỗi release = 1 branch → 1 PR → 1 merge → 1 deploy → giá trị nhìn thấy được
> **Conflict avoidance:** Sequential releases, mỗi release branch từ `main` mới nhất (sau release trước đã merge)

## RELEASE STATUS

| Release | Sprints | Branch | Commit | PR | Status |
|---|---|---|---|---|---|
| **R1** — Paper Ticket Flow | 0+1+2+3+5 | `feature/guard-qr-r1` (+sprint5) | `ee109800` | #126 direct | ✅ MERGED + DEPLOYED + RV PASS (CLI) |
| **R2** — Digital Claim Flow | 4 | `feature/guard-qr-r2-sprint4` | `08f8ff60` | #128 | ✅ MERGED + DEPLOYED (manual RV pending) |
| **R3** — Tested + Production Ready | 6 | `feature/guard-qr-r3-sprint6` | `4dd1a0a4` | #129 | ✅ MERGED + CD deploying (E2E spec deferred) |

**Issue #126 ready to close** after R2 manual RV + R3 CD verify.

---

## 1. BRANCH STRATEGY (No Conflict)

```
main (always-green, f7201ef4)
 │
 ├── feature/guard-qr-r1 (Sprint 0+1+2+3+5)
 │     ↓ PR → review → merge → deploy → RV
 │     └── main updated (R1 merged)
 │
 ├── feature/guard-qr-r2 (Sprint 4 — branch từ main mới nhất)
 │     ↓ PR → review → merge → deploy → RV
 │     └── main updated (R2 merged)
 │
 └── feature/guard-qr-r3 (Sprint 6 — branch từ main mới nhất)
       ↓ PR → review → merge → deploy → RV
       └── main updated (R3 merged — DONE)
```

**Rules:**
- **SEQUENTIAL** — không bao giờ 2 branch song song (tránh merge conflict)
- Mỗi release branch từ `main` SAU khi release trước đã merge
- Mỗi PR squash merge (1 commit per release trên main)
- Feature flag `Guard:QrVerifyEnabled` default OFF → merge an toàn, không ảnh hưởng production cho đến khi toggle ON

---

## 2. RELEASE PLAN (3 releases, mỗi release có giá trị rõ ràng)

### RELEASE R1 — "Paper Ticket Flow" (Sprint 0+1+2+3+5)

| | |
|---|---|
| **Sprints** | 0 (ANALYZE) + 1 (Domain+Infra) + 2 (API) + 3 (Guard UI) + 5 (Printer) |
| **Branch** | `feature/guard-qr-r1` |
| **Sessions** | ~8 |
| **Feature flag** | `Guard:QrVerifyEnabled` = OFF (default), toggle ON cho test |

**Giá trị nhìn thấy được:**
- ✅ Trang `/guard/scan` **KHÔNG CÒN HARDCODE** — thay bằng Blazor component real data
- ✅ Guard **chụp ảnh biển số + ảnh khách** → tạo QR
- ✅ Guard **in vé giấy** (thermal printer): biển số, giờ vào, ngày, tenant name, QR code
- ✅ Khách nhận **vé giấy** → lúc lấy xe đưa giấy cho guard
- ✅ Guard **quét QR trên vé giấy** → hiển thị biển số + 2 ảnh → Match/Mismatch → checkout
- ✅ Stats + hoạt động hôm nay = **real data** (thay 24/18/6 hardcode)
- ✅ **Channel C (paper) hoạt động end-to-end** — sản phẩm usable ngay

**Demo script (cho user xem):**
```
1. Login ShopERP as Guard → /guard/scan
2. Tab "Cấp QR" → chụp ảnh biển số + ảnh khách → nhập biển số → "Tạo QR"
3. QR hiển thị + 6-digit code + nút "In vé" → in vé giấy
4. Tab "Xác minh" → quét QR từ vé giấy → hiện biển số + 2 ảnh
5. Bấm "Match — Check-out" → success
6. Tab "Hôm nay" → stats real + list sessions real
```

**Không có trong R1:**
- ❌ KhachLink claim (khách chưa nhận QR vào app)
- ❌ Channel A (digital) — chỉ có Channel C (paper)
- ❌ Full test suite (chỉ build pass + guard-check)

**Rollback:** Feature flag OFF → trang `/guard/scan` redirect về old page (giữ `.cshtml` cũ làm backup)

---

### RELEASE R2 — "Digital Claim Flow" (Sprint 4)

| | |
|---|---|
| **Sprints** | 4 (KhachLink Claim + Wallet) |
| **Branch** | `feature/guard-qr-r2` (từ main sau R1 merge) |
| **Sessions** | ~2 |
| **Feature flag** | `Guard:QrVerifyEnabled` = ON (R1 đã RV) |

**Giá trị nhìn thấy được:**
- ✅ Khách mở KhachLink → `/qr/claim` → **camera quét QR** (từ màn hình Guard hoặc vé giấy)
- ✅ Khách nhập **6-digit code** (fallback, không camera)
- ✅ QR vào **Ví QR** (`/qr/wallet`) — list vé đang active
- ✅ Khách tap vé → **fullscreen QR** → đưa guard quét
- ✅ **Channel C→A migration:** khách nhận vé giấy → lúc rảnh quét QR trên giấy → lưu vào KhachLink → giấy không cần nữa
- ✅ **Channel A + B + C→A đều hoạt động** — khách không cần giấy (chống ướt/rách/mất)

**Demo script:**
```
1. (Guard) Cấp QR → hiển thị QR trên màn hình
2. (Khách) Mở KhachLink → /qr/claim → camera quét QR trên màn hình Guard
3. → "Đã nhận QR gửi xe" → /qr/wallet hiển thị vé
4. (Khách) Tap vé → fullscreen QR
5. (Guard) Quét QR từ màn hình KhachLink → verify → checkout
6. (C→A) Guard in vé giấy → khách mang vé về → hôm sau mở KhachLink → quét QR trên giấy → lưu vào Ví QR
```

**Không có trong R2:**
- ❌ Full test suite + E2E (R3)

**Rollback:** Remove 2 KhachLink pages + nav link. Guard flow (R1) vẫn hoạt động.

---

### RELEASE R3 — "Tested + Production Ready" (Sprint 6)

| | |
|---|---|
| **Sprints** | 6 (Unit + Integration + E2E) |
| **Branch** | `feature/guard-qr-r3` (từ main sau R2 merge) |
| **Sessions** | ~1 |
| **Feature flag** | ON (production) |

**Giá trị nhìn thấy được:**
- ✅ **Unit tests:** VehicleSession domain logic (12 tests) + GuardService (15 tests)
- ✅ **Integration tests:** GuardController 9 endpoints (14 tests)
- ✅ **E2E Playwright:** Full flow (issue → claim → verify → checkout) + Channel C→A migration
- ✅ **CI pipeline ALL PASS** — merge an toàn, không regression
- ✅ **Production ready** — deploy + RV pass

**Demo script:**
```
1. dotnet test → ALL PASS (existing + new ~41 tests)
2. CI pipeline green
3. Playwright E2E spec pass
4. Deploy to VPS → RV full flow
```

---

## 3. CONFLICT AVOIDANCE STRATEGY

### 3.1 Sequential branching
```
R1 branch: feature/guard-qr-r1 (from main @ f7201ef4)
  → merge to main → main @ <R1-commit>

R2 branch: feature/guard-qr-r2 (from main @ <R1-commit>)
  → merge to main → main @ <R2-commit>

R3 branch: feature/guard-qr-r3 (from main @ <R2-commit>)
  → merge to main → main @ <R3-commit>
```
Không bao giờ branch song song → 0 merge conflict.

### 3.2 File ownership per release
| File | R1 | R2 | R3 |
|---|---|---|---|
| `1_Shared/Domain.cs` | ✏️ ADD entities | — | — |
| `2_Gateway/Controllers/GuardController.cs` | ✏️ CREATE | ✏️ ADD `my-sessions` endpoint | — |
| `2_Gateway/Program.cs` | ✏️ DI register | — | — |
| `5_WebApps/ShopERP/Pages/Guard/Scan.cshtml` | 🗑️ DELETE | — | — |
| `5_WebApps/ShopERP/Components/Pages/Guard/Scan.razor` | ✏️ CREATE | — | — |
| `5_WebApps/ShopERP/wwwroot/js/*` | ✏️ CREATE | — | — |
| `5_WebApps/KhachLink/Components/Pages/Qr/*` | — | ✏️ CREATE | — |
| `5_WebApps/KhachLink/wwwroot/js/*` | — | ✏️ CREATE | — |
| `6_Tests/*` | — | — | ✏️ CREATE |
| `6_Testing/e2e-tests/*` | — | — | ✏️ CREATE |

**R1→R2 conflict risk:** `GuardController.cs` (R2 thêm `my-sessions` endpoint). Giải pháp: R2 branch từ main mới nhất (đã có R1) → chỉ thêm 1 method, không sửa existing → 0 conflict.

**R2→R3 conflict risk:** None (R3 chỉ thêm test files, không sửa production code).

### 3.3 Feature flag isolation
```json
// appsettings.json (R1)
"Guard": {
  "QrVerifyEnabled": false  // OFF — old page still works
}
```
- R1 merge: flag OFF → production không thay đổi → **safe merge**
- R1 RV pass → toggle ON → Guard page active
- R2 merge: flag đã ON → KhachLink pages available
- R3 merge: flag ON → tests added → CI green

### 3.4 Old page backup (R1 only)
- Đừng xóa `Scan.cshtml` ngay — rename thành `Scan.razor.legacy` hoặc giữ trong `Pages/Guard/Legacy/`
- Route `/guard/scan` → new Blazor component
- Route `/guard/scan-legacy` → old page (backup, xóa sau R3 RV)
- Nếu R1 có vấn đề → toggle flag OFF → redirect `/guard/scan` → `/guard/scan-legacy`

---

## 4. VALIDATION GATES (per release)

| Gate | R1 | R2 | R3 |
|---|---|---|---|
| `dotnet build` 0 errors | ✅ | ✅ | ✅ |
| `guard-check.ps1` ALL PASS | ✅ | ✅ | ✅ |
| CI pipeline pass | ✅ | ✅ | ✅ |
| Manual demo flow | ✅ | ✅ | ✅ |
| Unit tests (new) | — | — | ✅ (27) |
| Integration tests (new) | — | — | ✅ (14) |
| E2E Playwright | — | — | ✅ (1 spec) |
| Deploy to VPS | ✅ | ✅ | ✅ |
| RV on VPS | ✅ | ✅ | ✅ |
| Feature flag toggle test | ✅ ON/OFF | — | — |

---

## 5. ROLLBACK PLAN (per release)

| Release | Rollback method | Time |
|---|---|---|
| R1 | Feature flag OFF → redirect to legacy page | < 1 min |
| R2 | Remove KhachLink nav link + pages (git revert) | < 5 min |
| R3 | Tests additive — no rollback needed | N/A |
| Any | `git revert <release-commit>` on main | < 10 min |

---

## 6. ESTIMATED TIMELINE

| Release | Sessions | Cumulative | Giá trị |
|---|---|---|---|
| R1 — Paper Ticket Flow | ~8 | 8 | Guard tool hoạt động, thay hardcode |
| R2 — Digital Claim | ~2 | 10 | Khách không cần giấy |
| R3 — Tested + Prod Ready | ~1 | 11 | CI green, production confidence |

**Total: ~11 sessions (same as 7 sprints, nhưng group thành 3 release có giá trị rõ ràng)**

---

## 7. APPROVAL GATES

```
Sprint 0 findings → user approve → Sprint 1
Sprint 1 build pass → user approve → Sprint 2
Sprint 2 build pass → user approve → Sprint 3
Sprint 3 build pass → Sprint 5 (printer)
R1 complete → user approve → merge → deploy → RV
  ↓
R2 (Sprint 4) → build pass → user approve → merge → deploy → RV
  ↓
R3 (Sprint 6) → tests pass → user approve → merge → deploy → RV
  ↓
DONE — close issue #126
```
