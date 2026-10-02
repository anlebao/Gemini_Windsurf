# MASTER PLAN: Hỗ trợ Chế độ kế toán HTX theo TT 71/2024/TT-BTC

> Created: 2026-10-01
> Spec: `docs/specs/Thông tư 71-2024-TT-BTC.doc` (text extract: `.devin/tt71_extracted.txt`)
> Status: ⏳ PENDING USER REVIEW
> Branch (proposed): `feature/tt71-htx-accounting`

## 1. MỤC TIÊU

Bổ sung **Chế độ kế toán hợp tác xã (TT 71/2024/TT-BTC)** vào nền tảng kế toán:
- `AccountingStandard.TT71_2024` + `TenantType.HTX` + mapping tenant → chuẩn (HTX → TT 71)
- Chart tài khoản TT 71 đầy đủ (PL I: 51 TK cấp 1 + TK cấp 2/3)
- Phiếu thu/chi đúng TK theo chuẩn tenant (HTX: 511/512/558/642/658 — KHÔNG dùng 515/711/621/622/627/641/811)
- Báo cáo tài chính HTX: **B01-HTX** (Tình hình tài chính), **B02-HTX** (Kết quả hoạt động — tách giao dịch nội bộ/ngoài), **B09-HTX** (Thuyết minh)
- TT 71 **KHÔNG yêu cầu B03 (LCTT)** → ẩn/không sinh CashFlow cho tenant HTX

## 2. BỐI CẢNH — VÌ SAO MÂU THUẪN (review 2026-10-01)

| Khoản | TT 71/2024 (HTX) | Base code hiện tại |
|---|---|---|
| Chuẩn kế toán | Chế độ riêng của HTX | `AccountingStandard { TT99_2025, TT133_2016, TT58_2026 }` (`1_Shared/Domain.cs:3601`) — **thiếu TT71** |
| Kiểu tenant | HTX / Liên hiệp HTX | `TenantType { HKD, Enterprise_SuperSmall, Enterprise_SME, Enterprise_Large }` (`Domain.cs:3586`) — **thiếu HTX**; tenant có `HtxProfile` (membership 2026-09-29) rơi vào chuẩn DN |
| Chart TK | 511/512 (nội-ngoại bộ), 558, 611/612, 642, 658, 659, 442, 212 (tài sản chung không chia), 132/332 (tín dụng nội bộ)... | Chỉ seed TT 133 + TT 99 (`AccountChartSeeder.cs:32-33`); **515/711/621/622/627/641/811 không tồn tại trong TT 71**; **212 trùng mã khác nghĩa** (TT 99: TSCĐ thuê TC) |
| Phiếu thu | TK hợp lệ: 511, 512, 558 | `RevenueEntry.razor`: 511/512/**515/711** (515/711 sai) |
| Phiếu chi | TK hợp lệ: 642, 658, 611/612 | `ExpenseEntry.razor`: 621/622/627/641/642 (621/622/627/641 sai) |
| BCTC | B01-HTX, B02-HTX, B09-HTX (không có B03) | Mẫu DN: `Tt99Templates` (B01-DN/B02-DN/B 03-DN) — `IncomeStatementService.cs:79`, `BalanceSheetService.cs:80`; CashFlowStatement.razor hiện "B 03-DN" |

## 3. SCOPE (7 phases — Q1-Q5 đã chốt 2026-10-01)

| Phase | Tên | Layer | Gate | Task card |
|---|---|---|---|---|
| 1 | Domain: `AccountingStandard.TT71_2024` + `TenantType.HTX` + mapping + **backfill HTX cũ (Q3)** | 1_Shared + CoreHub | **Gate 5 (Domain)** — cần user approval | `task_phase1_domain_standard.md` |
| 2 | Chart TT 71 seeder (PL I đầy đủ) | 3_CoreHub (Seed) | — | `task_phase2_chart_seed.md` |
| 3 | Template BCTC HTX: `Tt71Templates` (B01-HTX + **B02-HTX + B09-HTX (Q2: làm luôn)**) + nhánh service | 3_CoreHub + ShopERP | — | `task_phase3_report_templates.md` |
| 4a | Phiếu thu/chi theo chuẩn tenant (account options động) | 5_WebApps/ShopERP | UI Platform compliance | `task_phase4_entry_forms.md` |
| 4b | **Mẫu in chứng từ PL II (Q5: CÓ)** — Phiếu thu **01-TT** + Phiếu chi **02-TT** (theo `docs/Accounting_Doc/phu-luc-II-Thong-tu-71.docx`) | 5_WebApps/ShopERP | UI Platform compliance | `task_phase4b_voucher_print.md` |
| 5 | UI chọn chuẩn + auto-map tenant→standard (kể cả HTX); **Q4: KHÔNG đụng menu/feature flags ngoài kế toán** | 5_WebApps/ShopERP | UI Platform compliance | `task_phase5_ui_standard.md` |
| 6 | Tests (seeder/template/form/mapper) | 6_Tests | — | `task_phase6_tests.md` |
| 7 | RV production + tài liệu (state/task cards) | production | Playwright Gate 3 | `task_phase7_rv.md` |

**Dependency chain:** 1 → 2 → 3 → (4a ∥ 4b ∥ 5) → 6 → 7.

## 4. DESIGN DECISIONS (đề xuất — chờ user duyệt)

| # | Decision | Ghi chú |
|---|---|---|
| D1 | `AccountingStandard.TT71_2024` thêm vào enum (cuối, KHÔNG đổi thứ tự hiện có — tránh break persisted value) | Cùng pattern TT58_2026 đã có sẵn nhưng chưa dùng |
| D2 | `TenantType.HTX = 5` (sau Enterprise_Large = 4) | `HtxProfileService.CreateProfileAsync` (hoặc membership flow) set `TenantType.HTX + AccountingStandard.TT71_2024` khi tạo profile HTX |
| D3 | Chart TT 71 seed độc lập (Standard=TT71_2024) — **trùng mã khác nghĩa (212) KHÔNG xung đột** vì key = (Standard, Code) | `AccountChartEntity` đã có Standard field; thêm `GetTt71Accounts()` + seed trong `SeedAsync` |
| D4 | `Tt71Templates` (mirror `Tt99Templates`): `BalanceSheetTt71` (B01-HTX), `IncomeStatementTt71` (B02-HTX), `NotesTt71` (B09-HTX — scope mở rộng, có thể defer) | B02-HTX tách **giao dịch nội bộ/ngoài** (01a/01b...20a/20b) — template line cần field nhãn + account mapping riêng |
| D5 | Service branch: `IncomeStatementService`/`BalanceSheetService` — `standard == TT71_2024` → dùng `Tt71Templates` | CashFlow: TT 71 không yêu cầu → `CashFlowStatement.razor` ẩn tab khi tenant HTX |
| D6 | Phiếu thu/chi: account options tính theo `TenantProvider` type/standard — HTX: thu = 511/512/558; chi = 642/658 (+611/612 nếu cần) | Tránh hardcode; helper dùng chung (giống `GetRevenueAccounts()` hiện tại nhưng có branch) |
| D7 | **Sổ kế toán HTX (PL III) — DEFER**: spec `Thông tư 71-2024-TT-BTC.doc` (PL I/II/IV) + `phu-luc-II-Thong-tu-71.docx` (PL II) đều KHÔNG có PL III. Cần file PL III riêng (user cung cấp sau) hoặc dùng sổ hiện có theo dõi theo TK TT 71 | Q1 RESOLVED 2026-10-01 — PL II có (chứng từ), PL III vẫn thiếu → defer sổ |
| D8 | **Backfill tenant HTX cũ (Q3 = CÓ):** sau Phase 1, script cập nhật tenant đã có `HtxProfile` → `SetTenantType(HTX, TT71_2024)` (idempotent, chạy khi deploy) | Q3 RESOLVED |
| D9 | **Mẫu in chứng từ PL II (Q5 = CÓ):** Phase 4b — Phiếu thu **01-TT** + Phiếu chi **02-TT** (structure từ `docs/Accounting_Doc/phu-luc-II-Thong-tu-71.docx`), render từ dữ liệu entry thật | Q5 RESOLVED |

## 5. CHART TÀI KHOẢN TT 71 (PL I — verified từ spec)

**Loại Tài sản:** 111 Tiền mặt (1111/1112) · 112 Tiền gửi NH (1121/1122) · 121 Đầu tư tài chính (1211/1218) · 131 Phải thu khách hàng · 132 Phải thu cho vay nội bộ (1321/13211/13212/1322) · 133 Thuế GTGT được khấu trừ (1331/1332) · 136 Phải thu nội bộ HTX (1361/1368) · 138 Phải thu khác · 141 Tạm ứng · 151 Hàng mua đang đi đường · 152 Vật liệu, dụng cụ · 154 CPSXKD dở dang · 156 Thành phẩm, hàng hóa · 157 Hàng gửi đi bán · 211 TSCĐ (2111/2113/2114/2117) · **212 Tài sản chung không chia** · 214 Hao mòn TSCĐ (2141/2142/2143/2144/2147) · 229 Dự phòng tổn thất tài sản · 242 Tài sản khác (2421/2422)

**Nợ phải trả:** 331 Phải trả người bán · 332 Phải trả tín dụng nội bộ (3321/33211/33212/3322) · 333 Thuế và các khoản phải nộp NN (3331/3334/3338) · 334 Phải trả NLĐ · 335 Các khoản phải nộp theo lương · 336 Phải trả nội bộ HTX (3361/3368) · 338 Phải trả khác · 341 Phải trả nợ vay · 342 Khoản hỗ trợ NN phải hoàn lại · 353 Quỹ khen thưởng, phúc lợi (3531/3532/3533)

**Vốn chủ sở hữu:** 411 Vốn đầu tư của chủ sở hữu (4111 Vốn góp thành viên/4118) · 418 Các quỹ thuộc VCSH · 421 LN sau thuế chưa phân phối (4211 bên ngoài/4212 nội bộ) · **442 Quỹ chung không chia của HTX (4421/4422)**

**Doanh thu:** **511 Doanh thu giao dịch bên ngoài (5111/5112/5113)** · **512 Doanh thu giao dịch nội bộ** · 521 Các khoản giảm trừ doanh thu · **558 Thu nhập khác**

**Chi phí:** **611 Giá vốn hàng bán giao dịch bên ngoài** · **612 Chi phí giao dịch nội bộ** · **642 Chi phí quản lý kinh doanh** · **658 Chi phí khác** · **659 Chi phí thuế TNDN** · 911 Xác định kết quả kinh doanh

**TK ngoài bảng:** 001, 002, 003, 004, 005, 006, 007, 008

> ⚠️ Lưu ý hạch toán đặc thù HTX (PL I): thu/chi tiền mặt BẮT BUỘC phiếu thu/phiếu chi đủ chữ ký (mục Nguyên tắc kế toán tiền); giao dịch nội bộ theo dõi riêng (132/136/332/336/4211/4212/512/612); quỹ chung không chia (442) + tài sản chung không chia (212).

## 6. BÁO CÁO TÀI CHÍNH HTX (PL IV — verified)

- **B01-HTX** — Báo cáo tình hình tài chính: chỉ tiêu tài sản (110 Tiền, 120 ĐTTC, 130 Phải thu, 140 Hàng tồn kho, 150 TSCĐ, 160 TSCĐ chung không chia?...), nguồn vốn (310 Phải trả người bán, 320 Người mua trả tiền trước, 330 Thuế, 340...), VCSH (400), tổng 500. → Cần trích chi tiết từ spec khi làm Phase 3.
- **B02-HTX** — Kết quả hoạt động: 01 Doanh thu SXKD (01a bên ngoài/01b nội bộ), 02 Giảm trừ (02a/02b), 10 DT thuần (10a/10b), 11 Giá vốn (11a/11b), 12 CP QLKD (12a/12b), 20 KQ SXKD (20a/20b), 31 Thu nhập khác, 32 Chi phí khác, 40 LN khác, 50 LNTT, 51 CP thuế TNDN, 60 LNST
- **B09-HTX** — Thuyết minh BCTC (đặc điểm HTX, kỳ kế toán, chế độ áp dụng, thông tin bổ sung từng khoản mục) — **defer hoặc Phase 3b**
- **KHÔNG có B03 (LCTT)** trong TT 71

## 7. GATES & HARD STOPS

| Gate | Áp dụng phase | Action |
|---|---|---|
| **Gate 5 (Domain)** | Phase 1 | User-approved exception bắt buộc (sửa `Domain.cs`: enum + `Tenant.SetTenantType`) |
| Single-Identity Pattern | All | Không đụng identity; chỉ thêm enum value + method |
| Domain Protection | All | KHÔNG sửa `AccountingEntry` (immutable) |
| UI Platform compliance | Phase 4, 5 | VanAButton/VanACard/VanAAlert/VanASelect — KHÔNG custom HTML/CSS |
| Playwright Gate 3 | Phase 1-6 | Playwright DISABLED khi code; chỉ Phase 7 |
| KhachLink HTTP-only | N/A | Không đụng KhachLink |

## 8. OPEN QUESTIONS (ALL RESOLVED 2026-10-01)

| # | Question | Resolution |
|---|---|---|
| Q1 | PL III (Sổ kế toán HTX)? | **RESOLVED:** user cung cấp `docs/Accounting_Doc/phu-luc-II-Thong-tu-71.docx` = **PL II chứng từ đầy đủ** (01-VT→07-VT, 01-BH→03-BH, **01-TT→09-TT** (Phiếu thu/chi), 01-TSCĐ→06-TSCĐ, 01a/01b-LĐTL→11-LĐTL). **PL III (sổ) vẫn chưa có** → sổ HTX defer (D7) |
| Q2 | B09-HTX (Thuyết minh BCTC)? | **RESOLVED: làm luôn** — Phase 3 gồm B01-HTX + B02-HTX + B09-HTX |
| Q3 | Backfill tenant HTX cũ? | **RESOLVED: CÓ** — `SetTenantType(HTX, TT71_2024)` idempotent khi deploy (Phase 1) |
| Q4 | `TenantType.HTX` ảnh hưởng menu/feature flags ngoài kế toán? | **RESOLVED: KHÔNG** — chỉ trong scope kế toán |
| Q5 | Mẫu in chứng từ PL II? | **RESOLVED: CÓ** — Phase 4b: Phiếu thu **01-TT** + Phiếu chi **02-TT** |

## 9. ACCEPTANCE CRITERIA

- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `guard-check.ps1` PASS
- [ ] `AccountingStandard.TT71_2024` + `TenantType.HTX` tồn tại; tenant HTX (HtxProfile) auto-map TT 71
- [ ] AccountChart seed TT 71 (51 TK cấp 1 + cấp 2/3) — không đụng TT 133/99
- [ ] Phiếu thu HTX: chỉ 511/512/558; Phiếu chi HTX: chỉ 642/658 (và 611/612 nếu duyệt)
- [ ] B01-HTX + B02-HTX render đúng cấu trúc (tách nội bộ/ngoài); HTX KHÔNG thấy B03
- [ ] Tests mới PASS (seeder, template, form, mapping) + Core.Tests/ShopERP.Tests không regress
- [ ] RV production 5-layer PASS (per `.devin/rules/runtime-verification.md`)

## 10. RELATED FILES

- **Spec:** `docs/specs/Thông tư 71-2024-TT-BTC.doc` (PL I + PL II + PL IV; extract `.devin/tt71_extracted.txt`)
- **Spec PL II (chứng từ):** `docs/Accounting_Doc/phu-luc-II-Thong-tu-71.docx` (extract `.devin/tt71_pl2_extracted.txt`)
- **Task cards:** `docs/AI/tasks/tt71_htx_accounting/task_phase{1-7}_*.md`
- **Code refs:** `1_Shared/Domain.cs:3586,3601` (enum) · `3_CoreHub/Infrastructure/Seed/AccountChartSeeder.cs` · `3_CoreHub/Services/Data/Tt99Templates.cs` · `3_CoreHub/Services/{IncomeStatementService,BalanceSheetService,HKDBookService}.cs` · `5_WebApps/ShopERP/Components/Pages/Accounting/{RevenueEntry,ExpenseEntry,TrialBalance,FinancialReports}.razor`
