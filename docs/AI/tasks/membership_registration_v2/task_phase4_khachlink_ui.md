# TASK CARD: Phase 4 — KhachLink UI (wizard 4 bước + Hồ sơ của tôi)

> **Master plan:** `docs/AI/plans/membership-registration-v2-master-plan.md`
> **Status:** ⏳ PENDING (chưa code)

## 1. CHANGES

| File | Change |
|---|---|
| `5_WebApps/KhachLink/Pages/MembershipRegister.razor` | Wizard 4 bước (thay form 1 trang): B0 entry (tên HTX + Điều lệ + login inline nếu thiếu token) · B1 3 card chọn loại (Official/LinkedCapital/LinkedNonCapital + "Xem Điều lệ vX") · B2 form động theo loại (prefill họ tên/SĐT, email, khu vực, vai trò; **field "Số vốn góp cam kết (VNĐ)"** bắt buộc cho Official/LinkedCapital; **tư cách**: Cá nhân / Tổ chức (HKD/DN — dropdown tenant sở hữu hoặc "Hệ thống tự tạo hồ sơ" cho collaborator)) · B3 xác nhận + consent + ký tay (tùy chọn) · B4 kết quả + nút "Theo dõi hồ sơ" |
| `5_WebApps/KhachLink/Pages/MembershipMyApplications.razor` (MỚI) | Trang "Hồ sơ của tôi": list hồ sơ mọi HTX + trạng thái (Draft/Submitted/NeedInfo/Approved/Rejected) + nút Nộp (Draft) / Bổ sung (NeedInfo→Resubmit) + nút Xem chi tiết |
| `5_WebApps/KhachLink/Services/Http/MembershipHttpService.cs` | `MyApplicationsAsync()` + `ResubmitApplicationAsync()` + `CreateMembershipApplicationRequestDto` += `CapitalContributionAmount` + DTO member-type/status parse |
| KhachLink nav/home | link "Hồ sơ thành viên HTX" (nếu có hồ sơ) |

## 2. LƯU Ý

- Detect collaborator: `/api/community/role` (isSalesman/isShipper) — chỉ hiện option "Hệ thống tự tạo hồ sơ" cho collaborator chưa có tenant sở hữu (D3 — tạo tại Approve, applicant không thấy tenant ngay).
- Số vốn góp: format tiền vi-VN (pattern Currency input hiện có), required khi Official/LinkedCapital, ẩn khi LinkedNonCapital; note "HTX xác nhận thu sau khi duyệt".
- UI Platform compliance: VanAnCard/VanAnButton/VanAnAlert/VanAnModal — không custom HTML/CSS (card selection dùng VanAnCard).
- KhachLink là Blazor WASM — HTTP qua Gateway (MembershipHttpService) — KHÔNG query DB.

## 3. ACCEPTANCE

- [ ] Wizard 3 loại đăng ký được (góp vốn đúng theo loại) + submit thành công
- [ ] Trang "Hồ sơ của tôi" render + submit/resubmit hoạt động
- [ ] ShopERP/KhachLink build 0 errors
