# TASK CARD P2 — TT 58/2026 CHẾ ĐỘ DNSN (SỔ S1→S4d + BCTC B01/B02-DNSN + RESOLVER FIX)

> Status: ✅ DONE — deployed `102ce865` (W1-W4) + regime-lock `615d85ac` — RV production L1-L3 PASS, 34/34 sweep (2026-10-30)
> Master plan: `docs/AI/plans/tt133-tt58-accounting-compliance-master-plan.md`
> Spec đối chiếu (BẮT BUỘC — trích từ `58_2026_TT-BTC_707080.docx` gốc): `docs/AI/tasks/tt133_tt58_compliance/spec/tt58-2026-full-text.txt`
> Gap đóng: **#2** — TT 58/2026 chỉ có enum, chưa có chế độ; resolver map sai

---

## 1. MỤC TIÊU

Tenant `Enterprise_SuperSmall` (hoặc HKD chọn áp dụng — Điều 2.2 TT 58) ghi sổ + lập BCTC đúng TT 58/2026/TT-BTC (hiệu lực 01/07/2026, thay TT 132/2018).

**Nguyên tắc gốc của TT 58:** KHÔNG có hệ thống tài khoản — chế độ **sổ** (giống TT152 HKD). Danh mục sổ phụ thuộc **phương pháp nộp thuế GTGT × TNDN** của doanh nghiệp (Điều 4→8).

## 2. SPEC ĐỐI CHIẾU (đã trích xuất từ docx gốc)

### 2.1 Danh mục sổ theo 4 trường hợp (Điều 5-8)

| `DnsnTaxMethod` | Thuế GTGT | Thuế TNDN | Sổ bắt buộc |
|---|---|---|---|
| `VatPercent_TndnPercent` (Điều 5) | % trên DT | % trên DT | **S1-DNSN** |
| `VatPercent_TndnIncome` (Điều 6) | % trên DT | trên thu nhập | **S2a + S2b + S2c + S2d-DNSN** |
| `VatDeduct_TndnPercent` (Điều 7) | khấu trừ | % trên DT | **S3a + S3b-DNSN** |
| `VatDeduct_TndnIncome` (Điều 8) | khấu trừ | trên thu nhập | **S2b + S2c + S2d + S3b-DNSN** |

### 2.2 Sổ tùy chọn (Điều 9) — D2: chỉ làm S4a

| Sổ | Nội dung | Quyết định |
|---|---|---|
| S4a-DNSN | Chi tiết thanh toán công nợ (theo đối tượng) | **CÓ** — tái dùng `CongNoService`/sổ công nợ hiện có |
| S4b-DNSN | Sổ TSCĐ | DEFER (chưa có module TSCĐ) |
| S4c-DNSN | Sổ theo dõi nghĩa vụ thuế khác | DEFER |
| S4d-DNSN | Sổ theo dõi VCSH | DEFER |

### 2.3 Cấu trúc cột từng sổ (theo biểu mẫu trong spec — verify khi implement)

| Sổ | Cột chính | Nguồn dữ liệu |
|---|---|---|
| **S1-DNSN** | Hóa đơn/chứng từ (số hiệu, ngày) · Diễn giải · Số tiền — nhóm theo **nhóm hàng hóa/dịch vụ/ngành nghề**, mỗi nhóm có dòng Tổng cộng + **Thuế GTGT** + **Thuế TNDN**; cuối: Tổng thuế GTGT phải nộp + Tổng thuế TNDN phải nộp | JournalEntry doanh thu (5xx/7xx) theo `IndustrySector`; thuế = DT × tỷ lệ % ngành (tái dùng `IndustryVatRates`/`IndustryPitRates` — GTGT phần trăm; TNDN % trên DT theo quy định thuế) |
| **S2a-DNSN** | Giống S1 (doanh thu theo nhóm ngành + dòng thuế GTGT — KHÔNG dòng TNDN vì TNDN trên thu nhập) | như trên, chỉ thuế GTGT |
| **S2b-DNSN** | Sổ chi tiết doanh thu, chi phí: CT (số/ngày) · Diễn giải · Doanh thu · Chi phí (tách nhóm CP: NVL, nhân công, khấu hao, dịch vụ mua ngoài, lãi vay, khác) | JournalEntry 5xx/7xx (DT) + 6xx/8xx (CP — phân nhóm theo AccountNumber) |
| **S2c-DNSN** | Sổ chi tiết VL/DC/SP/HH: chỉ tiêu · ĐVT · tồn đầu · nhập · xuất · tồn cuối (số lượng + giá trị) | JournalEntry 15x + tồn kho SQLite ShopERP (nếu cần số lượng — ghi nhận: chỉ giá trị nếu thiếu ĐVT) |
| **S2d-DNSN** | Sổ chi tiết tiền: CT · Diễn giải · thu/chi/tồn — tách tiền mặt vs tiền gửi không kỳ hạn | JournalEntry 111 (TM) + 112 (TGNH) |
| **S3a-DNSN** | Doanh thu theo nhóm ngành (không dòng thuế — GTGT khấu trừ) | JournalEntry 5xx/7xx |
| **S3b-DNSN** | Sổ theo dõi nghĩa vụ thuế GTGT: CT · Diễn giải · GTGT đầu vào được khấu trừ · GTGT đầu ra · GTGT phải nộp/được khấu trừ chuyển kỳ sau | JournalEntry 1331 (đầu vào) + 3331 (đầu ra); chênh lệch → nghĩa vụ |
| **S4a-DNSN** | Công nợ theo đối tượng: đối tượng · dư đầu · phải thu/phải trả phát sinh · dư cuối | `CongNoService` hiện có (131/331 + Vendor) |

### 2.4 BCTC (Điều 10) — chỉ 2 mẫu, KHÔNG B03/B09

**B01-DNSN** (mã → nguồn):
| Mã | Chỉ tiêu | Nguồn |
|---|---|---|
| 110 | Tiền | 111 + 112 |
| 120 | Các khoản nợ phải thu | 131, 133, 136, 138, 141 |
| 130 | Hàng tồn kho | 15x |
| 140 | TSCĐ | 211 − 214 |
| 150 | Tài sản khác | tài sản còn lại (121, 128, 217, 241, 242, 244, 229*) |
| **200** | Tổng TS = 110+120+130+140+150 | |
| 300 | Nợ phải trả = 310+320 | |
| 310 | Các khoản nợ phải trả | 331, 334, 335, 336, 338, 341, 352, 353 |
| 320 | Thuế và các khoản phải nộp NN | 333 |
| 400 | VCSH = 410+420+430 | |
| 410 | Vốn đầu tư CSH | 411 |
| 420 | LNST chưa phân phối | 421 (+ NetIncome plug) |
| 430 | Các quỹ thuộc VCSH | 418 |
| **500** | Tổng NV = 300+400 | invariant 200=500 |

**B02-DNSN** (mã): 01 DT & thu nhập thuần (5xx+7xx) · 02 Các khoản chi phí (6xx+8xx) · 03 = 01−02 · 10 Chi phí thuế TNDN · 20 = 03−10.

⚠️ Điều 10.1b: DN nộp TNDN % trên DT **không bắt buộc nộp** BCTC — vẫn lập được (UI cho xem/xuất, không bắt buộc).
⚠️ Điều 4.3: đổi chế độ chỉ từ đầu kỳ kế toán năm — UI đổi `DnsnTaxMethod` phải cảnh báo.
⚠️ Điều 11: chuyển số dư từ TK (TT132) → sổ — dùng `OpeningBalanceService` nhập số dư đầu kỳ (ghi hướng dẫn, không auto-migrate).

## 3. THIẾT KẾ

### 3.1 Domain (Gate 5 — cần duyệt D3)

- `TenantSettings` += `DnsnTaxMethod? DnsnTaxMethod` (null = mặc định `VatPercent_TndnPercent`).
- Enum mới `DnsnTaxMethod { VatPercent_TndnPercent=1, VatPercent_TndnIncome=2, VatDeduct_TndnPercent=3, VatDeduct_TndnIncome=4 }` — append, Domain.cs.
- `AccountingBookType` += `S1_DNSN, S2a_DNSN, S2b_DNSN, S2c_DNSN, S2d_DNSN, S3a_DNSN, S3b_DNSN, S4a_DNSN` (append-only sau S3a_HKD).
- KHÔNG seed `AccountChart` cho `TT58_2026` (xác nhận thiết kế hiện tại — TT58 không có hệ thống TK; journal vẫn ghi TK nội bộ để kỹ thuật, sổ hiển thị theo mẫu).

### 3.2 Services

- `3_CoreHub/Services/Data/Tt58Templates.cs`: `Tt58BookTemplate` cho S1/S2a (group ngành + dòng thuế), S2b (DT/CP nhóm), S2c, S2d, S3a, S3b (GTGT in/out/nghĩa vụ), S4a (đối tượng công nợ). Reuse `HKDBookTemplate` base + `GenericHKDBook` nếu khớp, hoặc record mới nếu cấu trúc dòng/nhóm khác (verify spec §2.3).
- `DnsnBookService` (CoreHub): `GetDnsnBookListAsync(tenant)` → danh mục sổ theo `DnsnTaxMethod` · `GenerateBookAsync(tenant, bookType, period)` · `GenerateB01DnsnAsync` / `GenerateB02DnsnAsync` (reuse `Tt99ReportTemplate` + pattern `BalanceSheetService`).
- `ITaxMethodResolver` hoặc method trên tenant service: đọc `Settings.DnsnTaxMethod`, default `VatPercent_TndnPercent`.
- `AccountingStandardResolver`: **`Enterprise_SuperSmall` → `AccountingStandard.TT58_2026`** (fix Gap #2 core); HKD giữ route sổ HKD.

### 3.3 UI

- Trang sổ DNSN (hoặc reuse `HKDBooks.razor` với book list động theo regime): hiển thị danh mục sổ theo `DnsnTaxMethod`; mỗi sổ render bảng đúng cột spec.
- FinancialReports hub: TT58 → tab "Sổ kế toán" + "B01-DNSN" + "B02-DNSN"; ẨN B03/B09/F01.
- Admin: khi set `Type=Enterprise_SuperSmall` → chọn `DnsnTaxMethod` (modal/select trên `/admin/tenants` hoặc trang settings tenant); cảnh báo đổi chỉ từ đầu năm (Điều 4.3).

## 4. FILES TẠO/SỬA

- Tạo: `3_CoreHub/Services/Data/Tt58Templates.cs` · `3_CoreHub/Services/IDnsnBookService.cs` + `DnsnBookService.cs` · tests
- Sửa: `Domain.cs` (enum + TenantSettings — Gate 5) · `AccountingStandardResolver.cs` · `HKDBooks.razor`/`FinancialReports.razor` (hoặc trang mới `DnsnBooks.razor`) · `ShopErpMenuService.cs` (menu regime-aware) · admin tenant UI
- KHÔNG sửa: AccountChartSeeder (giữ không seed TT58), nhánh TT99/TT71/TT152

## 5. ACCEPTANCE CRITERIA

- [ ] Resolver: SuperSmall→TT58_2026 (test) · SME→TT133 · Large→TT99 · HTX→TT71
- [ ] Danh mục sổ đúng 4 trường hợp Đ5-Đ8 theo `DnsnTaxMethod` (test bảng 2.1)
- [ ] S1-DNSN: nhóm ngành + dòng Thuế GTGT/TNDN per nhóm + tổng cuối
- [ ] S3b-DNSN: GTGT đầu vào (1331) / đầu ra (3331) / nghĩa vụ đúng
- [ ] B01-DNSN đúng 14 mã + cân 200=500 · B02-DNSN đúng 5 mã
- [ ] UI ẩn B03/B09/F01 cho TT58; sổ hiển thị đúng cột
- [ ] Không regress HKD/TT133/TT99/TT71
- [ ] HKD chọn TT58 (D4): set được `AccountingStandard=TT58_2026` + sổ DNSN render

## 6. TEST CASES (tối thiểu)

- T1: resolver SuperSmall→TT58
- T2-T5: danh mục sổ đúng 4 DnsnTaxMethod
- T6: S1 dòng thuế theo ngành (2 ngành × thuế khác nhau + tổng)
- T7: S3b khấu trừ: đầu vào 1331 − đầu ra 3331 → nghĩa vụ
- T8: B01-DNSN cân 200=500 với seed journal; plug vào 420
- T9: B02-DNSN 5 mã + công thức
- T10: tenant HKD dùng TT58 (chọn áp dụng)
