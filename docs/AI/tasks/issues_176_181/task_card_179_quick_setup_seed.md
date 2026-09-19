# Task Card #179: Quick-setup không lưu menu items / product list trống

> **Status:** NEEDS RV — chưa đủ bằng chứng runtime để sửa code (Gate 1: Anti-Guessing)
> **Priority:** P2 — onboarding bị vỡ
> **Created:** 2026-09-19
> **Master plan:** `docs/AI/tasks/issues_176_181/master_plan.md`
> **Effort:** 3-5h (RV + fix)

## Problem

`https://app2.khachvip.online/quick-setup?tenantId=1833b55c-1022-4a6c-8056-8935f503a62b` — chạy khởi tạo nhanh nhưng "không thấy menu items được lưu vào DB, danh sách sản phẩm của tenant trống".

## Đã verify (static)

Flow hiện tại:
```
QuickSetup.razor:442-484 (ProcessSetupAsync)
  → OnboardingService.ApplyTemplateAsync(templateId, shopId)   [in-process, 3_CoreHub]
      → OnboardingService.cs:101-103 idempotency check (Products count theo tenant)
      → FnbSeedStrategy.SeedAsync → Products(32) + Ingredients(15) + Recipes + Inventories
      → dbContext.SaveChangesAsync
  → success screen: ProductsCount/IngredientsCount = HARDCODED từ template metadata (QuickSetup.razor:468-470), KHÔNG đọc DB
  → exception bị nuốt: catch { Console.WriteLine } (QuickSetup.razor:475-478)
```

- `IVanAnDbContext` trong ShopERP = `ShopERPDbContext` (SQLite) — `Program.cs:152-154` → seed đúng nơi products cư trú (Option C).
- Products DbSet KHÔNG có global query filter (`ShopERPDbContext.cs:345-350` — chỉ Order/OrderItem/Outbox) → không bị filter che.
- Wizard hiển thị "Khởi tạo thành công" kể cả khi seed thất bại (số liệu hardcoded) → **giải thích "không thấy gì trong DB dù UI báo OK"**.

## Candidate root causes (chưa phân biệt được — cần RV)

1. Seed thực sự throw (vd: `Tenant` row chưa có trong SQLite Tenants cho tenant mới tạo PG — chưa verify nên không có TenantVerifiedEvent → không sync SQLite; seed Products vẫn ghi được vì Products không FK Tenants) — cần xem log container.
2. Seed OK nhưng product list user xem bị filter tenant khác / impersonation chưa đúng tenant.
3. Idempotency check `existingProducts > 0` nhầm (đếm nhầm tenant khác) → skip seed.

## Solution

### Phase A — RV production (bắt buộc trước khi sửa)
- [ ] A1: SSH `vanan-shop-a` (`gcloud compute ssh vanan-shop-a --zone asia-southeast1-b --project vanan-prod`) → `docker logs` ShopERP container tìm log `ApplyTemplateAsync: templateId=... → industryCode=...` (`OnboardingService.cs:96-98`) + `seed complete ... Products=` (`:135-138`) + exception trace.
- [ ] A2: Query SQLite (`Products` count theo tenantId `1833b55c-1022-4a6c-8056-8935f503a62b`) + kiểm tra `Tenants` row tồn tại trong SQLite.
- [ ] A3: Chạy lại quick-setup cho tenant TEST mới → đối chiếu kết quả (seed count thật).

### Phase B — Fix (theo root cause tìm được)
- [ ] B1: Không nuốt exception — hiển thị lỗi rõ ràng trong wizard (Alert đỏ thay vì Console.WriteLine).
- [ ] B2: Bỏ số hardcoded — đọc DB thật sau seed (`Products`/`Ingredients` count) cho màn hình thành công.
- [ ] B3: Fix root cause thực tế (vd: đảm bảo tenant row tồn tại trước seed, hoặc seed qua đúng path).

## Acceptance
- [ ] Quick-setup tạo tenant mới → SQLite có products thật → product list hiển thị → UI báo lỗi nếu fail
