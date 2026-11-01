# HƯỚNG DẪN SỬ DỤNG — NHẬP NHANH BẰNG MÔ TẢ (SMART ENTRY AI)

> Áp dụng cho: **Nhập Doanh Thu** (`/accounting/revenue`) · **Nhập Chi Phí** (`/accounting/expenses`) · **Chuyển Tiền Nội Bộ** (`/accounting/transfers`)
> Phiên bản: v1 (parser rule-based tiếng Việt) — deploy `234d2425`, flag `SmartEntryEnabled`
> Nguyên tắc vàng: **AI chỉ điền trước — người dùng luôn kiểm tra và bấm Lưu. Hệ thống không bao giờ tự lưu.**

---

## 1. Smart Entry là gì?

Thay vì tự chọn "loại phiếu + tài khoản + ngày + số tiền", bạn **nói hoặc gõ một câu tiếng Việt** mô tả nghiệp vụ. Hệ thống:

1. **Phân tích** câu → suy ra loại nghiệp vụ (intent), số tiền, ngày, phương thức thanh toán, đối tượng, nhóm chi phí.
2. **Hiển thị kết quả trích xuất** trong thẻ "Trích xuất" kèm độ tin cậy (High / Medium / Low) và cảnh báo phần còn thiếu.
3. Bạn bấm **"Áp vào form ↓"** → form được điền sẵn.
4. Bạn **kiểm tra, sửa nếu cần, bấm Lưu** như bình thường — mọi kiểm soát cũ (xem trước bút toán, cảnh báo trùng phiếu, cảnh báo số dư) vẫn chạy đầy đủ.

Nếu câu của bạn thuộc nghiệp vụ khác trang đang mở (ví dụ đang ở Phiếu Chi mà nói "rút tiền ngân hàng về quỹ"), hệ thống hiện **nút chuyển sang đúng màn hình** — bạn không phải nhớ đường dẫn.

## 2. Cách dùng (5 bước)

1. Mở trang Nhập Doanh Thu / Nhập Chi Phí / Chuyển Tiền.
2. Đầu trang bấm **"✨ Nhập bằng mô tả"** (mặc định là "Nhập thủ công" — form cũ không đổi gì).
3. Gõ câu mô tả vào ô, hoặc bấm **🎤 Nói** (nếu trình duyệt hỗ trợ mic — Chrome/Edge). Không có mic vẫn gõ tay bình thường.
4. Bấm **"Phân tích"** → đọc thẻ kết quả:
   - Badge **High** (xanh) = đủ dữ kiện · **Medium** (vàng) = thiếu 1-2 mục · **Low** (đỏ) = mơ hồ.
   - Dòng **"⚠ Thiếu: ..."** = những field hệ thống cố ý để trống, bạn phải tự điền — **hệ thống không đoán bừa**.
5. Bấm **"Áp vào form ↓"** → kiểm tra form (đặc biệt phần "Thiếu") → sửa nếu cần → **Lưu**.

## 3. Công thức câu chuẩn (nên theo)

```
[ĐỘNG TỪ NGHIỆP VỤ] + [ĐỐI TƯỢNG nếu là công nợ] + [SỐ TIỀN kèm đơn vị]
                    + [NGÀY nếu không phải hôm nay] + [PHƯƠNG THỨC nếu không phải tiền mặt]
```

Ví dụ chuẩn đầy đủ:

> `khách anh Hùng trả nợ 5 triệu hôm qua bằng chuyển khoản`

Động từ = "trả nợ" (thu nợ) · Đối tượng = "anh Hùng" · Số tiền = 5.000.000 · Ngày = hôm qua · PTTT = chuyển khoản.

**Thứ tự không bắt buộc** — nhưng đặt số tiền gần cuối và đối tượng ngay sau "khách/anh/chị/công ty" là chắc nhất.

## 4. Bảng từ khóa theo loại nghiệp vụ

### 4.1. Trên trang **Nhập Doanh Thu**

| Bạn muốn | Nói chứa từ khóa | Ví dụ | Kết quả trên form |
|---|---|---|---|
| Thu doanh thu | `thu`, `bán`, `doanh thu`, `thu tiền`, `nhận tiền` | "thu tiền bán hàng 2 triệu tiền mặt" | Loại phiếu **Thu doanh thu** |
| Bán chịu (phải thu 131) | `bán chịu`, `cho nợ`, `ghi nợ`, `khách nợ` | "bán chịu cho khách chị Lan 8 triệu" | Loại phiếu **Ghi nhận phải thu** + đối tượng |
| Khách trả nợ | `khách` + `trả nợ`/`thu nợ`/`trả tiền`/`trả góp` | "khách anh Hùng trả nợ 5 triệu" | Loại phiếu **Thu tiền khách trả nợ** + đối tượng (tự tải khoản nợ mở) |
| Khách ứng trước/cọc | `ứng trước`, `đặt cọc`, `nhận cọc` | "đặt cọc của khách anh Nam 3 triệu" | Thu tiền khách trả nợ + đối tượng + cảnh báo "trả trước, không phải doanh thu" |
| Góp vốn chủ | `góp vốn`, `vốn chủ`, `bổ sung vốn` | "chủ góp vốn thêm 200 triệu bằng tiền mặt" | Loại phiếu **Góp vốn chủ sở hữu** (Có 411 — KHÔNG phải doanh thu) |

### 4.2. Trên trang **Nhập Chi Phí**

| Bạn muốn | Nói chứa từ khóa | Ví dụ | Kết quả trên form |
|---|---|---|---|
| Chi phí thường | `chi`, `trả tiền`, `thanh toán tiền`, `tiền thuê`, `tiền điện`, `nộp thuế`, `trả lương`, `tạm ứng`, `hoàn tiền` | "chi tiền thuê mặt bằng 15 triệu hôm qua" | Loại phiếu **Chi phí** + nhóm chi + TK gợi ý |
| Mua chịu (phải trả 331) | `mua chịu`, `nợ người bán`, `nợ ncc`, `chưa thanh toán`, `chưa trả tiền` | "mua chịu vật tư của nhà cung cấp ABC 12 triệu" | Loại phiếu **Ghi nhận phải trả** + đối tượng |
| Trả tiền người bán | `nhà cung cấp`/`ncc`/`người bán` + `trả`/`thanh toán` | "trả nợ nhà cung cấp ABC 20 triệu" | Loại phiếu **Trả tiền người bán** + đối tượng |
| Nhập mua hàng hóa | `nhập hàng`, `nhập kho`, `mua hàng hóa` | "mua hàng hóa nhập kho 30 triệu" | Hiện nút **→ Phiếu Nhập Mua** (không điền phiếu chi) |

### 4.3. Nhóm chi phí → tài khoản gợi ý (trên Phiếu Chi)

| Nhóm | Từ khóa nhận diện |
|---|---|
| Thuê mặt bằng (rent) | `thuê`, `mặt bằng` |
| Điện nước (utilities) | `điện`, `nước`, `internet`, `điện thoại`, `wifi` |
| Nhân công (labor) | `lương`, `nhân công`, `thưởng`, `tạm ứng` |
| Nguyên vật liệu (materials) | `nguyên liệu`, `nguyên vật liệu`, `vật tư`, `nvl` |
| Marketing | `quảng cáo`, `marketing`, `ads`, `khuyến mãi` |
| Sửa chữa/vận chuyển (maintenance) | `sửa`, `bảo trì`, `bảo dưỡng`, `xăng`, `vận chuyển`, `ship` |

Không nhắc nhóm nào → vẫn là phiếu Chi phí, bạn tự chọn TK.

### 4.4. Chuyển tiền nội bộ (`/accounting/transfers`)

| Hướng | Nói chứa | Ví dụ | Từ TK → Đến TK |
|---|---|---|---|
| Ngân hàng → Quỹ (rút về) | `rút`/`lấy` + `ngân hàng` + `về/nhập/vào quỹ (tiền mặt)` | "rút tiền gửi ngân hàng một trăm triệu nhập vào quỹ tiền mặt" | 112 → 111 |
| Quỹ → Ngân hàng (nộp vào) | `nộp`/`gửi` + `tiền mặt`/`quỹ` + `vào ngân hàng/tài khoản` | "nộp 50 triệu tiền mặt vào tài khoản ngân hàng" | 111 → 112 |
| Có phí | thêm `phí <số>` | "rút 50 triệu từ ngân hàng về quỹ phí 30 nghìn" | 112 → 111, Phí = 30.000 (JE riêng 642) |

## 5. Cách nói số tiền — đầy đủ dạng chấp nhận

| Dạng | Ví dụ | Hệ thống hiểu |
|---|---|---|
| Số + đơn vị | `100 triệu`, `5 nghìn`, `1 tỷ` | 100.000.000 / 5.000 / 1.000.000.000 |
| Chữ viết đầy đủ | `một trăm triệu`, `hai mươi lăm nghìn`, `năm trăm nghìn` | 100.000.000 / 25.000 / 500.000 |
| Viết tắt | `100tr`, `100 củ`, `500k`, `2tr5` | 100.000.000 / 100.000.000 / 500.000 / 2.500.000 |
| Có lẻ/rưỡi | `một tỷ rưỡi`, `một trăm lẻ năm nghìn`, `1 tỷ 2` | 1.500.000.000 / 105.000 / 1.200.000.000 |
| Số có dấu phân cách | `100.000.000`, `1.500.000`, `1,5 tỷ` | 100.000.000 / 1.500.000 / 1.500.000.000 |
| Chục/mốt/lăm/nhăm | `hai chục nghìn`, `mười lăm triệu`, `hai mươi mốt` | 20.000 / 15.000.000 / 21 |

> **BẪY LỚN NHẤT — "số trần" = ĐỒNG:** `thu 500` được hiểu là **500 đồng**, không phải 500 nghìn hay 500 triệu. **Luôn nói kèm đơn vị** (nghìn / k / triệu / tr / củ / tỷ). Từ "đồng" đứng một mình KHÔNG phải đơn vị nhân — `500 đồng` vẫn là 500đ.
>
> Đơn vị hiểu được: `nghìn`, `ngàn`, `nghin`, `k` · `triệu`, `trieu`, `tr`, `củ` · `tỷ`, `tỉ` · `mươi`, `mười`, `chục`, `trăm`, `lẻ/lẽ/linh`, `rưỡi`.

## 6. Cách nói ngày

| Bạn nói | Ngày được điền |
|---|---|
| (không nói gì) / `hôm nay` | Hôm nay |
| `hôm qua`, `tối qua` | Hôm qua |
| `hôm kia`, `hôm kía` | Hôm kia |
| `ngày 25/10`, `ngày 25/10/2026`, `ngày 25-10` | Đúng ngày đó (không ghi năm → năm nay) |

> **Bẫy:** phải có chữ **"ngày"** đứng trước. `thu 1 triệu 25/10` → ngày 25/10 bị bỏ qua, phiếu ghi hôm nay. Nói `thu 1 triệu ngày 25/10`.
>
> Không hỗ trợ: "tuần trước", "tháng trước", "đầu tháng" → sẽ rơi về hôm nay; nhập ngày tay nếu cần.

## 7. Phương thức thanh toán (PTTT)

| Bạn nói | PTTT điền vào |
|---|---|
| `chuyển khoản`, `ngân hàng`, `tài khoản`, `ck` | Chuyển khoản |
| `qr`, `momo`, `zalopay`, `ví` | QR/Ví |
| `tiền mặt`, `quỹ`, hoặc không nói gì | Tiền mặt (mặc định) |

## 8. Cách nói đối tượng (phiếu công nợ)

- Đặt tên **ngay sau từ xưng hô/loại đối tượng**: `khách`, `anh`, `chị`, `cô`, `chú`, `ông`, `bà`, `em`, `công ty`, `cty`, `nhà cung cấp`, `ncc`, `người bán`, `đối tác`, `nhân viên`, `bên`.
- Tên lấy tối đa ~6 từ, dừng trước động từ/số/từ khóa nghiệp vụ.
- Từ xưng hô người (khách/anh/chị/ông/bà...) được lược bỏ khỏi tên; từ loại tổ chức được GIỮ:
  - `khách anh Hùng trả nợ` → đối tượng **"anh Hùng"**
  - `khách công ty TNHH ABC` → **"công ty TNHH ABC"**
  - `trả nợ nhà cung cấp ABC` → **"nhà cung cấp ABC"**
- `bên` là marker chung: `mua chịu của bên vận chuyển 5 triệu` → đối tượng "vận chuyển".

> Nghiệp vụ công nợ **BẮT BUỘC có đối tượng** — không nói tên → "⚠ Thiếu: đối tượng", bạn tự điền trên form.

## 9. Ma trận tình huống THÀNH CÔNG ✅

### 9.1. Phiếu Thu (Nhập Doanh Thu)

| # | Bạn nói | Loại phiếu | Số tiền | Đối tượng | PTTT | Ngày | Confidence |
|---|---|---|---|---|---|---|---|
| T1 | `thu tiền bán hàng 2 triệu tiền mặt` | Thu doanh thu | 2.000.000 | — | TM | Hôm nay | High |
| T2 | `thu doanh thu 15 triệu hôm qua chuyển khoản` | Thu doanh thu | 15.000.000 | — | CK | Hôm qua | High |
| T3 | `bán hàng thu 500k qr momo` | Thu doanh thu | 500.000 | — | QR | Hôm nay | High |
| T4 | `bán chịu cho khách chị Lan 8 triệu` | Ghi nhận phải thu | 8.000.000 | chị Lan | TM | Hôm nay | High |
| T5 | `khách anh Hùng trả nợ 5 triệu bằng chuyển khoản` | Thu tiền khách trả nợ | 5.000.000 | anh Hùng | CK | Hôm nay | High |
| T6 | `khách công ty TNHH ABC thanh toán nợ 1 tỷ 2 ngày 28/10 ck` | Thu tiền khách trả nợ | 1.200.000.000 | công ty TNHH ABC | CK | 28/10 | High |
| T7 | `thu nợ khách ông Ba trả góp 2tr5` | Thu tiền khách trả nợ | 2.500.000 | ông Ba | TM | Hôm nay | High |
| T8 | `đặt cọc của khách anh Nam 3 triệu` | Thu tiền khách trả nợ | 3.000.000 | anh Nam | TM | Hôm nay | High + cảnh báo "trả trước" |
| T9 | `chủ góp vốn thêm 200 triệu bằng tiền mặt` | Góp vốn chủ sở hữu | 200.000.000 | — | TM | Hôm nay | High |
| T10 | `nhận tiền dịch vụ tư vấn 7 triệu ngày 30/10 chuyển khoản` | Thu doanh thu | 7.000.000 | — | CK | 30/10 | High |

### 9.2. Phiếu Chi (Nhập Chi Phí)

| # | Bạn nói | Loại phiếu | Nhóm chi | Số tiền | Đối tượng | Ngày | Confidence |
|---|---|---|---|---|---|---|---|
| C1 | `chi tiền thuê mặt bằng 15 triệu hôm qua` | Chi phí | rent | 15.000.000 | — | Hôm qua | High |
| C2 | `chi 2tr5 tiền điện tháng 10` | Chi phí | utilities | 2.500.000 | — | Hôm nay | High |
| C3 | `trả lương nhân viên 80 triệu chuyển khoản` | Chi phí | labor | 80.000.000 | — | Hôm nay | High |
| C4 | `chi mua nguyên vật liệu 12 củ` | Chi phí | materials | 12.000.000 | — | Hôm nay | High |
| C5 | `chi quảng cáo facebook 3 triệu hôm kia` | Chi phí | marketing | 3.000.000 | — | Hôm kia | High |
| C6 | `thanh toán tiền xăng xe 500k` | Chi phí | maintenance | 500.000 | — | Hôm nay | High |
| C7 | `mua chịu vật tư của nhà cung cấp ABC 12 triệu` | Ghi nhận phải trả | — | 12.000.000 | nhà cung cấp ABC | Hôm nay | High |
| C8 | `trả nợ nhà cung cấp ABC 20 triệu` | Trả tiền người bán | — | 20.000.000 | nhà cung cấp ABC | Hôm nay | High |
| C9 | `thanh toán nợ cho người bán cô Tư 5tr ck` | Trả tiền người bán | — | 5.000.000 | người bán cô Tư | Hôm nay | High |
| C10 | `nộp thuế môn bài 2 triệu ngày 30/01` | Chi phí | — | 2.000.000 | — | 30/01 | High |
| C11 | `tạm ứng lương cho nhân viên Nam 5 triệu tiền mặt` | Chi phí | labor | 5.000.000 | — | Hôm nay | High |
| C12 | `hoàn tiền khách 1 triệu 5` | Chi phí | — | 1.500.000 | — | Hôm nay | High |

### 9.3. Chuyển tiền nội bộ

| # | Bạn nói | Hướng | Số tiền | Phí | Confidence |
|---|---|---|---|---|---|
| X1 | `hôm nay rút tiền gửi ngân hàng một trăm triệu nhập vào quỹ tiền mặt` | 112 → 111 | 100.000.000 | — | High |
| X2 | `nộp 50 triệu tiền mặt vào tài khoản ngân hàng` | 111 → 112 | 50.000.000 | — | High |
| X3 | `rút 50 triệu từ ngân hàng về quỹ phí 30 nghìn` | 112 → 111 | 50.000.000 | 30.000 | High |
| X4 | `gửi 20 triệu từ quỹ vào tài khoản ngân hàng hôm qua` | 111 → 112 | 20.000.000 | — | High |

## 10. Tình huống THẤT BẠI / DỄ SAI ❌ → cách nói lại đúng

### 10.1. Thiếu hoặc sai số tiền

| Bạn nói | Chuyện gì xảy ra | Nói lại đúng |
|---|---|---|
| `thu 500` | 500 **đồng** (số trần không đơn vị) — phiếu sai nặng nếu bạn không để ý | `thu 500k` hoặc `thu năm trăm nghìn` |
| `thu một trăm` | 100 đồng (không có "nghìn/triệu" sau "trăm") | `thu một trăm nghìn` / `thu một trăm triệu` |
| `khách anh Hùng trả nợ` | ⚠ Thiếu: **amount** — điền số tay sau khi Áp | `khách anh Hùng trả nợ 5 triệu` |
| `trả tiền điện tháng 10 là 2tr5` | Số tiền = **10 đồng** (!!) + cảnh báo "kiểm tra đơn vị" | "tháng 10" — số 10 trần bị đọc thành tiền TRƯỚC "2tr5" | `chi 2tr5 tiền điện tháng 10` — đặt **số tiền lên trước**, bỏ "là" |
| `thu khoảng 5 triệu` | "khoảng" bị bỏ qua — vẫn 5.000.000, OK | (vẫn đúng, nhưng nên bỏ "khoảng" cho rõ) |
| `thu 5 củ lẻ 2` | Parser hiểu **5.000.002** (lẻ 2 đồng), không phải 5.002.000 | "lẻ X" sau đơn vị lớn = X đồng, không phải X nghìn | `thu 5 triệu lẻ 2 nghìn` (5.002.000) |

### 10.2. Sai / mơ hồ loại nghiệp vụ (rủi ro cao nhất)

| Bạn nói | Hệ thống hiểu | Vì sao sai | Nói lại đúng |
|---|---|---|---|
| `thu nợ 5 triệu` (ở trang Doanh Thu) | "Trả tiền người bán" + cảnh báo → **nút đưa sang Phiếu Chi** | Thiếu chữ **"khách"** — hệ thống không biết ai nợ ai | `khách trả nợ 5 triệu` hoặc `thu nợ khách chị Lan 5 triệu` |
| `trả nợ 5 triệu` | Trả tiền người bán + cảnh báo "chưa rõ khách hay NCC" | Không nói bên nào | `trả nợ nhà cung cấp ABC 5 triệu` (chi) · `khách trả nợ 5 triệu` (thu) |
| `khách trả tiền 5 triệu` | **Thu tiền khách trả nợ** (không phải doanh thu!) | "khách + trả tiền" được ưu tiên hiểu là trả nợ | Muốn doanh thu: `thu tiền khách mua hàng 5 triệu` |
| `rút tiền ngân hàng 100 triệu` | ⚠ **Chưa xác định** (thiếu intent) | Thiếu từ khóa **quỹ/tiền mặt/két** → hệ thống không nhận ra là chuyển kho nội bộ | `rút tiền ngân hàng 100 triệu về quỹ tiền mặt` → hệ thống tự chuyển sang Phiếu Chuyển Tiền |
| `chuyển tiền 2 triệu` | Cảnh báo "hướng chưa rõ" + ⚠ thiếu tài khoản | Không nói từ đâu → đến đâu | `chuyển từ quỹ sang ngân hàng 2 triệu` |
| `mua hàng 30 triệu` | ⚠ **Chưa xác định** — "mua hàng" một mình không đủ | Phải nói **"mua hàng hóa"**, **"nhập hàng"** hoặc **"nhập kho"** | `mua hàng hóa nhập kho 30 triệu` → nút Phiếu Nhập Mua · muốn phiếu chi thường: `chi mua vật tư 30 triệu` |
| `công ty ABC trả 5 triệu hợp đồng tháng 10` | ⚠ **Chưa xác định** — "trả" trần không đủ | "trả" phải kèm `nợ`/`tiền`/`nhà cung cấp` — và "tháng 10" còn gây nhiễu số tiền | `khách công ty ABC trả nợ hợp đồng 5 triệu` (thu) · `trả nợ nhà cung cấp ABC 5 triệu` (chi) |
| `thu tiền hàng và trả nợ NCC 10 triệu` (2 việc 1 câu) | Chỉ hiểu 1 nghiệp vụ (ưu tiên trả NCC), amount lấy số đầu | Parser 1 câu = 1 nghiệp vụ | Tách 2 câu, làm 2 phiếu riêng |

### 10.3. Đối tượng bị bắt sai / thiếu

| Bạn nói | Kết quả | Vì sao | Nói lại đúng |
|---|---|---|---|
| `trả nợ 5 triệu cho anh Hùng` | Đối tượng chỉ bắt được **"Hùng"** (thiếu "anh") + loại phiếu hiểu là **Trả tiền người bán** (sai hướng công nợ!) | Không có chữ "khách" → mặc định nghiêng về người bán; marker "anh" đứng sâu sau số tiền chỉ lấy được phần đuôi tên | `khách anh Hùng trả nợ 5 triệu` — luôn bắt đầu bằng **bên + tên** |
| `khách trả nợ 5 triệu` | ⚠ Thiếu: **đối tượng** | "khách" không có tên đi kèm | `khách anh Hùng trả nợ 5 triệu` |
| `khách đặt cọc trước 3 triệu` | Đối tượng bắt nhầm thành **"đặt cọc trước"** | "đặt/cọc/trước" không nằm trong danh sách dừng → bị gom vào tên | `đặt cọc của khách anh Nam 3 triệu` — để tên đứng sau "khách" và trước số tiền |

### 10.4. Ngày / PTTT bị bỏ sót

| Bạn nói | Kết quả | Nói lại đúng |
|---|---|---|
| `thu 1 triệu 25/10` | Ngày = hôm nay (25/10 bị bỏ — thiếu chữ "ngày") | `thu 1 triệu ngày 25/10` |
| `chi tiền điện 2 triệu tuần trước` | Ngày = hôm nay ("tuần trước" không hiểu) | `chi tiền điện 2 triệu ngày 25/10` hoặc sửa ngày tay |
| `thu 5 triệu qua bank` | PTTT = **Tiền mặt** — "bank" (tiếng Anh) không nằm trong từ khóa | `thu 5 triệu chuyển khoản` hoặc `... vào tài khoản` |

### 10.5. Câu hệ thống **từ chối đoán** (đúng thiết kế)

| Bạn nói | Hiện | Bạn làm gì |
|---|---|---|
| `hôm qua có khoản thu` | ⚠ Thiếu: **amount** + confidence Low | Gõ số tiền vào form sau khi Áp |
| `blabla không liên quan` | ⚠ Thiếu: **intent** + Low | Nhập tay như bình thường (chuyển lại Nhập thủ công) |
| `anh Hùng` (chỉ tên) | ⚠ Thiếu intent + amount | Viết đủ câu nghiệp vụ |

> Nguyên tắc: **thiếu gì → để trống đó, không đoán**. Badge Medium/Low + dòng "⚠ Thiếu: ..." là tính năng an toàn, không phải lỗi.

## 11. Checklist SAU KHI bấm "Áp vào form" (30 giây kiểm tra)

| # | Kiểm | Vì sao |
|---|---|---|
| 1 | **Loại phiếu** đúng không? (Thu doanh thu / Phải thu / Trả nợ / Góp vốn · Chi phí / Phải trả / Trả người bán) | Quyết định bút toán 131/331/411/511 — sai loại = sai sổ |
| 2 | **Số tiền** — đếm số 0 | Bẫy "số trần = đồng" và "trăm" không đơn vị |
| 3 | **Ngày** | Câu thiếu chữ "ngày" → rơi về hôm nay |
| 4 | **Đối tượng** (phiếu công nợ) — đúng người, đủ tên | Bắt buộc lưu; sai tên → công nợ gán nhầm khách |
| 5 | **PTTT + Tài khoản tiền** | "chuyển khoản" → Nợ 112; tiền mặt → Nợ 111 |
| 6 | **Preview định khoản** (thẻ xem trước bút toán) | Đọc Nợ/Có trước khi Lưu — đây là bước kiểm kế toán cuối |
| 7 | **Phần "Thiếu"** đã bổ sung tay chưa | Hệ thống cố ý không đoán |

**Với chuyển tiền:** kiểm thêm Từ TK → Đến TK đúng hướng (rút về quỹ = 112→111; nộp vào NH = 111→112) và ô Phí nếu có.

## 12. Mẹo nói chuẩn & FAQ

- **Nói ngắn gọn, đủ ý:** nghiệp vụ + số tiền + (đối tượng nếu nợ) + (ngày nếu không hôm nay) + (PTTT nếu không tiền mặt). Câu dài nhiều ý → chỉ nghiệp vụ đầu được nhận.
- **1 câu = 1 phiếu.** "Vừa thu vừa chi" → làm 2 lần.
- **Diễn giải = câu gốc** — nói đủ nghĩa để sổ sách tự đọc được ("trả nợ nhà cung cấp ABC 20 triệu" tốt hơn "trả 20 triệu").
- **Mic:** bấm 🎤 → nói rõ ràng, tốc độ vừa. Kết quả gộp vào ô text — đọc lại trước khi Phân tích. Trình duyệt không hỗ trợ → nút mic tự ẩn.
- **Nói sai → sửa text rồi Phân tích lại**, hoặc Áp rồi sửa tay trên form (form vẫn là form cũ 100%).
- **Câu thuộc trang khác** → hệ thống hiện nút "→ Phiếu ..." kèm câu mô tả đi theo (deep-link), không mất câu đã gõ.

**Q: Có bị ghi nhầm doanh thu khi rút tiền ngân hàng về quỹ không?**
A: Không — nếu câu có đủ "ngân hàng" + "quỹ/tiền mặt", hệ thống nhận là chuyển tiền nội bộ và đưa sang Phiếu Chuyển Tiền (Nợ 111/Có 112 — không động vào doanh thu 511).

**Q: Tôi nói "khách trả tiền" mà muốn ghi doanh thu?**
A: "Khách + trả tiền" được hiểu là trả nợ (an toàn công nợ). Muốn doanh thu hãy nói "thu tiền bán hàng" / "doanh thu" — hoặc Áp xong đổi loại phiếu tay.

**Q: Câu quá mơ hồ có bị lưu nhầm không?**
A: Không thể. AI chỉ prefill — không có nút Lưu nào tự chạy. Câu mơ hồ chỉ điền phần chắc chắn, phần thiếu báo "⚠ Thiếu" và bạn tự điền.

**Q: Parser có hiểu tiếng Anh / viết tắt khác không?**
A: v1 chỉ tối ưu tiếng Việt kế toán phổ thông. Từ lạ/viết tắt riêng của shop → prefill phần hiểu được, bạn sửa phần còn lại.

---

*Tham chiếu kỹ thuật: `3_CoreHub/Services/VoucherTextParseService.cs` (11 intent, bảng số tiền/đơn vị/ngày/đối tượng) · task card `docs/AI/tasks/accounting_engine/task_pg_smart_entry_ai.md` · E2E `6_Testing/e2e-tests/smart-entry-flow.spec.ts`.*
