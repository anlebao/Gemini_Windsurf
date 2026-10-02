# TASK CARD: Phase 7 — RV production + tài liệu

> **Master plan:** `docs/AI/plans/tt71-htx-accounting-master-plan.md`
> **Status:** 🏗 PARTIAL 2026-10-02 — L1 ✅ + L2 ✅ + Gate 4 spec viết ✅ · L3/L4/L5 ⏳ chờ tenant HTX thật trên production (hiện 0 HtxProfile/0 tenant Type=5)

## 1. OBJECTIVE

Deploy + RV production 5-layer (theo `.devin/rules/runtime-verification.md`) + cập nhật project_state.

## 2. RV CHECKLIST

- [x] **L1 API:** **chart TT 71 seeded — ✅ Gateway PG `AccountCharts` Standard=3 (TT71_2024) = 89 TK** (Standard 0=75 TT99 · 1=51 TT133 · 3=89 TT71; accounting DB = PG theo Option C — ShopERP SQLite AccountCharts rỗng là bình thường, business-side). Phiếu thu/chi entry TK HTX + B01/B02-HTX structure = integration tests T3/T4 (service-level) — production UI path chờ tenant HTX (L3/L4)
- [x] **L2 Markers:** ✅ S2/S3 — `Tt71Templates`/`GenerateTt71Async`/`GenerateTt71NotesAsync` trong `/app/VanAn.CoreHub.dll`; `VietnameseCurrencyText`/`AccountingAccountProvider`/`AccountingStandardResolver`/`Tt71ReceiptVoucher`/`Tt71PaymentVoucher` trong `/app/VanAn.ShopERP.dll` (vanan-shop-a)
- [ ] **L3/L4 Playwright:** spec `6_Testing/e2e-tests/tt71-htx.spec.ts` **VIẾT XONG** (Gate 4, self-gating — skip nếu tenant không HTX, an toàn CI tier-full): revenue options 511/512/558 · expenses 642/658 · trial-balance auto TT71 · cash-flow chặn B03 · hub ẩn B03. **CHẠY ⏳ chờ session tenant HTX thật** (production hiện 0 HTX — user tạo tenant HTX qua membership, rồi chạy `npx playwright test e2e-tests/tt71-htx.spec.ts` với storageState HTX)
- [ ] **L5 Browser manual:** user tạo/đăng nhập tenant HTX thật → nhập phiếu thu/chi → in 01-TT/02-TT (A4) → xem B01/B02/B09-HTX + FinancialReports hub

## 3. DATA/QUY TRÌNH

- Production hiện tại: **0 HtxProfile / 0 tenant Type=5** (verified PG 2026-10-02) — backfill S1 không có tenant nào để đổi; không tạo fixture test trên production (tránh bẩn dữ liệu) — chờ user tạo tenant HTX thật
- Nếu chạy spec với tenant test: dọn dữ liệu sau RV (chuẩn "cleanup pristine")

## 4. TÀI LIỆU

- [x] `docs/AI/project_state.md` — Sections 2/3/4/10 (S1-S3 DONE + S4 tests; L3/L4/L5 user-pending)
- [x] Task cards Phase 1-7 → COMPLETE/PARTIAL (phase7: L1/L2/spec done, L3-L5 user-pending)
- [x] Master plan → status: code DONE — RV đầy đủ chờ tenant HTX thật

## 5. VERIFICATION

```powershell
# CD Multi-VPS sau push; theo dõi gh run list (không dùng gh run watch)
```
