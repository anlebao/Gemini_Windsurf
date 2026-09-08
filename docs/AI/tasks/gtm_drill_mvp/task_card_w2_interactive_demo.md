# Task Card W2: Interactive Demo — Preview Storefront

> **Status:** ⏳ NEXT (awaiting session start)
> **Week:** W2 / 5
> **Effort:** ~1 tuần
> **Master plan:** `docs/AI/tasks/gtm_drill_mvp/master_plan.md`
> **Top-level card:** `docs/AI/tasks/gtm_drill_mvp/task_card.md`
> **Prerequisite:** ✅ W1 COMPLETE + RV PASS

## Objective

Merchant tự xem storefront mock **trước khi đăng ký** — nhập tên quán + ngành → dựng storefront demo (logo, 3-5 sản phẩm mẫu, giờ mở cửa, màu theme) → nút "Đưa cửa hàng lên TimLaThay" → `/claim?name=...` prefill.

**Demo = onboarding.** Merchant thấy kết quả trước, đăng ký sau. Không cần account, không persistence — session-only state.

## Scope Checklist

### Task 2.1: Demo preview page
**Files:** `5_WebApps/KhachLink/Pages/Demo.razor` — NEW · `5_WebApps/KhachLink/Pages/DemoStoreState.cs` — NEW (in-memory model)
- [ ] Route `/demo`, anonymous, **không persistence** (session-only state)
- [ ] Input: tên quán + ngành → dựng storefront mock:
  - Logo (upload qua `ImageUploadService` có sẵn [V])
  - 3-5 sản phẩm mẫu theo ngành (name + price editable, thêm/xóa)
  - Giờ mở cửa (editable)
  - Chọn màu theme (reuse theme system từ `KhachLinkLayout`)
- [ ] Render tái dùng markup pattern từ `Store.razor` (storefront card). **UI Platform components.**
- [ ] Session state: `DemoStoreState` (CascadingValue hoặc component state — KHÔNG inject DbContext, KHÔNG localStorage cho data thật)

### Task 2.2: Demo → Claim chuyển tiếp
- [ ] Nút "Đưa cửa hàng lên TimLaThay" → `NavigateTo($"/claim?name={Uri.EscapeDataString(demoName)}")`
- [ ] **File:** `5_WebApps/KhachLink/Pages/Claim.razor` — UPDATE: đọc query param `name` prefill form
  - `NavigationManager.TryParseQuery` → `name` param → prefill `ShopName` field
  - Backward compatible: nếu không có `name` param, form trống như cũ

### Task 2.3: E2E spec
**File:** `6_Testing/e2e-tests/gtm-demo.spec.ts` — NEW
- [ ] Vào /demo → nhập tên → thấy storefront mock
- [ ] Sửa 1 sản phẩm (name + price) → thấy update render
- [ ] Upload logo (hoặc skip nếu không có test image) → thấy logo render
- [ ] Bấm "Đưa cửa hàng lên TimLaThay" → landing `/claim` với tên đã prefill
- [ ] Verify `name` query param matches input

## Prerequisites

- ✅ W1 COMPLETE + RV PASS (2026-09-07)
- ✅ KhachLink `ImageUploadService` (live)
- ✅ KhachLink `Store.razor` storefront markup (reuse pattern)
- ✅ KhachLink `Claim.razor` + `ClaimHttpService` (live)
- ✅ KhachLink theme system (`KhachLinkLayout.razor`)
- ✅ UI Platform components
- ⏳ `GrowthMachine:Enabled` flag — W2 page có thể build không cần flag (demo page không nhạy cảm — defer flag gating đến W5 nếu user muốn)

## Verification

1. **Build:** `dotnet build VanAn.sln` → 0 errors
2. **Local smoke test:** `dotnet run --project 5_WebApps/KhachLink` → http://localhost:5002/demo render Demo page
3. **Demo flow:** nhập tên "Quán Test" + ngành "Cà phê" → thấy storefront mock với 3-5 sản phẩm cà phê mẫu
4. **Edit product:** sửa tên + giá → render update
5. **Logo upload:** upload image → logo render trong storefront mock
6. **Theme select:** chọn màu → storefront mock đổi màu
7. **CTA:** bấm "Đưa cửa hàng lên TimLaThay" → navigate `/claim?name=Quán%20Test` → form prefilled
8. **E2E:** `gtm-demo.spec.ts` PASS (chạy sau build, theo Playwright rules)

## Governance checklist

- [ ] Domain purity: 0 domain mods (W2 không đụng Domain.cs)
- [ ] KhachLink HTTP-only: Demo page không inject DbContext, không gọi Gateway (session-only state)
- [ ] UI Platform: Demo.razor dùng VanAn.UI.Platform components — no custom CSS
- [ ] Không tạo .csproj mới
- [ ] No persistence: `DemoStoreState` = session-only, KHÔNG lưu DB/localStorage cho data thật
- [ ] ImageUploadService: reuse existing (đã có [V])

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Demo state mất khi refresh | Session-only by design — refresh = reset demo. Hiển thị hint "Refresh sẽ đặt lại demo". KHÔNG persistence MVP. |
| R2 | ImageUploadService cần auth | Verify: ImageUploadService có anonymous path? Nếu cần auth → demo logo upload defer v2, dùng placeholder image theo ngành. |
| R3 | Store.razor markup quá phức tạp để reuse | Extract storefront card pattern thành component riêng (nếu cần) hoặc copy markup pattern (không extract nếu đơn giản) |
| R4 | Sản phẩm mẫu theo ngành không có data | Hardcode 3-5 sản phẩm mẫu cho 5-10 ngành phổ biến (cà phê, phở, tạp hóa, salon, tiệm nail...). Ngành khác = generic 3 sản phẩm. |

## Files

| # | File | Action | Status |
|---|---|---|---|
| 1 | `5_WebApps/KhachLink/Pages/Demo.razor` | NEW | ⏳ |
| 2 | `5_WebApps/KhachLink/Pages/DemoStoreState.cs` | NEW (in-memory model) | ⏳ |
| 3 | `5_WebApps/KhachLink/Pages/Claim.razor` | UPDATE (name prefill) | ⏳ |
| 4 | `6_Testing/e2e-tests/gtm-demo.spec.ts` | NEW | ⏳ |

## Open questions (resolve before/during W2 session)

1. **ImageUploadService auth:** Demo page (anonymous) có upload được logo không? → verify `ImageUploadService` auth requirement lúc implement. Nếu cần auth → defer logo upload v2, dùng placeholder.
2. **Sản phẩm mẫu data source:** Hardcode 3-5 sản phẩm cho 5-10 ngành phổ biến? Hay generate từ product catalog (cần Gateway call)? → Hardcode MVP (zero Gateway dependency, zero auth).
3. **Theme select scope:** Đầy đủ 5 theme (Classic/Teen/Lady/Premium/Modern) hay chỉ color picker? → Color picker MVP (đơn giản hơn, đủ "thấy kết quả").

## Related

- Master plan: `docs/AI/tasks/gtm_drill_mvp/master_plan.md`
- Top-level card: `docs/AI/tasks/gtm_drill_mvp/task_card.md`
- W1 (done): `docs/AI/tasks/gtm_drill_mvp/task_card_w1_merchant_audit.md`
- W3 (next): `docs/AI/tasks/gtm_drill_mvp/task_card_w3_revenue_proof.md`
- KhachLink Store page (reuse pattern): `5_WebApps/KhachLink/Pages/Store.razor`
- KhachLink Claim page (prefill target): `5_WebApps/KhachLink/Pages/Claim.razor`
- KhachLink ImageUploadService: `5_WebApps/KhachLink/Services/ImageUploadService.cs`
