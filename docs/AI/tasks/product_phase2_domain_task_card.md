# TASK CARD — Phase 2: Domain — Product.Update() Method

> **Master plan:** `docs/AI/tasks/quicksetup_product_management_master_plan.md` (Section 3)
> **Branch:** `feature/product-mgmt-phase2-domain`
> **Priority:** 0 (Critical — BLOCKING Phase 3+)
> **Mode:** IMPLEMENT (Domain Phase active, user approval granted)
> **Prerequisite:** Phase 1 merged (hoặc chạy song song — Phase 2 độc lập với Phase 1)

---

## 0. CONTEXT & DECISIONS (locked)

### Domain facts (verified 2026-07-14)
- `Product` entity tại <ref_snippet file="C:/VibeCoding/Gemini_Windsurf/1_Shared/Domain.cs" lines="559-603" />
  - Fields: `ProductId`, `Name`, `Description`, `Price`, `CostPrice`, `Category`, `IsActive`, `ImageUrl`, `VatRate`, `TenantId` (from BaseEntity)
  - Existing methods: `UpdateCostPrice(decimal)` (line 598)
  - **Missing:** `Update()`, `Deactivate()`, `Activate()`
- `BaseEntity` tại <ref_snippet file="C:/VibeCoding/Gemini_Windsurf/1_Shared/Domain/Common.cs" lines="75-117" />
  - `IsDeleted` (line 85) — separate field từ `IsActive`
  - `UpdateAudit(string? updatedBy = null)` (line 96) — **BẮT BUỘC gọi sau mỗi mutation**
  - `MarkAsDelete(string? updatedBy = null)` (line 105) — set `IsDeleted = true`
- Pattern: **mọi** mutation method trong Domain đều gọi `UpdateAudit()` (47 occurrences trong Domain.cs)

### User decisions (locked 2026-07-14)
- **G5 — UpdateAudit():** BẮT BUỘC gọi `UpdateAudit()` trong `Update()`, `Deactivate()`, `Activate()`.
- **G6 — IsActive vs IsDeleted:** Phân biệt rõ:
  - `Deactivate()` → set `IsActive = false` (hide khỏi catalog public, vẫn hiện trong management)
  - `Activate()` → set `IsActive = true` (hiện lại catalog)
  - `MarkAsDeleted()` → set `IsDeleted = true` (true soft delete — ẩn khỏi mọi query, kể cả management list default)
  - DELETE endpoint (Phase 3) sẽ gọi `MarkAsDeleted()` — **KHÔNG** dùng `Deactivate()` cho delete.

---

## 1. TASKS

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P2-T1 | Thêm `Update()` method vào `Product` entity. Signature: `public void Update(string name, string description, decimal price, string category, bool isActive, string? imageUrl, decimal vatRate, string? updatedBy = null)`. Validation: `name` empty → `ArgumentException`; `price < 0` → `ArgumentException`; `vatRate < 0` → `ArgumentException`. Set các field + gọi `UpdateAudit(updatedBy)`. | `1_Shared/Domain.cs` (sau line 602, trước `}` đóng class Product line 603) | ⬜ |
| 2 | P2-T2 | Thêm `Deactivate(string? updatedBy = null)` method: `IsActive = false; UpdateAudit(updatedBy);` | same | ⬜ |
| 3 | P2-T3 | Thêm `Activate(string? updatedBy = null)` method: `IsActive = true; UpdateAudit(updatedBy);` | same | ⬜ |
| 4 | P2-T4 | Thêm `MarkAsDeleted(string? updatedBy = null)` method override (gọi base `MarkAsDelete` từ BaseEntity): `base.MarkAsDelete(updatedBy);` — set `IsDeleted = true`. **Lưu ý:** BaseEntity.MarkAsDelete là `protected`, Product có thể gọi trực tiếp. | same | ⬜ |
| 5 | P2-T5 | Verify build: `dotnet build VanAn.sln` 0 errors + `guard-check.ps1` pass. | Solution-wide | ⬜ |

---

## 2. EXIT CRITERIA

- [ ] `Product.Update(name, description, price, category, isActive, imageUrl, vatRate, updatedBy?)` tồn tại
- [ ] `Product.Deactivate(updatedBy?)` tồn tại (set `IsActive = false`)
- [ ] `Product.Activate(updatedBy?)` tồn tại (set `IsActive = true`)
- [ ] `Product.MarkAsDeleted(updatedBy?)` tồn tại (set `IsDeleted = true` via base)
- [ ] Mọi method gọi `UpdateAudit(updatedBy)` hoặc `base.MarkAsDelete(updatedBy)`
- [ ] Validation: `name` empty → throw; `price < 0` → throw; `vatRate < 0` → throw
- [ ] Domain layer vẫn pure (no EF Core, no DbContext, no DataAnnotations)
- [ ] Build: 0 errors

---

## 3. ANTI-PATTERNS (KHÔNG làm)

- ❌ Thêm `using Microsoft.EntityFrameworkCore` vào Domain.cs
- ❌ Thêm `[Required]`, `[Column]`, `[Table]` attributes (DataAnnotations) vào Product
- ❌ Quên `UpdateAudit()` — audit trail bị hỏng
- ❌ Đánh đồng `Deactivate()` với `MarkAsDeleted()` (G6)
- ❌ Sửa existing `UpdateCostPrice()` method (chỉ thêm method mới)
- ❌ Thêm field mới vào Product (chỉ thêm method)
- ❌ Public setter cho `Name`, `Price`, etc. (giữ `protected set` — encapsulation)

---

## 4. ROLLBACK PLAN

Nếu Phase 2 fail sau 3 rounds:
1. Revert `Domain.cs` về commit trước phase
2. Report: compile error cụ thể, evidence
3. ** KHÔNG** sửa BaseEntity để workaround

---

## 5. VERIFICATION CHECKLIST

```powershell
# 1. Build
dotnet build VanAn.sln
# Expected: 0 errors

# 2. Guard check
.\scripts\guard-check.ps1
# Expected: PASS

# 3. Domain purity check (manual)
# - Mở 1_Shared/Domain.cs
# - Verify Product class không có using EF Core / DataAnnotations
# - Verify mọi method mới gọi UpdateAudit() hoặc base.MarkAsDelete()

# 4. Unit test (optional — nếu có test project)
# - Test Update() với name empty → ArgumentException
# - Test Update() với price = -1 → ArgumentException
# - Test Deactivate() → IsActive = false, UpdatedAt thay đổi
# - Test Activate() → IsActive = true
# - Test MarkAsDeleted() → IsDeleted = true
```
