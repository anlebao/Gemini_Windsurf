# GAP REGISTER — KẾ TOÁN & TUÂN THỦ PHÁP LÝ
**Ngày:** 2026-11-09 · **Nguồn:** phân tích code + đối chiếu checklist `checklist_kiem_soat_phan_mem_ke_toan_erp_UPDATED_2026.md` + đánh giá 5 yêu cầu kỹ thuật
**Phạm vi:** toàn bộ gap tìm thấy trong session (pháp lý VN, kỹ thuật kế toán, twin/double-entry, sơ đồ vận hành)

---

## A. GAP TUÂN THỦ PHÁP LÝ VIỆT NAM (checklist TT 99/2025 · NĐ 254/2026 · TT 91/2026 · TT 133 · TT 58/2026 · TT 152/2025 · TT 71/2024 · Luật QLT 2025)

### A1. Đã đạt (P0–P2 ship 10/2026)
- Audit trail (IP/UserAgent; LogPeriodClose/Reopen/reconcile)
- Append-only + reversal · khóa sổ cứng + reopen 3-role + lý do + audit
- Regime-lock TT152/133/99/58/71 (AccountingStandardRules)
- Sổ Nhật ký/Sổ Cái + sổ chi tiết công nợ/tiền (W1/W2)
- BCTC B01–B03 + B09 (+ variant HTX)
- Cảnh báo tiền mặt ≥20tr + lý do bắt buộc
- Reconcile R1–R13 (Nợ=Có, kho↔GL, cross-store, mirror)
- E-invoice provider tích hợp cơ bản

### A2. Gap còn thiếu — xếp theo mức rủi ro

| # | Yêu cầu (điều khoản) | Hiện trạng | Mức |
|---|---|---|---|
| A-G1 | **TK mới TT99/2025** — TK 215 (tài sản sinh học), TK chi tiết 82112, tên/kết cấu TK đổi — Điều 11 | Chart of accounts hardcode C# (dictionary), chưa verify đã map | **HIGH** |
| A-G2 | **Nhất quán ≥1 kỳ năm khi đổi chế độ kế toán** + trình bày lại số liệu so sánh — Điều 10-11 TT99 | Không có effective-date per-period; regime resolve theo tenant | **HIGH** |
| A-G3 | **Hóa đơn máy tính tiền kết nối chuyển TCT** (NĐ 254/2026, TT 91/2026) — POS retail/F&B/khách sạn là đối tượng bắt buộc | POS + e-invoice có; chuỗi "HĐ MCT + truyền TCT" chưa verify | **HIGH** với tenant F&B/retail |
| A-G4 | **Mã số BCTC mới PL4 TT99** — bổ sung 124/125/126, sửa chỉ tiêu 134 | BCTC services chưa verify mã số cập nhật | MEDIUM |
| A-G5 | **Đối chiếu/cảnh báo HĐ đầu vào trên cổng TCT**: trạng thái, NCC bỏ trốn/ngừng HĐ (11 TH Điều 8 TT91), chuyển HĐ có mã khi rủi ro cao (10 ngày) | Chưa thấy | MEDIUM-HIGH |
| A-G6 | **Quy trình hóa đơn điều chỉnh/thay thế** — không xóa/gạch, ghi "Điều chỉnh cho HĐ số...", kê khai đúng kỳ | Reversal kế toán có; workflow HĐ-điều-chỉnh riêng chưa verify | MEDIUM |
| A-G7 | **Gộp HĐ <20tr cùng NCC cùng ngày ≥20tr** vẫn phải thanh toán không tiền mặt | Cảnh báo per-giao-dịch có; gộp-ngày-cùng-NCC chưa thấy | MEDIUM |
| A-G8 | **Chữ ký số** ký BCTC/sổ/HĐĐT (Luật GTĐT 2023) | Chưa thấy tích hợp | MEDIUM |
| A-G9 | **Export chuẩn thanh tra** — XML định dạng TCT + sổ theo mẫu Phụ lục | Excel export có (VaIIe); XML chuẩn chưa verify | MEDIUM |
| A-G10 | **eTax** — tờ khai/nộp thuế điện tử, đối chiếu sổ↔tờ khai | Chưa tích hợp | MEDIUM (có thể ngoài MVP) |
| A-G11 | **Âm quỹ/âm kho tại thời điểm xuất** (TK 111/152/156) | `GetBalancesAsync` là nguồn cho P1.3; warning real-time chưa verify ship | MEDIUM |
| A-G12 | **Giao dịch liên kết** — hồ sơ/tờ khai GDLK | Chưa có | LOW-MEDIUM |
| A-G13 | **IFRS theo TT 118/2026** (hiệu lực 01/01/2027) | Chưa — defer hợp lý nếu không có tenant thuộc đối tượng | LOW |
| A-G14 | **Ngoại tệ quy đổi VNĐ** — Điều 6 TT99 | Chưa thấy | LOW (đa số tenant VNĐ) |
| A-G15 | **Audit giá trị trước/sau sửa** chi tiết | Q3 chốt chỉ IP/UserAgent, hash-chain defer → PARTIAL | MEDIUM |
| A-G16 | **Phân nhiệm IT-admin vs data** (TT103/2005) — admin không can thiệp dữ liệu kế toán | SystemAdmin reconcile được; direct-DB bypass log không chặn ở tầng infra | LOW (vận hành) |
| A-G17 | **Backup ≥2 nơi độc lập + restore drill định kỳ** (NĐ 174/2016) | Infra-side, chưa verify | MEDIUM (ops) |
| A-G18 | **BCTC ký bởi Kế toán trưởng + đại diện pháp luật** (trung thực/hợp lý) | Chưa thấy quy trình ký số/ký duyệt | MEDIUM |

---

## B. GAP ĐÁNH GIÁ 5 YÊU CẦU KỸ THUẬT

| # | Yêu cầu | Verdict | Chi tiết |
|---|---|---|---|
| B-1 | Immutable Posted Entries | ✅ ĐẠT | `AccountingEntry` append-only + reversal-only; `JournalEntry` sửa qua reversal. Caveat: bất biến ở domain/service, **DB-level trigger/constraint chưa verify** |
| B-2 | Audit Trail (ai, khi, trước/sau) | ⚠️ 70% | Ai/khi/correlation có; **before/after values KHÔNG có** (Q3 defer hash-chain) |
| B-3 | Rule Engine (không hardcode, cấu hình + version) | ⚠️ 60% | VoucherTypeRegistry = engine thật; nhưng chart of accounts + closing prefixes (`5xx/6xx/911/421`) + reseller `3331/1331` + BCTC logic = hardcode C#; **không có rule-version** |
| B-4 | Reconciliation Orders→phiếu→sổ→BCTC | ✅ ĐẠT* | R1–R13 có check thật (R3/R8b/R10/R11/R13). Caveat: manual on-demand SystemAdmin-only; R11/R13 fail-open (drift lặng lẽ skip — RISK-016) |
| B-5 | Period Lock + kiểm soát mở lại + lịch sử | ✅ ĐẠT | Write-path verified: `AccountingEntryService` 4 call-sites + `ManualJournalVoucherService` + `BookingAccountingService` + `CongNoService` + `ImportService` đều check `Closed` → chặn tạo entry; reopen 3-role + reason + reversal + audit |

---

## C. GAP THÍCH ỨNG LUẬT KẾ TOÁN MỚI (phân tích riêng)

| # | Điểm | Rủi ro khi luật đổi |
|---|---|---|
| C-1 | Chart of accounts trong code — "storage decision (DB vs dictionary) deferred to W3" | Sửa code, không admin-upload chart mới |
| C-2 | Hardcode prefixes đóng kỳ/kết chuyển + tax accounts rải rác | Dễ sót account khi đổi TK |
| C-3 | BCTC services per-regime KHÔNG có strategy/registry như voucher | Viết mới từng service khi đổi mẫu BCTC |
| C-4 | Không có effective-date cho regime (resolve per-tenant, không per-period) | **Gap lớn nhất**: luật hiệu lực 2027 áp sai cho kỳ 2026 |

**Điểm thích ứng TỐT:** VoucherTypeRegistry (thêm chứng từ = 1 strategy), HKD template engine (`SUM_ACCOUNT` formulas), regime-lock tập trung, append-only bảo vệ dữ liệu lịch sử, reconcile R1–R13 safety net. **Đã thích ứng luật mới 2 lần** (TT71, TT58 regime-lock).

---

## D. GAP DIGITAL TWIN / EOM (không phải pháp lý — ghi nhận session)

| # | Gap | Trạng thái |
|---|---|---|
| D-1 | `processingSteps` thiếu trên TOÀN BỘ registry (kể cả 63 entries cũ + 41 entries mới 610–650) | Registry-wide backlog |
| D-2 | `ruleRefs`/`kpiRefs`/`approvalRequired:false` explicit chưa backfill | Registry-wide backlog |
| D-3 | Narrative process-details mỏng — thiếu "Decisions & rationale" kiểu SRS | Đề xuất nâng cấp |
| D-4 | sequenceDiagram cho cross-boundary technical handoffs (NATS, reconcile HTTP) | Chưa có (chỉ swimlane) |
| D-5 | Lane title `ACTR-ID + name` theo §6.2 SRS | Hiện dùng `SYS-xxx` cho software |
| D-6 | Start/end events + decision diamonds explicit trong diagrams | Thiếu ở nhiều chỗ |
| D-7 | Drift report residual: 4 VALCN controller CONDITIONAL (stem heuristic false-positive) + 23 mandatory NATS findings | Backlog D9 |

---

## E. ƯU TIÊN ĐỀ XUẤT

**P0 — verify ngay (rẻ, rủi ro cao):**
1. **A-G2 effective-date regime** — chặn áp luật mới lên dữ liệu cũ; nền cho mọi thay đổi TT tiếp theo
2. **A-G1/A-G4** verify chart + mã số BCTC đã map TK 215/82112/124-126/134 chưa
3. **A-G3** verify chuỗi hóa đơn MCT → truyền TCT (tenant F&B/retail bắt buộc 2026)

**P1 — feature gap:**
4. **B-2** before/after audit values (hash-chain đã defer — kích hoạt lại)
5. **A-G5** đối chiếu cổng TCT + cảnh báo NCC bỏ trốn
6. **B-3/C-1..C-3** data-driven chart + strategy-registry cho BCTC/closing (voucher pattern có sẵn)
7. **A-G11** âm quỹ/âm kho real-time warning

**P2 — mở rộng:**
8. A-G6/G7/G8/G9/G10 (HĐ điều chỉnh, gộp hóa đơn, chữ ký số, XML chuẩn, eTax)
9. A-G12/G13/G14 (GDLK, IFRS TT118, ngoại tệ) — theo tenant thực tế
10. B-4 fail-open → có alert khi reconcile skip cross-store check

---

*Verify status: mọi mục "chưa verify" = chưa đọc code chứng cứ trong session — cần 1 bước kiểm tra trước khi kết luận thiếu.*
