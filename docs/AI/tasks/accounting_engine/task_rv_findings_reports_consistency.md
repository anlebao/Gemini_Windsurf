# TASK CARD — RV FINDINGS: BÁO CÁO TÀI CHÍNH / SỔ SÁCH / DASHBOARD CONSISTENCY

> Status: ✅ **IMPLEMENTED (2026-11-03)** — fix xong + tests pass, CHỜ push/deploy + re-sweep production.
> Nguồn: RV sweep `6_Testing/rv-scripts/accounting-consistency-rv.sh` — production PG, kỳ 2026-11, chạy 2026-11-03.
> Report: `6_Testing/rv-scripts/accounting-consistency-report.txt` (36 PASS · 6 FAIL).
> Sweep script re-run được (read-only): `bash accounting-consistency-rv.sh [--period YYYY-MM] [--max-tenants N]`

## Phát hiện & Root Cause

### 🔴 BUG-1 — B01 BalanceSheet: tài khoản ngoài Chart bị **drop lặng** + contra-asset dấu sai (lệch **232.600.000đ** — Cafe Tân Quy)

**Triệu chứng:** `totalAssetsEnding = 291.357.500` trong khi sổ sách thật = 58.757.500. B01 vẫn "cân" A=L+E vì NetIncome plug ép Mã 417.

**Root cause (2 lỗi cộng dồn trong `BalanceSheetService.ComputeBalanceSheetDirectLinesAsync`):**

| Lỗi | Vị trí | Chi tiết |
|---|---|---|
| RC-B01a | `BalanceSheetService.cs:236-240` | `chart is null → continue` — TK **không có trong chart** bị bỏ qua ÂM THẦM. `1121` (−220.000.000) khớp prefix "112" vào Mã 110 nhưng `GetAccountAsync("1121", TT133)` = null → −220tr biến mất. Prefix-match đã "bắt" TK rồi chart-check lại vứt đi |
| RC-B01b | `BalanceSheetService.cs:247-251` + `Tt133Templates.cs:49` | contra-asset `IsNormalCredit` → `end = -end` → 214 (−6.3tr) thành **+6.3tr cộng vào Tài sản** qua Mã 152→150→200. Contra line phải **trừ** khỏi tổng TS, không phải cộng |
| RC-B01c | `BalanceSheetService.cs:~190,~353,~457` | NetIncome plug ép `200 == 500` → che imbalance thật. Không warning khi plug-delta lớn |

**Kiểm chứng:** 291.357.500 − 58.757.500 = 232.600.000 = 220.000.000 (1121 drop) + 12.600.000 (214 swing ±). Khớp tuyệt đối.

**Fix đề xuất:**
1. **Không drop âm thầm:** TK match prefix nhưng không có chart → vẫn cộng vào line (theo template intent) HOẶC gom vào dòng "Tài sản khác" + `LogWarning` liệt kê TK bị thiếu chart. Khuyến nghị: cộng theo template + warn — report không được mất số.
2. **Contra-asset:** template line có `IsNormalNegative`/`IsContra` (flag thứ 6 trong template tuple — cần xác nhận tên) → amount phải **âm** trong cộng tổng (đúng hướng hiển thị ngoặc đơn "(6.300.000)"), hoặc line 150 = 151 − |152|. Chọn theo spec B01-DNN: GT hao mòn lũy kế trình bày âm.
3. **Plug guard:** khi |plug-delta| > 0 → `LogWarning` (đã có throw khi ≠ — giữ); thêm report field hoặc log rõ delta trước plug.
4. **Test:** unit test B01 với account ngoài chart (1121) + contra 214 — assert tổng TS == sổ cái net.

### 🔴 BUG-2 — B02 IncomeStatement: TK 611 không được map → lợi nhuận phóng đại **7.501.000đ** (Cafe Tân Quy)

**Triệu chứng:** `netProfitEnding = 57.500.000` vs sổ sách 49.999.000. Lệch = đúng phát sinh TK 611.

**Root cause:** TT133 B02 template map COGS = 632, OpEx = 642 — **611 "Mua hàng" không nằm trong AccountCodes của bất kỳ line nào** → phát sinh 611 rơi ra khỏi báo cáo hoàn toàn (giống pattern B01a — drop âm thầm theo template). TK 611 là TK chuẩn TT133 (giá vốn trước kết chuyển sang 632) và đang được dùng trong production.

**Fix đề xuất:**
1. Map 611 vào Mã 11 (Giá vốn) cùng 632: `new[]{"632","611"}` — hoặc thêm line riêng theo template TT133 chuẩn (611 gộp giá vốn).
2. **Audit tương tự:** quét mọi template (B01/B02/B03 × TT58/TT71/TT99/TT133) — mọi TK có phát sinh trong chart chuẩn phải map vào ≥1 line; TK không map → warn. Nguyên tắc: **report phải bằng sổ cái, không được drop TK**.
3. Test: B02 với phát sinh 611 → assert netProfit == sổ cái net.

### 🟡 GAP-3 — Journal ↔ Entries lệch 5tr/1tr (Cafe Tân Quy) — cần xác nhận by-design

| Check | Lệch | Giả thuyết root cause |
|---|---|---|
| A4a revenue | 5.000.000 | JE bán chịu Nợ131/Có511 (entries 131=5tr khớp đúng) — entries KHÔNG ghi Revenue theo thiết kế Q1 (bán chịu không ghi DT thu tiền) → **by-design**, journal revenue > entries revenue là đúng |
| A4b expense | 1.000.000 | JE 642 từ voucher (khấu hao/ký quỹ/JV tay) không tạo AccountingEntry — entries chỉ cover thu/chi manual + order → **by-design** |

**Fix đề xuất:** nếu xác nhận by-design → sửa check A4 trong sweep (trừ JEs PostingPurpose receivable/payable/voucher-originated khỏi so sánh), không sửa code. Nếu không → bug bridge.

### 🟠 GAP-4 — Công nợ HKD: entries 131=27.134.567 / 331=−1.000.000 nhưng journal 131/331 = **0** (Vạn An Cafe HKD)

**Triệu chứng:** CongNoReport hiển thị phải thu 27tr nhưng sổ cái không có JE nào.

**Giả thuyết root cause (cần verify):**
- (a) `ManualEntryJournalBridge`/`CongNoJournalBridge` skip JEs cho tenant HKD (regime-gate — HKD không kết chuyển, có thể cũng không post JE?) 
- (b) Entries tạo trước khi bridge tồn tại (data cũ chưa backfill)
- (c) Bridge fail silently cho HKD.

**Verify query:** `SELECT "PostingPurpose", count(*) FROM "JournalEntries" WHERE "TenantId"='00000000-...-0001' GROUP BY 1` + check CongNo bridge code path cho HKD regime.

**Fix đề xuất:** xác nhận intent HKD trước — nếu HKD by-design không post JE kép → sổ cái HKD dùng entries làm source (đúng theo sổ HKD TT152), sửa check E1/E2 thành "entries vs JE **hoặc** entries-only cho HKD"; nếu HKD vẫn phải post JE → bug bridge (cần backfill + fix).

### ⚪ DATA — chất lượng dữ liệu (không phải bug code)

- **Cafe Tân Quy:** TK 1121 dư Có −220tr (ngân hàng âm — bút toán gốc sai); Nợ phải trả âm (338 −3tr, 341 −1.3tr dư Nợ); Dashboard net 46tr ≠ sổ sách net 50tr (hệ quả BUG-2/A4b).
- **An Tâm:** `Tenants.Type = NULL` → VAS bị chặn 403 (contract đúng nhưng tenant thiếu Type — cần sửa data hoặc default).
- **HKD:** entries-only data (công nợ 27tr) — cần review hạch toán.

## ✅ Implementation đã làm (2026-11-03)

| Item | Fix thực tế | Files |
|---|---|---|
| BUG-1a (drop âm thầm) | Refactor compute → **longest-prefix-wins** per-account + **include + LogWarning** cho TK không có trong chart (global + tenant). Chống luôn double-count tiềm ẩn (2147→"2147" chứ không "214"; 5117→"5117" chứ không "511"). | `BalanceSheetService.ComputeBalanceSheetDirectLinesAsync`, `IncomeStatementService.ComputeIncomeDirectLinesAsync` |
| BUG-1b (contra dấu sai) | Sign đổi sang **theo phía line**: mã 1xx/2xx (TS) giữ raw signed → contra-asset (214/229) tự ÂM trừ đúng; mã 3xx/4xx (NV) negate. (Trước: `IsNormalCredit` negate → 214 cộng dương.) | `BalanceSheetService` |
| BUG-1c (plug guard) | `LogPlugDelta` log residual trước plug ×4 đường (TT99/58/71/133) + warning riêng khi có TK unmatched (plug hấp thụ số không map). | `BalanceSheetService` |
| BUG-2 (611) | Map `"611"` vào mã 11 B02-DNN + **seed 6 TK level-2 TT133 thiếu** (1121/1122/5113/6421/6422/2147). | `Tt133Templates`, `AccountChartSeeder` |
| TT71 double-map (bonus) | 338 ra khỏi mã 130 (chỉ nợ phải trả 360) · 138 ra khỏi mã 360 (chỉ phải thu 130) — trước cộng 2 lần vào 200+300. | `Tt71Templates` |
| GAP-3 → by-design | A4a trừ JE `PostingPurpose='receivable'` (bán chịu Q1) + `ReferenceType='JournalVoucher'`; A4b trừ `JournalVoucher` (khấu hao/ký quỹ/CTGS/vay/xuất kho/thanh lý — không tạo entry). | `accounting-consistency-rv.sh` |
| GAP-4 → by-design | HKD(1)/DNSN(2) cash-basis — bridge không JE công nợ (G-C4): E1/E2 skip + info. DN/HTX chỉ chạy khi flag `ShopFeature:{t}:AccrualReceivableJE`='true' (payable-create vẫn Skipped). | `accounting-consistency-rv.sh` |
| DATA | ⏸ Chưa đụng — cần duyệt riêng (1121 âm −220tr, An Tâm Type NULL). | — |
| Chart lookup | `GetAccountAsync` → `GetTenantAccountAsync` (resolve cả tenant custom accounts đúng type/sign). | cả 2 service |

**Tests:** +5 mới (RV_BS contra-214/1129-unmapped · RV_IS 611→mã11/642x→mã24 · seeder count 51→57) · cập nhật W7_IS3 (−8M — trước OpEx bị drop) · W7_IS4 (trừ cả OpEx) · **91/91 targeted + 509/509 accounting suites PASS** · build 0 errors.

**Còn lại:** push + CD + re-sweep `accounting-consistency-rv.sh` production (target 0 FAIL — lưu ý E1/E2 giờ skip HKD) + DATA cleanup (duyệt riêng).

## Kế hoạch fix (chờ duyệt)

| Ưu tiên | Item | Phạm vi | Risk |
|---|---|---|---|
| 1 | BUG-1 B01a+B01b+B01c | `BalanceSheetService.cs` + templates | Report-only, không đổi data |
| 2 | BUG-2 611 mapping | `IncomeStatementService` / `Tt133Templates.cs` | Report-only |
| 3 | GAP-3 verify by-design → chỉnh sweep check A4 | sweep script | None |
| 4 | GAP-4 verify HKD JE intent → fix bridge hoặc chỉnh check | CongNo bridge / sweep | Cần quyết định regime |
| 5 | DATA cleanup (1121 âm, An Tâm Type NULL) | Data fix PG | Cần duyệt riêng |

**Validate sau fix:** re-run `accounting-consistency-rv.sh` toàn bộ (target 0 FAIL) + unit tests + RV L1-L2.
