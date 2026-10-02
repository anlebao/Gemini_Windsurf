# TASK CARD: Phase 7 — RV production + tài liệu

> **Master plan:** `docs/AI/plans/tt71-htx-accounting-master-plan.md`
> **Status:** PENDING

## 1. OBJECTIVE

Deploy + RV production 5-layer (theo `.devin/rules/runtime-verification.md`) + cập nhật project_state.

## 2. RV CHECKLIST

- [ ] **L1 API:** tenant HTX — account chart TT 71 seeded (đếm AccountCharts theo standard) · phiếu thu/chi tạo entry với TK HTX hợp lệ · IncomeStatement/BalanceSheet với standard=TT71 trả đúng cấu trúc (B02-HTX mã 01a/01b/10a/10b/20a/20b; B01-HTX mã 110/310/500)
- [ ] **L2 Markers:** binaries có `Tt71Templates`, `AccountingStandard.TT71_2024`, `TenantType.HTX`
- [ ] **L3/L4 Playwright:** (sau khi build pass + Gate 3) tenant HTX: mở /accounting/revenue → account select chỉ 511/512/558; /accounting/expenses → 642/658; /accounting/trial-balance auto TT 71; /accounting/cash-flow ẩn; in Phiếu thu 01-TT/Phiếu chi 02-TT render đúng
- [ ] **L5 Browser manual:** user tạo/đăng nhập tenant HTX thật → nhập phiếu thu/chi → in chứng từ → xem B01/B02/B09-HTX

## 3. DATA/QUY TRÌNH

- (Q3) Backfill tenant HTX cũ → SetTenantType(HTX, TT71) — trước RV nếu duyệt
- Dọn dữ liệu test sau RV (chuẩn "cleanup pristine")

## 4. TÀI LIỆU

- [ ] `docs/AI/project_state.md` — Sections 2/3/4/10 (objective + status + next actions + maintenance log)
- [ ] Task cards Phase 1-7 → COMPLETE
- [ ] Master plan → status DONE

## 5. VERIFICATION

```powershell
# CD Multi-VPS sau push; theo dõi gh run list (không dùng gh run watch)
```
